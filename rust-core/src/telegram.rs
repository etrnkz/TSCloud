use crate::{Result, SecureCloudError};
use std::collections::HashMap;

// Simplified Telegram client for testing
pub struct TelegramClient {
    channel_id: i64,
    // In a real implementation, this would store actual Telegram connection
    mock_storage: HashMap<String, Vec<u8>>,
    next_message_id: i64,
}

impl TelegramClient {
    pub async fn new(
        _api_id: i32,
        _api_hash: String,
        channel_id: i64,
        _session_file: Option<String>,
    ) -> Result<Self> {
        Ok(Self {
            channel_id,
            mock_storage: HashMap::new(),
            next_message_id: 1,
        })
    }

    pub async fn upload_chunk(&mut self, chunk_data: Vec<u8>, chunk_id: String) -> Result<i64> {
        // Mock implementation - in reality this would upload to Telegram
        let message_id = self.next_message_id;
        self.next_message_id += 1;
        
        self.mock_storage.insert(message_id.to_string(), chunk_data);
        
        println!("Mock uploaded chunk {} as message {}", chunk_id, message_id);
        Ok(message_id)
    }

    pub async fn download_chunk(&self, message_id: i64) -> Result<Vec<u8>> {
        // Mock implementation - in reality this would download from Telegram
        self.mock_storage
            .get(&message_id.to_string())
            .cloned()
            .ok_or_else(|| SecureCloudError::Telegram("Chunk not found".to_string()))
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

// Simplified parallel uploader
pub struct ParallelUploader {
    client: TelegramClient,
    max_concurrent: usize,
}

impl ParallelUploader {
    pub fn new(client: TelegramClient, max_concurrent: usize) -> Self {
        Self {
            client,
            max_concurrent,
        }
    }

    pub async fn upload_chunks(
        &mut self,
        chunks: Vec<(String, Vec<u8>)>, // (chunk_id, data)
        progress_callback: impl Fn(usize, usize) + Send + Sync + 'static,
    ) -> Result<Vec<(String, i64)>> { // (chunk_id, message_id)
        let mut results = Vec::new();
        let total_chunks = chunks.len();

        for (i, (chunk_id, chunk_data)) in chunks.into_iter().enumerate() {
            let message_id = self.client.upload_chunk(chunk_data, chunk_id.clone()).await?;
            results.push((chunk_id, message_id));
            progress_callback(i + 1, total_chunks);
        }

        Ok(results)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_mock_telegram_client() {
        let mut client = TelegramClient::new(12345, "test_hash".to_string(), -1001234567890, None).await.unwrap();
        
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