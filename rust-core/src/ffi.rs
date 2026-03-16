use crate::{Config, crypto::MasterKey, storage::{StorageEngine, SyncStatus}};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int, c_uint, c_ulonglong};
use std::ptr;
use std::sync::Arc;
use tokio::sync::RwLock;
use std::collections::HashMap;

// Global storage for engine instances
static mut ENGINES: Option<HashMap<u32, Arc<RwLock<StorageEngine>>>> = None;
static mut NEXT_ENGINE_ID: u32 = 1;

// Initialize the global storage
unsafe fn init_engines() {
    if ENGINES.is_none() {
        ENGINES = Some(HashMap::new());
    }
}

// Error codes for C interface
pub const SC_SUCCESS: c_int = 0;
pub const SC_ERROR_INVALID_PARAM: c_int = -1;
pub const SC_ERROR_CRYPTO: c_int = -2;
pub const SC_ERROR_DATABASE: c_int = -3;
pub const SC_ERROR_IO: c_int = -4;
pub const SC_ERROR_TELEGRAM: c_int = -5;
pub const SC_ERROR_NOT_FOUND: c_int = -6;

// C-compatible structures
#[repr(C)]
pub struct CSyncStatus {
    pub total_files: c_uint,
    pub total_size: c_ulonglong,
    pub pending_chunks: c_uint,
    pub last_sync: c_ulonglong,
}

#[repr(C)]
pub struct CConfig {
    pub telegram_api_id: c_int,
    pub telegram_api_hash: *const c_char,
    pub telegram_channel_id: c_ulonglong,
    pub database_path: *const c_char,
    pub chunk_size: c_uint,
    pub compression_level: c_int,
}

// Convert C config to Rust config
unsafe fn c_config_to_rust(c_config: *const CConfig) -> Option<Config> {
    if c_config.is_null() {
        return None;
    }

    let config = &*c_config;
    let api_hash = if config.telegram_api_hash.is_null() {
        String::new()
    } else {
        CStr::from_ptr(config.telegram_api_hash).to_string_lossy().to_string()
    };

    let database_path = if config.database_path.is_null() {
        "secure_cloud.db".to_string()
    } else {
        CStr::from_ptr(config.database_path).to_string_lossy().to_string()
    };

    Some(Config {
        telegram_api_id: config.telegram_api_id,
        telegram_api_hash: api_hash,
        telegram_channel_id: config.telegram_channel_id as i64,
        database_path,
        chunk_size: config.chunk_size as usize,
        compression_level: config.compression_level,
    })
}

// Initialize the storage engine
#[no_mangle]
pub unsafe extern "C" fn sc_init_engine(
    config: *const CConfig,
    password: *const c_char,
    salt: *const u8,
    salt_len: c_uint,
) -> c_int {
    init_engines();

    if config.is_null() || password.is_null() || salt.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let rust_config = match c_config_to_rust(config) {
        Some(cfg) => cfg,
        None => return SC_ERROR_INVALID_PARAM,
    };

    let password_str = match CStr::from_ptr(password).to_str() {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    if salt_len != 32 {
        return SC_ERROR_INVALID_PARAM;
    }

    let salt_slice = std::slice::from_raw_parts(salt, salt_len as usize);
    let mut salt_array = [0u8; 32];
    salt_array.copy_from_slice(salt_slice);

    let master_key = match MasterKey::derive_from_password(password_str, &salt_array) {
        Ok(key) => key,
        Err(_) => return SC_ERROR_CRYPTO,
    };

    // Create runtime for async operations
    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    let storage_engine = match rt.block_on(StorageEngine::new(rust_config, master_key)) {
        Ok(engine) => engine,
        Err(_) => return SC_ERROR_DATABASE,
    };

    let engine_id = NEXT_ENGINE_ID;
    NEXT_ENGINE_ID += 1;

    if let Some(ref mut engines) = ENGINES {
        engines.insert(engine_id, Arc::new(RwLock::new(storage_engine)));
    }

    engine_id as c_int
}

// Add a file to storage
#[no_mangle]
pub unsafe extern "C" fn sc_add_file(
    engine_id: c_int,
    file_path: *const c_char,
    file_id_out: *mut c_char,
    file_id_len: c_uint,
) -> c_int {
    if file_path.is_null() || file_id_out.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let engines = match ENGINES.as_ref() {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let engine = match engines.get(&(engine_id as u32)) {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let path_str = match CStr::from_ptr(file_path).to_str() {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    let file_id = match rt.block_on(async {
        let engine = engine.read().await;
        engine.add_file(path_str).await
    }) {
        Ok(id) => id,
        Err(_) => return SC_ERROR_IO,
    };

    let file_id_cstring = match CString::new(file_id) {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let file_id_bytes = file_id_cstring.as_bytes_with_nul();
    if file_id_bytes.len() > file_id_len as usize {
        return SC_ERROR_INVALID_PARAM;
    }

    ptr::copy_nonoverlapping(
        file_id_bytes.as_ptr(),
        file_id_out as *mut u8,
        file_id_bytes.len(),
    );

    SC_SUCCESS
}

// Download a file from storage
#[no_mangle]
pub unsafe extern "C" fn sc_download_file(
    engine_id: c_int,
    file_id: *const c_char,
    output_path: *const c_char,
) -> c_int {
    if file_id.is_null() || output_path.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let engines = match ENGINES.as_ref() {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let engine = match engines.get(&(engine_id as u32)) {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let file_id_str = match CStr::from_ptr(file_id).to_str() {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let output_path_str = match CStr::from_ptr(output_path).to_str() {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    match rt.block_on(async {
        let engine = engine.read().await;
        engine.download_file(file_id_str, std::path::Path::new(output_path_str)).await
    }) {
        Ok(_) => SC_SUCCESS,
        Err(_) => SC_ERROR_IO,
    }
}

// Get sync status
#[no_mangle]
pub unsafe extern "C" fn sc_get_sync_status(
    engine_id: c_int,
    status_out: *mut CSyncStatus,
) -> c_int {
    if status_out.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let engines = match ENGINES.as_ref() {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let engine = match engines.get(&(engine_id as u32)) {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    let status = match rt.block_on(async {
        let engine = engine.read().await;
        engine.get_sync_status().await
    }) {
        Ok(s) => s,
        Err(_) => return SC_ERROR_DATABASE,
    };

    *status_out = CSyncStatus {
        total_files: status.total_files as c_uint,
        total_size: status.total_size as c_ulonglong,
        pending_chunks: status.pending_chunks as c_uint,
        last_sync: status.last_sync as c_ulonglong,
    };

    SC_SUCCESS
}

// Sync pending uploads
#[no_mangle]
pub unsafe extern "C" fn sc_sync_pending_uploads(engine_id: c_int) -> c_int {
    let engines = match ENGINES.as_ref() {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let engine = match engines.get(&(engine_id as u32)) {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    match rt.block_on(async {
        let engine = engine.read().await;
        engine.sync_pending_uploads().await
    }) {
        Ok(_) => SC_SUCCESS,
        Err(_) => SC_ERROR_TELEGRAM,
    }
}

// Start folder watching
#[no_mangle]
pub unsafe extern "C" fn sc_start_folder_watching(
    engine_id: c_int,
    folder_path: *const c_char,
) -> c_int {
    if folder_path.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let engines = match ENGINES.as_ref() {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let engine = match engines.get(&(engine_id as u32)) {
        Some(e) => e,
        None => return SC_ERROR_NOT_FOUND,
    };

    let path_str = match CStr::from_ptr(folder_path).to_str() {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let rt = match tokio::runtime::Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return SC_ERROR_IO,
    };

    match rt.block_on(async {
        let mut engine = engine.write().await;
        engine.start_folder_watching(path_str).await
    }) {
        Ok(_) => SC_SUCCESS,
        Err(_) => SC_ERROR_IO,
    }
}

// Generate salt for key derivation
#[no_mangle]
pub unsafe extern "C" fn sc_generate_salt(salt_out: *mut u8, salt_len: c_uint) -> c_int {
    if salt_out.is_null() || salt_len != 32 {
        return SC_ERROR_INVALID_PARAM;
    }

    let salt = MasterKey::generate_salt();
    ptr::copy_nonoverlapping(salt.as_ptr(), salt_out, 32);

    SC_SUCCESS
}

// Cleanup engine
#[no_mangle]
pub unsafe extern "C" fn sc_cleanup_engine(engine_id: c_int) -> c_int {
    if let Some(ref mut engines) = ENGINES {
        engines.remove(&(engine_id as u32));
    }
    SC_SUCCESS
}

// Get last error message (placeholder - would need proper error handling)
#[no_mangle]
pub unsafe extern "C" fn sc_get_last_error(
    error_out: *mut c_char,
    error_len: c_uint,
) -> c_int {
    if error_out.is_null() {
        return SC_ERROR_INVALID_PARAM;
    }

    let error_msg = "Error details not implemented yet";
    let error_cstring = match CString::new(error_msg) {
        Ok(s) => s,
        Err(_) => return SC_ERROR_INVALID_PARAM,
    };

    let error_bytes = error_cstring.as_bytes_with_nul();
    if error_bytes.len() > error_len as usize {
        return SC_ERROR_INVALID_PARAM;
    }

    ptr::copy_nonoverlapping(
        error_bytes.as_ptr(),
        error_out as *mut u8,
        error_bytes.len(),
    );

    SC_SUCCESS
}