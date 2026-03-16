use crate::{
    Result, SecureCloudError, Config,
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
        let telegram_client = self.telegram_client.read().await;
        let _client = telegram_client.as_ref()
            .ok_or_else(|| SecureCloudError::Telegram("Telegram client not initialized".to_string()))?;

        let mut uploaded_count = 0;
        for chunk in chunks {
            if chunk.telegram_message_id.is_some() {
                continue; // Already uploaded
            }

            // For this mock implementation, we'll simulate successful upload
            let message_id = (chunk.sequence as i64) + 1000; // Mock message ID
            let now = chrono::Utc::now().timestamp();
            let db = self.database.write().await;
            db.update_chunk_telegram_info(&chunk.id, message_id, now)?;
            uploaded_count += 1;
            
            println!("Mock uploaded chunk {} as message {}", chunk.id, message_id);
        }

        Ok(uploaded_count)
    }

    async fn download_chunks(&self, chunks: &[ChunkRecord]) -> Result<Vec<Vec<u8>>> {
        let _telegram_client = self.telegram_client.read().await;
        
        // Mock implementation - in reality would download from Telegram
        let mut encrypted_chunks = Vec::new();
        for chunk in chunks {
            let _message_id = chunk.telegram_message_id
                .ok_or_else(|| SecureCloudError::Telegram("Chunk not uploaded".to_string()))?;

            // Mock encrypted chunk data
            let mock_data = vec![0u8; chunk.size as usize];
            encrypted_chunks.push(mock_data);
        }

        Ok(encrypted_chunks)
    }

    pub async fn get_sync_status(&self) -> Result<SyncStatus> {
        let db = self.database.read().await;
        let all_files = db.get_all_files()?;
        let pending_uploads = db.get_pending_uploads()?;

        let total_files = all_files.len();
        let total_size: u64 = all_files.iter().map(|f| f.size).sum();
        let pending_chunks = pending_uploads.len();

        Ok(SyncStatus {
            total_files,
            total_size,
            pending_chunks,
            last_sync: chrono::Utc::now().timestamp(),
        })
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