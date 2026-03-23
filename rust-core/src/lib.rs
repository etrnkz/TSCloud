pub mod crypto;
pub mod storage;
pub mod telegram;
pub mod file_processor;
pub mod database;
pub mod ffi;
pub mod config;

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

pub use ffi::*;
pub use config::AppConfig;

// Core error types
#[derive(Debug, thiserror::Error)]
pub enum TSCloudError {
    #[error("Cryptographic error: {0}")]
    Crypto(String),
    #[error("Database error: {0}")]
    Database(#[from] rusqlite::Error),
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),
    #[error("Telegram error: {0}")]
    Telegram(String),
    #[error("Serialization error: {0}")]
    Serialization(String),
}

pub type Result<T> = std::result::Result<T, TSCloudError>;

// Core configuration
#[derive(Debug, Clone)]
pub struct Config {
    pub telegram_api_id: i32,
    pub telegram_api_hash: String,
    pub telegram_bot_token: Option<String>, // For Bot API
    pub telegram_channel_id: i64,
    pub database_path: String,
    pub chunk_size: usize,
    pub compression_level: i32,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            telegram_api_id: 0,
            telegram_api_hash: String::new(),
            telegram_bot_token: None,
            telegram_channel_id: 0,
            database_path: "secure_cloud.db".to_string(),
            chunk_size: 16 * 1024 * 1024, // 16MB default
            compression_level: 3,
        }
    }
}