use crate::{Result, SecureCloudError, crypto::{ChunkEncryptor, hash_data}, database::{FileRecord, ChunkRecord}};
use std::fs::File;
use std::io::{Read, BufReader};
use std::path::Path;
use uuid::Uuid;
use zstd::stream::{Encoder, Decoder};
use std::io::Write;

pub struct FileProcessor {
    chunk_size: usize,
    compression_level: i32,
}

impl FileProcessor {
    pub fn new(chunk_size: usize, compression_level: i32) -> Self {
        Self {
            chunk_size,
            compression_level,
        }
    }

    pub fn process_file<P: AsRef<Path>>(
        &self,
        file_path: P,
        encryptor: &ChunkEncryptor,
    ) -> Result<(FileRecord, Vec<ChunkRecord>)> {
        let path = file_path.as_ref();
        let file = File::open(path)?;
        let file_size = file.metadata()?.len();
        let mut reader = BufReader::new(file);

        // Calculate file hash
        let mut file_data = Vec::new();
        reader.read_to_end(&mut file_data)?;
        let file_hash = hash_data(&file_data);

        // Compress the entire file
        let compressed_data = self.compress_data(&file_data)?;
        let compressed_size = compressed_data.len() as u64;

        // Split into chunks and encrypt
        let chunks = self.create_chunks(&compressed_data, encryptor)?;
        let chunk_count = chunks.len() as u32;

        let file_id = Uuid::new_v4().to_string();
        let now = chrono::Utc::now().timestamp();

        let file_record = FileRecord {
            id: file_id.clone(),
            path: path.to_string_lossy().to_string(),
            size: file_size,
            hash: file_hash.to_vec(),
            compressed_size,
            chunk_count,
            created_at: now,
            updated_at: now,
        };

        let chunk_records: Vec<ChunkRecord> = chunks
            .into_iter()
            .enumerate()
            .map(|(i, (encrypted_data, nonce))| {
                let chunk_hash = hash_data(&encrypted_data);
                ChunkRecord {
                    id: Uuid::new_v4().to_string(),
                    file_id: file_id.clone(),
                    sequence: i as u32,
                    size: encrypted_data.len() as u32,
                    compressed_size: encrypted_data.len() as u32, // Already compressed
                    hash: chunk_hash.to_vec(),
                    nonce: nonce.to_vec(),
                    telegram_message_id: None,
                    uploaded_at: None,
                }
            })
            .collect();

        Ok((file_record, chunk_records))
    }

    pub fn reconstruct_file(
        &self,
        chunks: &[ChunkRecord],
        encrypted_chunks: &[Vec<u8>],
        encryptor: &ChunkEncryptor,
        output_path: &Path,
    ) -> Result<()> {
        if chunks.len() != encrypted_chunks.len() {
            return Err(SecureCloudError::Crypto(
                "Chunk count mismatch".to_string()
            ));
        }

        // Decrypt and combine chunks
        let mut compressed_data = Vec::new();
        for (chunk, encrypted_data) in chunks.iter().zip(encrypted_chunks.iter()) {
            // Verify chunk hash
            let actual_hash = hash_data(encrypted_data);
            if actual_hash.as_slice() != chunk.hash {
                return Err(SecureCloudError::Crypto(
                    format!("Chunk {} hash verification failed", chunk.sequence)
                ));
            }

            // Decrypt chunk
            let nonce: [u8; 24] = chunk.nonce.as_slice().try_into()
                .map_err(|_| SecureCloudError::Crypto("Invalid nonce size".to_string()))?;
            
            let decrypted_chunk = encryptor.decrypt_chunk(encrypted_data, &nonce)?;
            compressed_data.extend_from_slice(&decrypted_chunk);
        }

        // Decompress
        let decompressed_data = self.decompress_data(&compressed_data)?;

        // Write to output file
        std::fs::write(output_path, decompressed_data)?;

        Ok(())
    }

    fn compress_data(&self, data: &[u8]) -> Result<Vec<u8>> {
        let mut compressed = Vec::new();
        {
            let mut encoder = Encoder::new(&mut compressed, self.compression_level)
                .map_err(|e| SecureCloudError::Crypto(format!("Compression error: {}", e)))?;
            encoder.write_all(data)
                .map_err(|e| SecureCloudError::Crypto(format!("Compression write error: {}", e)))?;
            encoder.finish()
                .map_err(|e| SecureCloudError::Crypto(format!("Compression finish error: {}", e)))?;
        }
        Ok(compressed)
    }

    fn decompress_data(&self, compressed_data: &[u8]) -> Result<Vec<u8>> {
        let mut decompressed = Vec::new();
        {
            let mut decoder = Decoder::new(compressed_data)
                .map_err(|e| SecureCloudError::Crypto(format!("Decompression error: {}", e)))?;
            decoder.read_to_end(&mut decompressed)
                .map_err(|e| SecureCloudError::Crypto(format!("Decompression read error: {}", e)))?;
        }
        Ok(decompressed)
    }

    fn create_chunks(
        &self,
        data: &[u8],
        encryptor: &ChunkEncryptor,
    ) -> Result<Vec<(Vec<u8>, [u8; 24])>> {
        let mut chunks = Vec::new();
        let mut offset = 0;

        while offset < data.len() {
            let end = std::cmp::min(offset + self.chunk_size, data.len());
            let chunk_data = &data[offset..end];
            
            let (encrypted_chunk, nonce) = encryptor.encrypt_chunk(chunk_data)?;
            chunks.push((encrypted_chunk, nonce));
            
            offset = end;
        }

        Ok(chunks)
    }
}

pub struct StreamingProcessor {
    chunk_size: usize,
    compression_level: i32,
}

impl StreamingProcessor {
    pub fn new(chunk_size: usize, compression_level: i32) -> Self {
        Self {
            chunk_size,
            compression_level,
        }
    }

    pub fn process_file_streaming<P: AsRef<Path>>(
        &self,
        file_path: P,
        encryptor: &ChunkEncryptor,
        mut chunk_callback: impl FnMut(usize, Vec<u8>, [u8; 24]) -> Result<()>,
    ) -> Result<FileRecord> {
        let path = file_path.as_ref();
        let file = File::open(path)?;
        let file_size = file.metadata()?.len();
        let mut reader = BufReader::new(file);

        let file_id = Uuid::new_v4().to_string();
        let now = chrono::Utc::now().timestamp();

        // For streaming, we'll need to process in chunks
        let mut buffer = vec![0u8; self.chunk_size];
        let mut chunk_index = 0;
        let mut total_compressed_size = 0u64;

        // Calculate file hash (we need to read the file twice for this approach)
        let file_data = std::fs::read(path)?;
        let file_hash = hash_data(&file_data);

        // Process file in streaming chunks
        let mut reader = BufReader::new(File::open(path)?);
        loop {
            let bytes_read = reader.read(&mut buffer)?;
            if bytes_read == 0 {
                break;
            }

            let chunk_data = &buffer[..bytes_read];
            let compressed_chunk = self.compress_data(chunk_data)?;
            let (encrypted_chunk, nonce) = encryptor.encrypt_chunk(&compressed_chunk)?;
            
            total_compressed_size += encrypted_chunk.len() as u64;
            chunk_callback(chunk_index, encrypted_chunk, nonce)?;
            chunk_index += 1;
        }

        Ok(FileRecord {
            id: file_id,
            path: path.to_string_lossy().to_string(),
            size: file_size,
            hash: file_hash.to_vec(),
            compressed_size: total_compressed_size,
            chunk_count: chunk_index as u32,
            created_at: now,
            updated_at: now,
        })
    }
}