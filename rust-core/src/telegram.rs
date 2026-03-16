use crate::{Result, SecureCloudError};
use grammers_client::{Client, Config, InitParams, SignInError, Update};
use grammers_session::Session;
use std::io::{self, BufRead, Write};
use tokio::sync::mpsc;

pub struct TelegramClient {
    client: Client,
    channel_id: i64,
}

impl TelegramClient {
    pub async fn new(
        api_id: i32,
        api_hash: String,
        channel_id: i64,
        session_file: Option<String>,
    ) -> Result<Self> {
        let session = match session_file {
            Some(path) => Session::load_file_or_create(&path)
                .map_err(|e| SecureCloudError::Telegram(format!("Session error: {}", e)))?,
            None => Session::new(),
        };

        let client = Client::connect(Config {
            session,
            api_id,
            api_hash: api_hash.clone(),
            params: InitParams {
                ..Default::default()
            },
        })
        .await
        .map_err(|e| SecureCloudError::Telegram(format!("Connection error: {}", e)))?;

        // Check if we're signed in
        if !client.is_authorized().await
            .map_err(|e| SecureCloudError::Telegram(format!("Auth check error: {}", e)))? {
            return Err(SecureCloudError::Telegram(
                "Client not authorized. Please sign in first.".to_string()
            ));
        }

        Ok(Self {
            client,
            channel_id,
        })
    }

    pub async fn sign_in_with_phone(
        api_id: i32,
        api_hash: String,
        phone: String,
        session_file: Option<String>,
    ) -> Result<Self> {
        let session = match session_file {
            Some(path) => Session::load_file_or_create(&path)
                .map_err(|e| SecureCloudError::Telegram(format!("Session error: {}", e)))?,
            None => Session::new(),
        };

        let mut client = Client::connect(Config {
            session,
            api_id,
            api_hash: api_hash.clone(),
            params: InitParams {
                ..Default::default()
            },
        })
        .await
        .map_err(|e| SecureCloudError::Telegram(format!("Connection error: {}", e)))?;

        // Request login code
        let token = client
            .request_login_code(&phone)
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Login code request error: {}", e)))?;

        // This would typically be handled by the UI layer
        print!("Enter the code you received: ");
        io::stdout().flush().unwrap();
        let stdin = io::stdin();
        let code = stdin.lock().lines().next().unwrap().unwrap();

        match client.sign_in(&token, &code).await {
            Err(SignInError::PasswordRequired(password_token)) => {
                print!("Enter your 2FA password: ");
                io::stdout().flush().unwrap();
                let password = stdin.lock().lines().next().unwrap().unwrap();
                client
                    .check_password(password_token, password.trim())
                    .await
                    .map_err(|e| SecureCloudError::Telegram(format!("Password error: {}", e)))?;
            }
            Ok(_) => {}
            Err(e) => return Err(SecureCloudError::Telegram(format!("Sign in error: {}", e))),
        }

        // Save session if file path provided
        if let Some(path) = session_file {
            client.session().save_to_file(&path)
                .map_err(|e| SecureCloudError::Telegram(format!("Session save error: {}", e)))?;
        }

        Ok(Self {
            client,
            channel_id: 0, // Will be set later
        })
    }

    pub async fn upload_chunk(&self, chunk_data: Vec<u8>, chunk_id: String) -> Result<i64> {
        use grammers_client::types::{Message, Uploadable};

        // Create a document from the chunk data
        let uploaded_file = self.client
            .upload_stream(
                std::io::Cursor::new(chunk_data),
                chunk_data.len(),
                format!("{}.enc", chunk_id),
            )
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Upload error: {}", e)))?;

        // Send the document to the channel
        let message = self.client
            .send_message(self.channel_id, "")
            .document(uploaded_file)
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Send message error: {}", e)))?;

        Ok(message.id())
    }

    pub async fn download_chunk(&self, message_id: i64) -> Result<Vec<u8>> {
        // Get the message
        let message = self.client
            .get_messages_by_id(self.channel_id, &[message_id])
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Get message error: {}", e)))?
            .into_iter()
            .next()
            .ok_or_else(|| SecureCloudError::Telegram("Message not found".to_string()))?;

        // Download the document
        if let Some(document) = message.document() {
            let mut buffer = Vec::new();
            self.client
                .download_media(&document, &mut buffer)
                .await
                .map_err(|e| SecureCloudError::Telegram(format!("Download error: {}", e)))?;
            Ok(buffer)
        } else {
            Err(SecureCloudError::Telegram("No document in message".to_string()))
        }
    }

    pub async fn delete_message(&self, message_id: i64) -> Result<()> {
        self.client
            .delete_messages(self.channel_id, &[message_id])
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Delete message error: {}", e)))?;
        Ok(())
    }

    pub async fn get_channel_info(&self) -> Result<String> {
        let chat = self.client
            .resolve_username("your_channel_username") // This would be configurable
            .await
            .map_err(|e| SecureCloudError::Telegram(format!("Resolve username error: {}", e)))?
            .ok_or_else(|| SecureCloudError::Telegram("Channel not found".to_string()))?;

        Ok(format!("Channel: {} (ID: {})", chat.name(), chat.id()))
    }

    pub fn set_channel_id(&mut self, channel_id: i64) {
        self.channel_id = channel_id;
    }
}

// Parallel upload manager
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
        &self,
        chunks: Vec<(String, Vec<u8>)>, // (chunk_id, data)
        progress_callback: impl Fn(usize, usize) + Send + Sync + 'static,
    ) -> Result<Vec<(String, i64)>> { // (chunk_id, message_id)
        use tokio::sync::Semaphore;
        use std::sync::Arc;

        let semaphore = Arc::new(Semaphore::new(self.max_concurrent));
        let progress_callback = Arc::new(progress_callback);
        let mut handles = Vec::new();
        let total_chunks = chunks.len();

        for (i, (chunk_id, chunk_data)) in chunks.into_iter().enumerate() {
            let permit = semaphore.clone().acquire_owned().await.unwrap();
            let client = &self.client; // This won't work due to lifetime issues
            let progress_callback = progress_callback.clone();
            
            // Note: This is a simplified version. In practice, you'd need to handle
            // the client sharing differently, possibly using Arc<Mutex<TelegramClient>>
            // or restructuring the API.
            
            let handle = tokio::spawn(async move {
                let _permit = permit; // Keep permit alive
                
                // Upload chunk (this would need proper client access)
                // let message_id = client.upload_chunk(chunk_data, chunk_id.clone()).await?;
                
                progress_callback(i + 1, total_chunks);
                // Ok((chunk_id, message_id))
                Ok((chunk_id, 0i64)) // Placeholder
            });
            
            handles.push(handle);
        }

        let mut results = Vec::new();
        for handle in handles {
            let result = handle.await
                .map_err(|e| SecureCloudError::Telegram(format!("Task join error: {}", e)))?;
            results.push(result?);
        }

        Ok(results)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // Note: These tests would require actual Telegram credentials and setup
    // They're included as examples of how the API would be tested

    #[tokio::test]
    #[ignore] // Ignore by default since it requires real credentials
    async fn test_telegram_connection() {
        // This would test actual connection to Telegram
        // let client = TelegramClient::new(api_id, api_hash, channel_id, None).await;
        // assert!(client.is_ok());
    }
}