use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};
use serde::{Deserialize, Serialize};
use tokio::time::sleep;
use crate::telegram::TelegramClient;
use crate::database::Database;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ChannelConfig {
    pub id: i64,
    pub name: String,
    pub priority: u8,
    pub active: bool,
    pub max_file_size: u64,
    pub description: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RedundancyConfig {
    pub enabled: bool,
    pub min_copies: u8,
    pub max_copies: u8,
    pub verify_uploads: bool,
    pub auto_repair: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LoadBalancingConfig {
    pub strategy: String, // "round_robin", "priority", "least_used", "random"
    pub health_check_interval: u64,
    pub retry_attempts: u8,
    pub timeout: u64,
}

#[derive(Debug, Clone)]
pub struct ChannelHealth {
    pub channel_id: i64,
    pub is_healthy: bool,
    pub last_check: Instant,
    pub response_time: Duration,
    pub error_count: u32,
    pub success_count: u32,
    pub last_error: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileLocation {
    pub file_id: String,
    pub channel_id: i64,
    pub message_id: i64,
    pub chunk_index: Option<u32>,
    pub is_primary: bool,
    pub upload_time: i64,
    pub verified: bool,
}

pub struct MultiChannelManager {
    channels: Vec<ChannelConfig>,
    redundancy_config: RedundancyConfig,
    load_balancing_config: LoadBalancingConfig,
    channel_health: Arc<Mutex<HashMap<i64, ChannelHealth>>>,
    telegram_client: Arc<TelegramClient>,
    database: Arc<Database>,
    current_channel_index: Arc<Mutex<usize>>,
}

impl MultiChannelManager {
    pub fn new(
        channels: Vec<ChannelConfig>,
        redundancy_config: RedundancyConfig,
        load_balancing_config: LoadBalancingConfig,
        telegram_client: Arc<TelegramClient>,
        database: Arc<Database>,
    ) -> Self {
        let mut channel_health = HashMap::new();
        
        for channel in &channels {
            channel_health.insert(channel.id, ChannelHealth {
                channel_id: channel.id,
                is_healthy: true,
                last_check: Instant::now(),
                response_time: Duration::from_millis(0),
                error_count: 0,
                success_count: 0,
                last_error: None,
            });
        }

        Self {
            channels,
            redundancy_config,
            load_balancing_config,
            channel_health: Arc::new(Mutex::new(channel_health)),
            telegram_client,
            database,
            current_channel_index: Arc::new(Mutex::new(0)),
        }
    }

    pub async fn upload_file_with_redundancy(
        &self,
        file_data: &[u8],
        file_name: &str,
        original_size: u64,
    ) -> Result<Vec<FileLocation>, Box<dyn std::error::Error + Send + Sync>> {
        let mut locations = Vec::new();
        let healthy_channels = self.get_healthy_channels().await;
        
        if healthy_channels.is_empty() {
            return Err("No healthy channels available".into());
        }

        let copies_to_create = if self.redundancy_config.enabled {
            std::cmp::min(
                self.redundancy_config.max_copies as usize,
                std::cmp::max(
                    self.redundancy_config.min_copies as usize,
                    healthy_channels.len()
                )
            )
        } else {
            1
        };

        let selected_channels = self.select_channels_for_upload(&healthy_channels, copies_to_create).await;

        for (index, channel) in selected_channels.iter().enumerate() {
            match self.upload_to_channel(channel.id, file_data, file_name).await {
                Ok((message_id, file_id)) => {
                    let location = FileLocation {
                        file_id: file_id.clone(),
                        channel_id: channel.id,
                        message_id,
                        chunk_index: None,
                        is_primary: index == 0,
                        upload_time: chrono::Utc::now().timestamp(),
                        verified: false,
                    };

                    // Verify upload if enabled
                    if self.redundancy_config.verify_uploads {
                        match self.verify_upload(&location).await {
                            Ok(true) => {
                                let mut verified_location = location;
                                verified_location.verified = true;
                                locations.push(verified_location);
                                self.record_success(channel.id).await;
                            }
                            Ok(false) => {
                                self.record_error(channel.id, "Upload verification failed").await;
                                continue;
                            }
                            Err(e) => {
                                self.record_error(channel.id, &format!("Verification error: {}", e)).await;
                                continue;
                            }
                        }
                    } else {
                        locations.push(location);
                        self.record_success(channel.id).await;
                    }
                }
                Err(e) => {
                    self.record_error(channel.id, &format!("Upload failed: {}", e)).await;
                    continue;
                }
            }
        }

        if locations.is_empty() {
            return Err("Failed to upload to any channel".into());
        }

        // Store locations in database
        for location in &locations {
            self.database.store_file_location(location).await?;
        }

        Ok(locations)
    }

    pub async fn download_file_with_fallback(
        &self,
        file_id: &str,
    ) -> Result<Vec<u8>, Box<dyn std::error::Error + Send + Sync>> {
        let locations = self.database.get_file_locations(file_id).await?;
        
        if locations.is_empty() {
            return Err("No file locations found".into());
        }

        // Sort by priority: primary first, then by upload time
        let mut sorted_locations = locations;
        sorted_locations.sort_by(|a, b| {
            if a.is_primary && !b.is_primary {
                std::cmp::Ordering::Less
            } else if !a.is_primary && b.is_primary {
                std::cmp::Ordering::Greater
            } else {
                b.upload_time.cmp(&a.upload_time)
            }
        });

        let mut last_error = None;

        for location in sorted_locations {
            // Check if channel is healthy
            if !self.is_channel_healthy(location.channel_id).await {
                continue;
            }

            match self.download_from_channel(location.channel_id, &location.file_id).await {
                Ok(data) => {
                    self.record_success(location.channel_id).await;
                    return Ok(data);
                }
                Err(e) => {
                    self.record_error(location.channel_id, &format!("Download failed: {}", e)).await;
                    last_error = Some(e);
                    continue;
                }
            }
        }

        Err(last_error.unwrap_or_else(|| "All download attempts failed".into()))
    }

    pub async fn repair_missing_copies(&self) -> Result<u32, Box<dyn std::error::Error + Send + Sync>> {
        if !self.redundancy_config.auto_repair {
            return Ok(0);
        }

        let mut repaired_count = 0;
        let all_files = self.database.get_all_file_locations().await?;
        let mut file_groups: HashMap<String, Vec<FileLocation>> = HashMap::new();

        // Group locations by file_id
        for location in all_files {
            file_groups.entry(location.file_id.clone()).or_insert_with(Vec::new).push(location);
        }

        for (file_id, locations) in file_groups {
            let healthy_locations: Vec<_> = locations.iter()
                .filter(|loc| self.is_channel_healthy_sync(loc.channel_id))
                .collect();

            if healthy_locations.len() < self.redundancy_config.min_copies as usize {
                // Need to create more copies
                let copies_needed = self.redundancy_config.min_copies as usize - healthy_locations.len();
                
                if let Some(source_location) = healthy_locations.first() {
                    match self.download_from_channel(source_location.channel_id, &source_location.file_id).await {
                        Ok(file_data) => {
                            let healthy_channels = self.get_healthy_channels().await;
                            let available_channels: Vec<_> = healthy_channels.iter()
                                .filter(|ch| !locations.iter().any(|loc| loc.channel_id == ch.id))
                                .take(copies_needed)
                                .collect();

                            for channel in available_channels {
                                match self.upload_to_channel(channel.id, &file_data, &format!("repair_{}", file_id)).await {
                                    Ok((message_id, new_file_id)) => {
                                        let new_location = FileLocation {
                                            file_id: file_id.clone(),
                                            channel_id: channel.id,
                                            message_id,
                                            chunk_index: None,
                                            is_primary: false,
                                            upload_time: chrono::Utc::now().timestamp(),
                                            verified: true,
                                        };
                                        
                                        self.database.store_file_location(&new_location).await?;
                                        repaired_count += 1;
                                    }
                                    Err(e) => {
                                        self.record_error(channel.id, &format!("Repair upload failed: {}", e)).await;
                                    }
                                }
                            }
                        }
                        Err(e) => {
                            eprintln!("Failed to download file for repair: {}", e);
                        }
                    }
                }
            }
        }

        Ok(repaired_count)
    }

    pub async fn health_check_all_channels(&self) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
        for channel in &self.channels {
            if !channel.active {
                continue;
            }

            let start_time = Instant::now();
            let is_healthy = match self.telegram_client.test_channel_health(channel.id).await {
                Ok(_) => {
                    self.record_success(channel.id).await;
                    true
                }
                Err(e) => {
                    self.record_error(channel.id, &format!("Health check failed: {}", e)).await;
                    false
                }
            };

            let response_time = start_time.elapsed();
            
            if let Ok(mut health_map) = self.channel_health.lock() {
                if let Some(health) = health_map.get_mut(&channel.id) {
                    health.is_healthy = is_healthy;
                    health.last_check = Instant::now();
                    health.response_time = response_time;
                }
            }
        }

        Ok(())
    }

    pub async fn get_channel_statistics(&self) -> HashMap<i64, ChannelHealth> {
        self.channel_health.lock().unwrap().clone()
    }

    pub async fn start_background_tasks(&self) {
        let health_check_interval = Duration::from_secs(self.load_balancing_config.health_check_interval);
        let manager = Arc::new(self.clone());

        // Health check task
        let health_manager = manager.clone();
        tokio::spawn(async move {
            loop {
                if let Err(e) = health_manager.health_check_all_channels().await {
                    eprintln!("Health check error: {}", e);
                }
                sleep(health_check_interval).await;
            }
        });

        // Auto-repair task
        let repair_manager = manager.clone();
        tokio::spawn(async move {
            loop {
                sleep(Duration::from_secs(3600)).await; // Run every hour
                if let Ok(repaired) = repair_manager.repair_missing_copies().await {
                    if repaired > 0 {
                        println!("Auto-repaired {} file copies", repaired);
                    }
                }
            }
        });
    }

    // Private helper methods
    async fn get_healthy_channels(&self) -> Vec<ChannelConfig> {
        let health_map = self.channel_health.lock().unwrap();
        self.channels.iter()
            .filter(|ch| ch.active && health_map.get(&ch.id).map_or(true, |h| h.is_healthy))
            .cloned()
            .collect()
    }

    async fn select_channels_for_upload(&self, healthy_channels: &[ChannelConfig], count: usize) -> Vec<ChannelConfig> {
        let mut selected = Vec::new();
        
        match self.load_balancing_config.strategy.as_str() {
            "priority" => {
                let mut sorted_channels = healthy_channels.to_vec();
                sorted_channels.sort_by_key(|ch| ch.priority);
                selected.extend(sorted_channels.into_iter().take(count));
            }
            "round_robin" => {
                let mut index = self.current_channel_index.lock().unwrap();
                for _ in 0..count {
                    if !healthy_channels.is_empty() {
                        selected.push(healthy_channels[*index % healthy_channels.len()].clone());
                        *index = (*index + 1) % healthy_channels.len();
                    }
                }
            }
            "least_used" => {
                let health_map = self.channel_health.lock().unwrap();
                let mut sorted_channels = healthy_channels.to_vec();
                sorted_channels.sort_by_key(|ch| {
                    health_map.get(&ch.id).map_or(0, |h| h.success_count)
                });
                selected.extend(sorted_channels.into_iter().take(count));
            }
            "random" => {
                use rand::seq::SliceRandom;
                let mut rng = rand::thread_rng();
                let mut shuffled = healthy_channels.to_vec();
                shuffled.shuffle(&mut rng);
                selected.extend(shuffled.into_iter().take(count));
            }
            _ => {
                // Default to priority
                let mut sorted_channels = healthy_channels.to_vec();
                sorted_channels.sort_by_key(|ch| ch.priority);
                selected.extend(sorted_channels.into_iter().take(count));
            }
        }

        selected
    }

    async fn upload_to_channel(
        &self,
        channel_id: i64,
        file_data: &[u8],
        file_name: &str,
    ) -> Result<(i64, String), Box<dyn std::error::Error + Send + Sync>> {
        self.telegram_client.upload_file(channel_id, file_data, file_name).await
    }

    async fn download_from_channel(
        &self,
        channel_id: i64,
        file_id: &str,
    ) -> Result<Vec<u8>, Box<dyn std::error::Error + Send + Sync>> {
        self.telegram_client.download_file(channel_id, file_id).await
    }

    async fn verify_upload(&self, location: &FileLocation) -> Result<bool, Box<dyn std::error::Error + Send + Sync>> {
        // Try to download a small portion to verify the file exists and is accessible
        match self.telegram_client.get_file_info(location.channel_id, &location.file_id).await {
            Ok(_) => Ok(true),
            Err(_) => Ok(false),
        }
    }

    async fn is_channel_healthy(&self, channel_id: i64) -> bool {
        self.channel_health.lock().unwrap()
            .get(&channel_id)
            .map_or(false, |h| h.is_healthy)
    }

    fn is_channel_healthy_sync(&self, channel_id: i64) -> bool {
        self.channel_health.lock().unwrap()
            .get(&channel_id)
            .map_or(false, |h| h.is_healthy)
    }

    async fn record_success(&self, channel_id: i64) {
        if let Ok(mut health_map) = self.channel_health.lock() {
            if let Some(health) = health_map.get_mut(&channel_id) {
                health.success_count += 1;
                health.is_healthy = true;
                health.last_error = None;
            }
        }
    }

    async fn record_error(&self, channel_id: i64, error: &str) {
        if let Ok(mut health_map) = self.channel_health.lock() {
            if let Some(health) = health_map.get_mut(&channel_id) {
                health.error_count += 1;
                health.last_error = Some(error.to_string());
                
                // Mark as unhealthy if too many errors
                if health.error_count > 5 {
                    health.is_healthy = false;
                }
            }
        }
    }
}

impl Clone for MultiChannelManager {
    fn clone(&self) -> Self {
        Self {
            channels: self.channels.clone(),
            redundancy_config: self.redundancy_config.clone(),
            load_balancing_config: self.load_balancing_config.clone(),
            channel_health: self.channel_health.clone(),
            telegram_client: self.telegram_client.clone(),
            database: self.database.clone(),
            current_channel_index: self.current_channel_index.clone(),
        }
    }
}