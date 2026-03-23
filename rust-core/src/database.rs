use crate::{Result, TSCloudError, crypto::MasterKey};
use rusqlite::{Connection, params, Row};
use serde::{Serialize, Deserialize};
use std::path::Path;
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileRecord {
    pub id: String,
    pub path: String,
    pub size: u64,
    pub hash: Vec<u8>,
    pub compressed_size: u64,
    pub chunk_count: u32,
    pub created_at: i64,
    pub updated_at: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ChunkRecord {
    pub id: String,
    pub file_id: String,
    pub sequence: u32,
    pub size: u32,
    pub compressed_size: u32,
    pub hash: Vec<u8>,
    pub nonce: Vec<u8>,
    pub telegram_message_id: Option<i64>,
    pub uploaded_at: Option<i64>,
}

pub struct Database {
    conn: Connection,
}

impl Database {
    pub fn new<P: AsRef<Path>>(path: P) -> Result<Self> {
        let conn = Connection::open(path)?;
        let db = Self { conn };
        db.initialize_schema()?;
        Ok(db)
    }

    fn initialize_schema(&self) -> Result<()> {
        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS files (
                id TEXT PRIMARY KEY,
                path TEXT NOT NULL UNIQUE,
                size INTEGER NOT NULL,
                hash BLOB NOT NULL,
                compressed_size INTEGER NOT NULL,
                chunk_count INTEGER NOT NULL,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            )",
            [],
        )?;

        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS chunks (
                id TEXT PRIMARY KEY,
                file_id TEXT NOT NULL,
                sequence INTEGER NOT NULL,
                size INTEGER NOT NULL,
                compressed_size INTEGER NOT NULL,
                hash BLOB NOT NULL,
                nonce BLOB NOT NULL,
                telegram_message_id INTEGER,
                uploaded_at INTEGER,
                FOREIGN KEY (file_id) REFERENCES files (id) ON DELETE CASCADE
            )",
            [],
        )?;

        // File locations table for multi-channel support
        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS file_locations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id TEXT NOT NULL,
                channel_id INTEGER NOT NULL,
                message_id INTEGER NOT NULL,
                chunk_index INTEGER,
                is_primary BOOLEAN NOT NULL DEFAULT 0,
                upload_time INTEGER NOT NULL,
                verified BOOLEAN NOT NULL DEFAULT 0,
                telegram_file_id TEXT NOT NULL
            )",
            [],
        )?;

        // Channel health table
        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS channel_health (
                channel_id INTEGER PRIMARY KEY,
                is_healthy BOOLEAN NOT NULL DEFAULT 1,
                last_check INTEGER NOT NULL,
                response_time INTEGER NOT NULL DEFAULT 0,
                error_count INTEGER NOT NULL DEFAULT 0,
                success_count INTEGER NOT NULL DEFAULT 0,
                last_error TEXT
            )",
            [],
        )?;

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_chunks_file_id ON chunks (file_id)",
            [],
        )?;

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_chunks_sequence ON chunks (file_id, sequence)",
            [],
        )?;

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_file_locations_file_id ON file_locations (file_id)",
            [],
        )?;

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_file_locations_channel_id ON file_locations (channel_id)",
            [],
        )?;

        Ok(())
    }

    pub fn insert_file(&self, file: &FileRecord) -> Result<()> {
        self.conn.execute(
            "INSERT OR REPLACE INTO files 
             (id, path, size, hash, compressed_size, chunk_count, created_at, updated_at)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![
                file.id,
                file.path,
                file.size as i64,
                file.hash,
                file.compressed_size as i64,
                file.chunk_count as i64,
                file.created_at,
                file.updated_at
            ],
        )?;
        Ok(())
    }

    pub fn get_file_by_path(&self, path: &str) -> Result<Option<FileRecord>> {
        let mut stmt = self.conn.prepare(
            "SELECT id, path, size, hash, compressed_size, chunk_count, created_at, updated_at
             FROM files WHERE path = ?1"
        )?;

        let file = stmt.query_row(params![path], |row| {
            Ok(FileRecord {
                id: row.get(0)?,
                path: row.get(1)?,
                size: row.get::<_, i64>(2)? as u64,
                hash: row.get(3)?,
                compressed_size: row.get::<_, i64>(4)? as u64,
                chunk_count: row.get::<_, i64>(5)? as u32,
                created_at: row.get(6)?,
                updated_at: row.get(7)?,
            })
        });

        match file {
            Ok(f) => Ok(Some(f)),
            Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
            Err(e) => Err(e.into()),
        }
    }

    pub fn insert_chunk(&self, chunk: &ChunkRecord) -> Result<()> {
        self.conn.execute(
            "INSERT OR REPLACE INTO chunks 
             (id, file_id, sequence, size, compressed_size, hash, nonce, telegram_message_id, uploaded_at)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)",
            params![
                chunk.id,
                chunk.file_id,
                chunk.sequence as i64,
                chunk.size as i64,
                chunk.compressed_size as i64,
                chunk.hash,
                chunk.nonce,
                chunk.telegram_message_id,
                chunk.uploaded_at
            ],
        )?;
        Ok(())
    }

    pub fn get_chunks_for_file(&self, file_id: &str) -> Result<Vec<ChunkRecord>> {
        let mut stmt = self.conn.prepare(
            "SELECT id, file_id, sequence, size, compressed_size, hash, nonce, telegram_message_id, uploaded_at
             FROM chunks WHERE file_id = ?1 ORDER BY sequence"
        )?;

        let chunk_iter = stmt.query_map(params![file_id], |row| {
            Ok(ChunkRecord {
                id: row.get(0)?,
                file_id: row.get(1)?,
                sequence: row.get::<_, i64>(2)? as u32,
                size: row.get::<_, i64>(3)? as u32,
                compressed_size: row.get::<_, i64>(4)? as u32,
                hash: row.get(5)?,
                nonce: row.get(6)?,
                telegram_message_id: row.get(7)?,
                uploaded_at: row.get(8)?,
            })
        })?;

        let mut chunks = Vec::new();
        for chunk in chunk_iter {
            chunks.push(chunk?);
        }
        Ok(chunks)
    }

    pub fn update_chunk_telegram_info(&self, chunk_id: &str, message_id: i64, uploaded_at: i64) -> Result<()> {
        self.conn.execute(
            "UPDATE chunks SET telegram_message_id = ?1, uploaded_at = ?2 WHERE id = ?3",
            params![message_id, uploaded_at, chunk_id],
        )?;
        Ok(())
    }

    pub fn delete_file(&self, file_id: &str) -> Result<()> {
        self.conn.execute("DELETE FROM files WHERE id = ?1", params![file_id])?;
        Ok(())
    }

    pub fn get_all_files(&self) -> Result<Vec<FileRecord>> {
        let mut stmt = self.conn.prepare(
            "SELECT id, path, size, hash, compressed_size, chunk_count, created_at, updated_at
             FROM files ORDER BY path"
        )?;

        let file_iter = stmt.query_map([], |row| {
            Ok(FileRecord {
                id: row.get(0)?,
                path: row.get(1)?,
                size: row.get::<_, i64>(2)? as u64,
                hash: row.get(3)?,
                compressed_size: row.get::<_, i64>(4)? as u64,
                chunk_count: row.get::<_, i64>(5)? as u32,
                created_at: row.get(6)?,
                updated_at: row.get(7)?,
            })
        })?;

        let mut files = Vec::new();
        for file in file_iter {
            files.push(file?);
        }
        Ok(files)
    }

    pub fn get_pending_uploads(&self) -> Result<Vec<ChunkRecord>> {
        let mut stmt = self.conn.prepare(
            "SELECT id, file_id, sequence, size, compressed_size, hash, nonce, telegram_message_id, uploaded_at
             FROM chunks WHERE telegram_message_id IS NULL ORDER BY file_id, sequence"
        )?;

        let chunk_iter = stmt.query_map([], |row| {
            Ok(ChunkRecord {
                id: row.get(0)?,
                file_id: row.get(1)?,
                sequence: row.get::<_, i64>(2)? as u32,
                size: row.get::<_, i64>(3)? as u32,
                compressed_size: row.get::<_, i64>(4)? as u32,
                hash: row.get(5)?,
                nonce: row.get(6)?,
                telegram_message_id: row.get(7)?,
                uploaded_at: row.get(8)?,
            })
        })?;

        let mut chunks = Vec::new();
        for chunk in chunk_iter {
            chunks.push(chunk?);
        }
        Ok(chunks)
    }

    pub fn get_file_by_id(&self, file_id: &str) -> Result<Option<FileRecord>> {
        let mut stmt = self.conn.prepare(
            "SELECT id, path, size, hash, compressed_size, chunk_count, created_at, updated_at
             FROM files WHERE id = ?1"
        )?;

        let file = stmt.query_row(params![file_id], |row| {
            Ok(FileRecord {
                id: row.get(0)?,
                path: row.get(1)?,
                size: row.get::<_, i64>(2)? as u64,
                hash: row.get(3)?,
                compressed_size: row.get::<_, i64>(4)? as u64,
                chunk_count: row.get::<_, i64>(5)? as u32,
                created_at: row.get(6)?,
                updated_at: row.get(7)?,
            })
        });

        match file {
            Ok(f) => Ok(Some(f)),
            Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
            Err(e) => Err(e.into()),
        }
    }

    pub fn update_file(&self, file: &FileRecord) -> Result<()> {
        self.conn.execute(
            "UPDATE files SET path = ?2, size = ?3, hash = ?4, compressed_size = ?5, 
             chunk_count = ?6, updated_at = ?7 WHERE id = ?1",
            params![
                file.id,
                file.path,
                file.size as i64,
                file.hash,
                file.compressed_size as i64,
                file.chunk_count as i64,
                file.updated_at
            ],
        )?;
        Ok(())
    }

    pub fn get_storage_stats(&self) -> Result<(u32, u64, u32)> {
        let mut stmt = self.conn.prepare(
            "SELECT COUNT(*) as file_count, COALESCE(SUM(size), 0) as total_size 
             FROM files"
        )?;

        let (file_count, total_size) = stmt.query_row([], |row| {
            Ok((
                row.get::<_, i64>(0)? as u32,
                row.get::<_, i64>(1)? as u64,
            ))
        })?;

        let mut stmt = self.conn.prepare(
            "SELECT COUNT(*) FROM chunks WHERE telegram_message_id IS NULL"
        )?;

        let pending_chunks = stmt.query_row([], |row| {
            Ok(row.get::<_, i64>(0)? as u32)
        })?;

        Ok((file_count, total_size, pending_chunks))
    }

    // Multi-channel support methods
    pub fn store_file_location(&self, file_id: &str, channel_id: i64, message_id: i64, telegram_file_id: &str, is_primary: bool) -> Result<()> {
        let upload_time = chrono::Utc::now().timestamp();
        
        self.conn.execute(
            "INSERT INTO file_locations (file_id, channel_id, message_id, chunk_index, is_primary, upload_time, verified, telegram_file_id)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![
                file_id,
                channel_id,
                message_id,
                None::<i32>, // chunk_index
                is_primary,
                upload_time,
                true, // verified
                telegram_file_id
            ],
        )?;

        Ok(())
    }

    pub fn get_file_locations(&self, file_id: &str) -> Result<Vec<(i64, i64, String, bool)>> {
        let mut stmt = self.conn.prepare(
            "SELECT channel_id, message_id, telegram_file_id, is_primary
             FROM file_locations WHERE file_id = ?1 ORDER BY is_primary DESC, upload_time DESC"
        )?;

        let location_iter = stmt.query_map([file_id], |row| {
            Ok((
                row.get::<_, i64>(0)?,     // channel_id
                row.get::<_, i64>(1)?,     // message_id
                row.get::<_, String>(2)?,  // telegram_file_id
                row.get::<_, bool>(3)?,    // is_primary
            ))
        })?;

        let mut locations = Vec::new();
        for location in location_iter {
            locations.push(location?);
        }

        Ok(locations)
    }

    pub fn remove_file_location(&self, channel_id: i64, message_id: i64) -> Result<()> {
        self.conn.execute(
            "DELETE FROM file_locations WHERE channel_id = ?1 AND message_id = ?2",
            params![channel_id, message_id],
        )?;

        Ok(())
    }

    pub fn update_channel_health(&self, channel_id: i64, is_healthy: bool, response_time: u64, error: Option<&str>) -> Result<()> {
        let current_time = chrono::Utc::now().timestamp();
        
        // Try to update existing record
        let updated = self.conn.execute(
            "UPDATE channel_health SET 
             is_healthy = ?1, 
             last_check = ?2, 
             response_time = ?3,
             error_count = CASE WHEN ?1 THEN error_count ELSE error_count + 1 END,
             success_count = CASE WHEN ?1 THEN success_count + 1 ELSE success_count END,
             last_error = ?4
             WHERE channel_id = ?5",
            params![is_healthy, current_time, response_time as i64, error, channel_id],
        )?;

        // If no record was updated, insert a new one
        if updated == 0 {
            self.conn.execute(
                "INSERT INTO channel_health (channel_id, is_healthy, last_check, response_time, error_count, success_count, last_error)
                 VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
                params![
                    channel_id, 
                    is_healthy, 
                    current_time, 
                    response_time as i64,
                    if is_healthy { 0 } else { 1 },
                    if is_healthy { 1 } else { 0 },
                    error
                ],
            )?;
        }

        Ok(())
    }

    pub fn get_channel_health(&self, channel_id: i64) -> Result<Option<(bool, i64, u64, u32, u32, Option<String>)>> {
        let mut stmt = self.conn.prepare(
            "SELECT is_healthy, last_check, response_time, error_count, success_count, last_error
             FROM channel_health WHERE channel_id = ?1"
        )?;

        let health = stmt.query_row(params![channel_id], |row| {
            Ok((
                row.get::<_, bool>(0)?,           // is_healthy
                row.get::<_, i64>(1)?,            // last_check
                row.get::<_, i64>(2)? as u64,     // response_time
                row.get::<_, i64>(3)? as u32,     // error_count
                row.get::<_, i64>(4)? as u32,     // success_count
                row.get::<_, Option<String>>(5)?, // last_error
            ))
        });

        match health {
            Ok(h) => Ok(Some(h)),
            Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
            Err(e) => Err(e.into()),
        }
    }

    pub fn get_all_channel_health(&self) -> Result<Vec<(i64, bool, i64, u64, u32, u32, Option<String>)>> {
        let mut stmt = self.conn.prepare(
            "SELECT channel_id, is_healthy, last_check, response_time, error_count, success_count, last_error
             FROM channel_health ORDER BY channel_id"
        )?;

        let health_iter = stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,            // channel_id
                row.get::<_, bool>(1)?,           // is_healthy
                row.get::<_, i64>(2)?,            // last_check
                row.get::<_, i64>(3)? as u64,     // response_time
                row.get::<_, i64>(4)? as u32,     // error_count
                row.get::<_, i64>(5)? as u32,     // success_count
                row.get::<_, Option<String>>(6)?, // last_error
            ))
        })?;

        let mut health_records = Vec::new();
        for health in health_iter {
            health_records.push(health?);
        }

        Ok(health_records)
    }

    pub fn get_files_by_channel(&self, channel_id: i64) -> Result<Vec<String>> {
        let mut stmt = self.conn.prepare(
            "SELECT DISTINCT file_id FROM file_locations WHERE channel_id = ?1"
        )?;

        let file_iter = stmt.query_map([channel_id], |row| {
            Ok(row.get::<_, String>(0)?)
        })?;

        let mut file_ids = Vec::new();
        for file_id in file_iter {
            file_ids.push(file_id?);
        }

        Ok(file_ids)
    }
}