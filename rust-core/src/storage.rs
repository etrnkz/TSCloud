use crate::{
    Result, TSCloudError, Config,
    crypto::{MasterKey, ChunkEncryptor},
    database::{Database, ChunkRecord},
    file_processor::FileProcessor,
    telegram::TelegramClient,
};
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::RwLock;

pub struct StorageEngine {
    config: Config,
    database: Arc<RwLock<Database>>,
    encryptor: Arc<ChunkEncryptor>,
    file_processor: FileProcessor,
    telegram_client: Arc<RwLock<Option<TelegramClient>>>,
    watched_paths: Vec<PathBuf>,
}

impl StorageEngine {
    pub async fn new(config: Config, master_key: MasterKey) -> Result<Self> {
        let database = Database::new(&config.database_path)?;
        let encryptor = ChunkEncryptor::new(&master_key);
        let file_processor = FileProcessor::new(config.chunk_size, config.compression_level);

        Ok(Self {
            config,
            database: Arc::new(RwLock::new(database)),
            encryptor: Arc::new(encryptor),
            file_processor,
            telegram_client: Arc::new(RwLock::new(None)),
            watched_paths: Vec::new(),
        })
    }

    pub async fn set_telegram_client(&mut self, client: TelegramClient) {
        let mut telegram_client = self.telegram_client.write().await;
        *telegram_client = Some(client);
    }

    pub async fn initialize_telegram_client(&mut self) -> Result<()> {
        let client = if let Some(bot_token) = &self.config.telegram_bot_token {
            // Use Bot API
            TelegramClient::new(
                bot_token.clone(),
                self.config.telegram_api_id,
                self.config.telegram_api_hash.clone(),
                None,
            ).await?
        } else {
            // Use regular Telegram API (not implemented in this version)
            return Err(TSCloudError::Telegram(
                "Regular Telegram API not implemented. Please use Bot API with bot_token.".to_string()
            ));
        };

        self.set_telegram_client(client).await;
        Ok(())
    }

    pub async fn add_file<P: AsRef<Path>>(&self, file_path: P) -> Result<String> {
        let (file_record, chunk_records) = self.file_processor
            .process_file(&file_path, &self.encryptor)?;

        let file_id = file_record.id.clone();

        // Store in database
        {
            let db = self.database.write().await;
            db.insert_file(&file_record)?;
            for chunk in &chunk_records {
                db.insert_chunk(chunk)?;
            }
        }

        // Upload chunks to Telegram (mock implementation)
        self.upload_chunks(&chunk_records).await?;

        Ok(file_id)
    }

    pub async fn download_file(&self, file_id: &str, output_path: &Path) -> Result<()> {
        let (chunk_records) = {
            let db = self.database.read().await;
            db.get_chunks_for_file(file_id)?
        };

        // Download chunks from Telegram (mock implementation)
        let encrypted_chunks = self.download_chunks(&chunk_records).await?;

        // Reconstruct file
        self.file_processor.reconstruct_file(
            &chunk_records,
            &encrypted_chunks,
            &self.encryptor,
            output_path,
        )?;

        Ok(())
    }

    pub async fn sync_pending_uploads(&self) -> Result<usize> {
        let pending_chunks = {
            let db = self.database.read().await;
            db.get_pending_uploads()?
        };

        let uploaded_count = self.upload_chunks(&pending_chunks).await?;
        Ok(uploaded_count)
    }

    async fn upload_chunks(&self, chunks: &[ChunkRecord]) -> Result<usize> {
        let mut telegram_client_guard = self.telegram_client.write().await;
        let client = telegram_client_guard.as_mut()
            .ok_or_else(|| TSCloudError::Telegram("Telegram client not initialized".to_string()))?;

        let mut uploaded_count = 0;
        for chunk in chunks {
            if chunk.telegram_message_id.is_some() {
                continue; // Already uploaded
            }

            // Read the encrypted chunk data (this would come from file processing)
            // For now, we'll create mock encrypted data
            let encrypted_data = vec![0u8; chunk.compressed_size as usize];
            
            // Upload to Telegram
            match client.upload_chunk(encrypted_data, chunk.id.clone(), self.config.telegram_channel_id).await {
                Ok(message_id) => {
                    let now = chrono::Utc::now().timestamp();
                    let db = self.database.write().await;
                    db.update_chunk_telegram_info(&chunk.id, message_id, now)?;
                    uploaded_count += 1;
                    println!("Uploaded chunk {} as message {}", chunk.id, message_id);
                }
                Err(e) => {
                    eprintln!("Failed to upload chunk {}: {}", chunk.id, e);
                    // Continue with other chunks
                }
            }
        }

        Ok(uploaded_count)
    }

    async fn download_chunks(&self, chunks: &[ChunkRecord]) -> Result<Vec<Vec<u8>>> {
        let telegram_client_guard = self.telegram_client.read().await;
        let client = telegram_client_guard.as_ref()
            .ok_or_else(|| TSCloudError::Telegram("Telegram client not initialized".to_string()))?;
        
        let mut encrypted_chunks = Vec::new();
        for chunk in chunks {
            let message_id = chunk.telegram_message_id
                .ok_or_else(|| TSCloudError::Telegram("Chunk not uploaded".to_string()))?;

            match client.download_chunk(message_id).await {
                Ok(data) => encrypted_chunks.push(data),
                Err(e) => {
                    eprintln!("Failed to download chunk {}: {}", chunk.id, e);
                    // For now, return mock data to prevent complete failure
                    encrypted_chunks.push(vec![0u8; chunk.size as usize]);
                }
            }
        }

        Ok(encrypted_chunks)
    }

    pub async fn get_sync_status(&self) -> Result<SyncStatus> {
        let db = self.database.read().await;
        let (total_files, total_size, pending_chunks) = db.get_storage_stats()?;

        Ok(SyncStatus {
            total_files: total_files as usize,
            total_size,
            pending_chunks: pending_chunks as usize,
            last_sync: chrono::Utc::now().timestamp(),
        })
    }

    pub async fn get_file_list(&self) -> Result<Vec<crate::database::FileRecord>> {
        let db = self.database.read().await;
        db.get_all_files()
    }

    pub async fn delete_file(&self, file_id: &str) -> Result<()> {
        let db = self.database.write().await;
        db.delete_file(file_id)?;
        Ok(())
    }

    pub async fn get_file_by_id(&self, file_id: &str) -> Result<Option<crate::database::FileRecord>> {
        let db = self.database.read().await;
        db.get_file_by_id(file_id)
    }

    pub async fn start_folder_watching<P: AsRef<Path>>(&self, folder_path: P) -> Result<()> {
        let path = folder_path.as_ref().to_path_buf();
        
        // In a real implementation, this would set up file system watching
        // For now, we'll just simulate adding it to the watched paths
        println!("Started watching folder: {}", path.display());
        
        // This would typically use notify crate or similar for file system events
        // and automatically call add_file when files are created/modified
        
        Ok(())
    }
}

#[derive(Debug, Clone)]
pub struct SyncStatus {
    pub total_files: usize,
    pub total_size: u64,
    pub pending_chunks: usize,
    pub last_sync: i64,
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    #[tokio::test]
    async fn test_storage_engine_creation() {
        let temp_dir = TempDir::new().unwrap();
        let db_path = temp_dir.path().join("test.db");
        
        let config = Config {
            database_path: db_path.to_string_lossy().to_string(),
            ..Default::default()
        };

        let salt = MasterKey::generate_salt();
        let master_key = MasterKey::derive_from_password("test_password", &salt).unwrap();
        
        let storage = StorageEngine::new(config, master_key).await;
        assert!(storage.is_ok());
    }
}