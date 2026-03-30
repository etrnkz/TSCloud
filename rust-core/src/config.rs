use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TelegramConfig {
    pub bot_token: String,
    pub channel_id: i64,
    pub api_id: i32,
    pub api_hash: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SecurityConfig {
    pub chunk_size: usize,
    pub compression_level: i32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DatabaseConfig {
    pub path: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AppConfig {
    pub telegram: TelegramConfig,
    pub security: SecurityConfig,
    pub database: DatabaseConfig,
}

impl AppConfig {
    pub fn load_from_file(path: &str) -> Result<Self, Box<dyn std::error::Error>> {
        let content = std::fs::read_to_string(path)?;
        let config: AppConfig = serde_json::from_str(&content)?;
        Ok(config)
    }
    
    pub fn load_default() -> Result<Self, Box<dyn std::error::Error>> {
        Self::load_from_file("config.json")
    }
}

impl Default for AppConfig {
    fn default() -> Self {
        Self {
            telegram: TelegramConfig {
                bot_token: String::new(),
                channel_id: 0,
                api_id: 0,
                api_hash: String::new(),
            },
            security: SecurityConfig {
                chunk_size: 16 * 1024 * 1024, // 16MB
                compression_level: 3,
            },
            database: DatabaseConfig {
                path: "secure_cloud.db".to_string(),
            },
        }
    }
}