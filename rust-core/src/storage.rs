use crate::{
    Result, SecureCloudError, Config,
    crypto::{MasterKey, ChunkEncryptor},
    database::{Database, FileRecord, ChunkRecord},
    file_processor::FileProcessor,
    telegram::TelegramClient,
};
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::RwLock;
use notify::{Watcher, RecursiveMode, watcher, DebouncedEvent};
use std::sync::mpsc;
use std::time::Duration;

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

        // Upload chunks to Telegram
        self.upload_chunks(&chunk_records).await?;

        Ok(file_id)
    }

    pub async fn download_file(&self, file_id: &str, output_path: &Path) -> Result<()> {
        let (file_record, chunk_records) = {
            let db = self.database.read().await;
            let file = db.get_file_by_path(&file_id)
                .map_err(|_| SecureCloudError::Database(rusqlite::Error::QueryReturnedNoRows))?
                .ok_or_else(|| SecureCloudError::Database(rusqlite::Error::QueryReturnedNoRows))?;
            let chunks = db.get_chunks_for_file(&file.id)?;
            (file, chunks)
        };

        // Download chunks from Telegram
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

    pub async fn start_folder_watching<P: AsRef<Path>>(&mut self, folder_path: P) -> Result<()> {
        let path = folder_path.as_ref().to_path_buf();
        self.watched_paths.push(path.clone());

        let (tx, rx) = mpsc::channel();
        let mut watcher = watcher(tx, Duration::from_secs(2))
            .map_err(|e| SecureCloudError::Io(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("Watcher creation error: {}", e)
            )))?;

        watcher.watch(&path, RecursiveMode::Recursive)
            .map_err(|e| SecureCloudError::Io(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("Watch error: {}", e)
            )))?;

        // Spawn a task to handle file system events
        let storage_engine = self.clone_for_watcher().await;
        tokio::spawn(async move {
            loop {
                match rx.recv() {
                    Ok(event) => {
                        if let Err(e) = storage_engine.handle_fs_event(event).await {
                            eprintln!("Error handling file system event: {}", e);
                        }
                    }
                    Err(e) => {
                        eprintln!("Watcher channel error: {}", e);
                        break;
                    }
                }
            }
        });

        Ok(())
    }

    async fn clone_for_watcher(&self) -> Self {
        Self {
            config: self.config.clone(),
            database: self.database.clone(),
            encryptor: self.encryptor.clone(),
            file_processor: FileProcessor::new(self.config.chunk_size, self.config.compression_level),
            telegram_client: self.telegram_client.clone(),
            watched_paths: self.watched_paths.clone(),
        }
    }

    async fn handle_fs_event(&self, event: DebouncedEvent) -> Result<()> {
        match event {
            DebouncedEvent::Create(path) | DebouncedEvent::Write(path) => {
                if path.is_file() {
                    println!("File changed: {:?}", path);
                    if let Err(e) = self.add_file(&path).await {
                        eprintln!("Error adding file {:?}: {}", path, e);
                    }
                }
            }
            DebouncedEvent::Remove(path) => {
                println!("File removed: {:?}", path);
                // Handle file removal - mark as deleted in database
                // Implementation would depend on requirements
            }
            _ => {}
        }
        Ok(())
    }

    async fn upload_chunks(&self, chunks: &[ChunkRecord]) -> Result<usize> {
        let telegram_client = self.telegram_client.read().await;
        let client = telegram_client.as_ref()
            .ok_or_else(|| SecureCloudError::Telegram("Telegram client not initialized".to_string()))?;

        let mut uploaded_count = 0;
        for chunk in chunks {
            if chunk.telegram_message_id.is_some() {
                continue; // Already uploaded
            }

            // For this example, we'll assume chunk data is stored temporarily
            // In a real implementation, you'd need to reconstruct the encrypted chunk
            let chunk_data = vec![0u8; chunk.size as usize]; // Placeholder

            match client.upload_chunk(chunk_data, chunk.id.clone()).await {
                Ok(message_id) => {
                    let now = chrono::Utc::now().timestamp();
                    let db = self.database.write().await;
                    db.update_chunk_telegram_info(&chunk.id, message_id, now)?;
                    uploaded_count += 1;
                }
                Err(e) => {
                    eprintln!("Failed to upload chunk {}: {}", chunk.id, e);
                }
            }
        }

        Ok(uploaded_count)
    }

    async fn download_chunks(&self, chunks: &[ChunkRecord]) -> Result<Vec<Vec<u8>>> {
        let telegram_client = self.telegram_client.read().await;
        let client = telegram_client.as_ref()
            .ok_or_else(|| SecureCloudError::Telegram("Telegram client not initialized".to_string()))?;

        let mut encrypted_chunks = Vec::new();
        for chunk in chunks {
            let message_id = chunk.telegram_message_id
                .ok_or_else(|| SecureCloudError::Telegram("Chunk not uploaded".to_string()))?;

            let chunk_data = client.download_chunk(message_id).await?;
            encrypted_chunks.push(chunk_data);
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
            last_sync: chrono::Utc::now().timestamp(), // Placeholder
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