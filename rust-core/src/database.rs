use crate::{Result, SecureCloudError, crypto::MasterKey};
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

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_chunks_file_id ON chunks (file_id)",
            [],
        )?;

        self.conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_chunks_sequence ON chunks (file_id, sequence)",
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
}