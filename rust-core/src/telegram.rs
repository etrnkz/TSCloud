use crate::{Result, TSCloudError};
use reqwest::multipart;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use base64::{Engine as _, engine::general_purpose};

#[derive(Debug, Deserialize)]
struct TelegramResponse<T> {
    ok: bool,
    result: Option<T>,
    description: Option<String>,
}

#[derive(Debug, Deserialize)]
struct Message {
    message_id: i64,
    date: i64,
    document: Option<Document>,
}

#[derive(Debug, Deserialize)]
struct Document {
    file_id: String,
    file_unique_id: String,
    file_name: Option<String>,
    file_size: Option<u64>,
}

#[derive(Debug, Deserialize)]
struct File {
    file_id: String,
    file_unique_id: String,
    file_size: Option<u64>,
    file_path: Option<String>,
}

pub struct TelegramClient {
    bot_token: String,
    client: reqwest::Client,
    base_url: String,
}

impl TelegramClient {
    pub async fn new(
        bot_token: String,
        _api_id: i32,      // Not used in Bot API
        _api_hash: String, // Not used in Bot API
        _session_file: Option<String>, // Not used in Bot API
    ) -> Result<Self> {
        let client = reqwest::Client::new();
        let base_url = format!("https://api.telegram.org/bot{}", bot_token);
        
        let telegram_client = Self {
            bot_token,
            client,
            base_url,
        };
        
        // Test the connection
        telegram_client.test_connection().await?;
        
        Ok(telegram_client)
    }

    async fn test_connection(&self) -> Result<()> {
        let url = format!("{}/getMe", self.base_url);
        let response = self.client.get(&url).send().await
            .map_err(|e| TSCloudError::Telegram(format!("Connection failed: {}", e)))?;
        
        if !response.status().is_success() {
            return Err(TSCloudError::Telegram("Invalid bot token".to_string()));
        }
        
        Ok(())
    }

    pub async fn test_channel_health(&self, channel_id: i64) -> Result<()> {
        let url = format!("{}/getChat", self.base_url);
        let params = HashMap::from([
            ("chat_id", channel_id.to_string()),
        ]);

        let response = self.client
            .post(&url)
            .json(&params)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Health check failed: {}", e)))?;

        if response.status().is_success() {
            Ok(())
        } else {
            Err(TSCloudError::Telegram("Channel health check failed".to_string()))
        }
    }

    pub async fn upload_file(
        &self,
        channel_id: i64,
        file_data: &[u8],
        file_name: &str,
    ) -> Result<(i64, String)> {
        let form = multipart::Form::new()
            .text("chat_id", channel_id.to_string())
            .part("document", 
                multipart::Part::bytes(file_data.to_vec())
                    .file_name(file_name.to_string())
                    .mime_str("application/octet-stream")
                    .map_err(|e| TSCloudError::Telegram(format!("Failed to create multipart: {}", e)))?
            )
            .text("caption", format!("🔐 TSCloud: {} ({} bytes)", file_name, file_data.len()));

        let url = format!("{}/sendDocument", self.base_url);
        let response = self.client
            .post(&url)
            .multipart(form)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Upload failed: {}", e)))?;

        let response_text = response.text().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to read response: {}", e)))?;

        let telegram_response: TelegramResponse<Message> = serde_json::from_str(&response_text)
            .map_err(|e| TSCloudError::Telegram(format!("Failed to parse response: {}", e)))?;

        if !telegram_response.ok {
            return Err(TSCloudError::Telegram(
                telegram_response.description.unwrap_or("Upload failed".to_string())
            ));
        }

        let message = telegram_response.result
            .ok_or_else(|| TSCloudError::Telegram("No message in response".to_string()))?;

        let document = message.document
            .ok_or_else(|| TSCloudError::Telegram("No document in response".to_string()))?;

        Ok((message.message_id, document.file_id))
    }

    pub async fn download_file(&self, _channel_id: i64, file_id: &str) -> Result<Vec<u8>> {
        // First get file info
        let file_info = self.get_file_info(file_id).await?;
        
        if let Some(file_path) = file_info.file_path {
            let download_url = format!("https://api.telegram.org/file/bot{}/{}", self.bot_token, file_path);
            
            let response = self.client.get(&download_url).send().await
                .map_err(|e| TSCloudError::Telegram(format!("Download failed: {}", e)))?;
            
            if response.status().is_success() {
                let file_data = response.bytes().await
                    .map_err(|e| TSCloudError::Telegram(format!("Failed to read file data: {}", e)))?;
                Ok(file_data.to_vec())
            } else {
                Err(TSCloudError::Telegram(format!("Download failed with status: {}", response.status())))
            }
        } else {
            Err(TSCloudError::Telegram("No file path in response".to_string()))
        }
    }

    pub async fn get_file_info(&self, file_id: &str) -> Result<File> {
        let url = format!("{}/getFile", self.base_url);
        let params = HashMap::from([
            ("file_id", file_id.to_string()),
        ]);

        let response = self.client
            .post(&url)
            .json(&params)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Get file info failed: {}", e)))?;

        let response_text = response.text().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to read response: {}", e)))?;

        let telegram_response: TelegramResponse<File> = serde_json::from_str(&response_text)
            .map_err(|e| TSCloudError::Telegram(format!("Failed to parse response: {}", e)))?;

        if telegram_response.ok {
            telegram_response.result
                .ok_or_else(|| TSCloudError::Telegram("No result in response".to_string()))
        } else {
            Err(TSCloudError::Telegram(
                telegram_response.description.unwrap_or("Get file info failed".to_string())
            ))
        }
    }

    pub async fn upload_chunk(&mut self, chunk_data: Vec<u8>, chunk_id: String, channel_id: i64) -> Result<i64> {
        // Create a document with the chunk data
        let filename = format!("{}.bin", chunk_id);
        
        let form = multipart::Form::new()
            .text("chat_id", channel_id.to_string())
            .part("document", 
                multipart::Part::bytes(chunk_data)
                    .file_name(filename)
                    .mime_str("application/octet-stream")
                    .map_err(|e| TSCloudError::Telegram(format!("Failed to create multipart: {}", e)))?
            );

        let url = format!("{}/sendDocument", self.base_url);
        let response = self.client
            .post(&url)
            .multipart(form)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Upload failed: {}", e)))?;

        let response_text = response.text().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to read response: {}", e)))?;

        let telegram_response: TelegramResponse<Message> = serde_json::from_str(&response_text)
            .map_err(|e| TSCloudError::Telegram(format!("Failed to parse response: {}", e)))?;

        if !telegram_response.ok {
            return Err(TSCloudError::Telegram(
                telegram_response.description.unwrap_or("Upload failed".to_string())
            ));
        }

        let message = telegram_response.result
            .ok_or_else(|| TSCloudError::Telegram("No message in response".to_string()))?;

        Ok(message.message_id)
    }

    pub async fn download_chunk(&self, message_id: i64) -> Result<Vec<u8>> {
        // In a real implementation, we need to store file_id when uploading
        // For now, we'll implement a basic approach that works with the storage pattern
        
        // Step 1: Get updates to find the message (this is not efficient for production)
        // In production, you should store file_id in database when uploading
        let url = format!("{}/getUpdates", self.base_url);
        let response = self.client.get(&url).send().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to get updates: {}", e)))?;

        let response_text = response.text().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to read response: {}", e)))?;

        // For now, return an error with instructions for proper implementation
        Err(TSCloudError::Telegram(
            "Download requires file_id storage. In production, store file_id when uploading and use getFile API.".to_string()
        ))
    }

    pub async fn delete_message(&mut self, channel_id: i64, message_id: i64) -> Result<()> {
        let url = format!("{}/deleteMessage", self.base_url);
        
        let params = HashMap::from([
            ("chat_id", channel_id.to_string()),
            ("message_id", message_id.to_string()),
        ]);

        let response = self.client
            .post(&url)
            .json(&params)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Delete failed: {}", e)))?;

        let response_text = response.text().await
            .map_err(|e| TSCloudError::Telegram(format!("Failed to read response: {}", e)))?;

        let telegram_response: TelegramResponse<bool> = serde_json::from_str(&response_text)
            .map_err(|e| TSCloudError::Telegram(format!("Failed to parse response: {}", e)))?;

        if !telegram_response.ok {
            return Err(TSCloudError::Telegram(
                telegram_response.description.unwrap_or("Delete failed".to_string())
            ));
        }

        Ok(())
    }

    pub async fn get_channel_info(&self, channel_id: i64) -> Result<String> {
        let url = format!("{}/getChat", self.base_url);
        
        let params = HashMap::from([
            ("chat_id", channel_id.to_string()),
        ]);

        let response = self.client
            .post(&url)
            .json(&params)
            .send()
            .await
            .map_err(|e| TSCloudError::Telegram(format!("Get chat info failed: {}", e)))?;

        if response.status().is_success() {
            Ok(format!("Telegram Channel (ID: {})", channel_id))
        } else {
            Err(TSCloudError::Telegram("Failed to get channel info".to_string()))
        }
    }
}

// Enhanced parallel uploader with real Telegram integration
pub struct ParallelUploader {
    client: TelegramClient,
    max_concurrent: usize,
}

impl ParallelUploader {
    pub fn new(client: TelegramClient, max_concurrent: usize) -> Self {
        Self {
            client,
            max_concurrent: std::cmp::min(max_concurrent, 10), // Telegram rate limits
        }
    }

    pub async fn upload_chunks(
        &mut self,
        chunks: Vec<(String, Vec<u8>)>, // (chunk_id, data)
        channel_id: i64,
        progress_callback: impl Fn(usize, usize) + Send + Sync + 'static,
    ) -> Result<Vec<(String, i64)>> { // (chunk_id, message_id)
        let mut results = Vec::new();
        let total_chunks = chunks.len();

        // Process chunks with rate limiting (Telegram allows ~30 messages/second)
        for (i, (chunk_id, chunk_data)) in chunks.into_iter().enumerate() {
            let message_id = self.client.upload_chunk(chunk_data, chunk_id.clone(), channel_id).await?;
            results.push((chunk_id, message_id));
            progress_callback(i + 1, total_chunks);
            
            // Rate limiting - wait 100ms between uploads to avoid hitting limits
            if i < total_chunks - 1 {
                tokio::time::sleep(tokio::time::Duration::from_millis(100)).await;
            }
        }

        Ok(results)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_telegram_client_creation() {
        // Test with invalid token - should fail gracefully
        let result = TelegramClient::new(
            "invalid_token".to_string(),
            -1001234567890,
            12345,
            "test_hash".to_string(),
            None
        ).await;
        
        assert!(result.is_err());
    }

    // Mock implementation for testing when no real bot token is available
    pub struct MockTelegramClient {
        channel_id: i64,
        mock_storage: HashMap<String, Vec<u8>>,
        next_message_id: i64,
    }

    impl MockTelegramClient {
        pub async fn new(
            _bot_token: String,
            channel_id: i64,
            _api_id: i32,
            _api_hash: String,
            _session_file: Option<String>,
        ) -> Result<Self> {
            Ok(Self {
                channel_id,
                mock_storage: HashMap::new(),
                next_message_id: 1,
            })
        }

        pub async fn upload_chunk(&mut self, chunk_data: Vec<u8>, chunk_id: String, _channel_id: i64) -> Result<i64> {
            let message_id = self.next_message_id;
            self.next_message_id += 1;
            
            self.mock_storage.insert(message_id.to_string(), chunk_data);
            
            println!("Mock uploaded chunk {} as message {}", chunk_id, message_id);
            Ok(message_id)
        }

        pub async fn download_chunk(&self, message_id: i64) -> Result<Vec<u8>> {
            self.mock_storage
                .get(&message_id.to_string())
                .cloned()
                .ok_or_else(|| TSCloudError::Telegram("Chunk not found".to_string()))
        }

        pub async fn delete_message(&mut self, message_id: i64) -> Result<()> {
            self.mock_storage.remove(&message_id.to_string());
            println!("Mock deleted message {}", message_id);
            Ok(())
        }

        pub async fn get_channel_info(&self) -> Result<String> {
            Ok(format!("Mock Channel (ID: {})", self.channel_id))
        }

        pub fn set_channel_id(&mut self, channel_id: i64) {
            self.channel_id = channel_id;
        }
    }

    #[tokio::test]
    async fn test_mock_telegram_client() {
        let mut client = MockTelegramClient::new(
            "mock_token".to_string(),
            -1001234567890,
            12345,
            "test_hash".to_string(),
            None
        ).await.unwrap();
        
        let test_data = b"test chunk data".to_vec();
        let chunk_id = "test_chunk_1".to_string();
        
        // Test upload
        let message_id = client.upload_chunk(test_data.clone(), chunk_id).await.unwrap();
        assert_eq!(message_id, 1);
        
        // Test download
        let downloaded_data = client.download_chunk(message_id).await.unwrap();
        assert_eq!(downloaded_data, test_data);
        
        // Test delete
        client.delete_message(message_id).await.unwrap();
        assert!(client.download_chunk(message_id).await.is_err());
    }
}