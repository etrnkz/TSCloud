# TSCloud Architecture

## System Overview

TSCloud implements a zero-knowledge encrypted cloud storage system using Telegram as the storage backend. The architecture ensures that all encryption happens locally, with Telegram only storing encrypted blobs that cannot be decrypted without the user's master password.

## Core Components

### 1. Rust Core Engine (`rust-core/`)

The high-performance cryptographic engine written in Rust provides:

- **Cryptographic Operations**: XChaCha20-Poly1305 encryption, Argon2id key derivation, BLAKE3 hashing
- **File Processing**: Zstandard compression, chunking, streaming operations
- **Telegram Integration**: MTProto/Bot API communication, parallel transfers
- **Database Management**: SQLite metadata storage with encryption
- **C ABI**: Foreign Function Interface for cross-language integration

#### Key Modules:
```
rust-core/src/
├── crypto.rs          # Cryptographic primitives
├── storage.rs         # Storage engine and file watching
├── telegram.rs        # Telegram client implementation
├── file_processor.rs  # File compression and chunking
├── database.rs        # SQLite metadata management
└── ffi.rs            # C ABI for desktop integration
```

### 2. Desktop Application (`desktop-ui/`)

Cross-platform desktop client built with C# and WPF:

- **User Interface**: File management, sync status, configuration
- **Native Integration**: P/Invoke calls to Rust core via C ABI
- **File Watching**: Automatic sync on file system changes
- **Key Management**: Secure password handling and key derivation

#### Architecture:
```
desktop-ui/
├── Services/          # Business logic and Rust integration
├── ViewModels/        # MVVM pattern implementation
├── Models/           # Data transfer objects
├── Views/            # WPF user interface
└── NativeMethods.cs  # P/Invoke declarations
```

### 3. Android Client (`android-client/`)

Native Android application using Kotlin and Jetpack Compose:

- **Mobile UI**: Material Design 3 interface
- **Secure Storage**: Android Keystore integration
- **Background Sync**: WorkManager for periodic updates
- **Cryptography**: BouncyCastle for crypto operations

#### Architecture:
```
android-client/app/src/main/java/com/TSCloud/android/
├── crypto/           # Cryptographic operations
├── data/            # Database and repository layer
├── security/        # Android Keystore management
├── ui/              # Jetpack Compose UI
└── services/        # Background services
```

## Data Flow Architecture

### Upload Process (Desktop → Telegram)

```
File Change Detection
        ↓
File Reading & Hashing
        ↓
Zstandard Compression
        ↓
Chunking (8-32MB pieces)
        ↓
Per-Chunk Encryption (XChaCha20-Poly1305)
        ↓
Metadata Storage (SQLite)
        ↓
Parallel Upload to Telegram
        ↓
Chunk Mapping Update
```

### Download Process (Telegram → Android)

```
Metadata Sync from Desktop
        ↓
Chunk Download from Telegram
        ↓
Hash Verification (BLAKE3)
        ↓
Per-Chunk Decryption
        ↓
Chunk Reassembly
        ↓
Zstandard Decompression
        ↓
File Hash Verification
        ↓
Local File Storage
```

## Security Architecture

### Zero-Knowledge Design

1. **Local Key Derivation**:
   ```
   User Password + Salt → Argon2id → Master Key (256-bit)
   ```

2. **Per-Chunk Encryption**:
   ```
   Chunk Data + Master Key + Random Nonce → XChaCha20-Poly1305 → Encrypted Chunk
   ```

3. **Integrity Protection**:
   ```
   Original Data → BLAKE3 → Hash (256-bit)
   Encrypted Chunk → BLAKE3 → Chunk Hash (256-bit)
   ```

### Key Management

- **Desktop**: Master key encrypted with user profile protection
- **Android**: Master key encrypted with Android Keystore (hardware-backed)
- **Cross-Device**: Same password derives identical keys via deterministic salt

### Metadata Protection

All sensitive metadata is encrypted locally:
- Original filenames and paths
- Directory structure
- File sizes and timestamps
- Chunk mappings and sequence

## Database Schema

### Local SQLite Database

```sql
-- File metadata (encrypted)
CREATE TABLE files (
    id TEXT PRIMARY KEY,           -- UUID
    path TEXT NOT NULL,            -- Original file path
    size INTEGER NOT NULL,         -- Original file size
    hash BLOB NOT NULL,            -- BLAKE3 hash of original file
    compressed_size INTEGER,       -- Size after compression
    chunk_count INTEGER,           -- Number of chunks
    created_at INTEGER,            -- Unix timestamp
    updated_at INTEGER             -- Unix timestamp
);

-- Chunk metadata
CREATE TABLE chunks (
    id TEXT PRIMARY KEY,           -- UUID
    file_id TEXT NOT NULL,         -- Foreign key to files
    sequence INTEGER NOT NULL,     -- Chunk order (0-based)
    size INTEGER NOT NULL,         -- Encrypted chunk size
    hash BLOB NOT NULL,            -- BLAKE3 hash of encrypted chunk
    nonce BLOB NOT NULL,           -- XChaCha20 nonce (24 bytes)
    telegram_message_id INTEGER,   -- Telegram message ID
    uploaded_at INTEGER,           -- Upload timestamp
    FOREIGN KEY (file_id) REFERENCES files (id)
);
```

## Telegram Integration

### Storage Strategy

- **Channel as Storage**: Private Telegram channel stores encrypted chunks
- **Message Mapping**: Each chunk stored as a document in a message
- **Metadata Separation**: File structure never exposed to Telegram
- **Rate Limiting**: Respects Telegram API limits (20 requests/minute)

### Upload Protocol

1. **Chunk Preparation**: Encrypt and hash chunk locally
2. **Document Upload**: Send encrypted chunk as document to channel
3. **Message ID Storage**: Map chunk ID to Telegram message ID
4. **Integrity Verification**: Verify upload success and chunk integrity

### Download Protocol

1. **Message Retrieval**: Get message by stored message ID
2. **Document Download**: Download encrypted chunk from Telegram
3. **Hash Verification**: Verify chunk integrity before decryption
4. **Decryption**: Decrypt chunk with stored nonce and master key

## Performance Optimizations

### Streaming Architecture

- **Memory Efficiency**: Process files without loading entirely into memory
- **Parallel Processing**: Concurrent chunk encryption and upload
- **Resumable Transfers**: Resume interrupted uploads/downloads
- **Compression First**: Reduce data size before encryption

### Caching Strategy

- **Metadata Caching**: Local SQLite database for fast access
- **Chunk Deduplication**: Avoid re-uploading identical chunks
- **Progressive Download**: Download chunks on-demand
- **Background Sync**: Periodic metadata synchronization

## Cross-Platform Compatibility

### Shared Components

- **Cryptographic Parameters**: Identical across all platforms
- **Chunk Format**: Standardized binary format
- **Metadata Schema**: Version-controlled database schema
- **Key Derivation**: Same Argon2id parameters

### Platform-Specific Adaptations

- **Desktop**: File system watching, system tray integration
- **Android**: Background services, notification management
- **iOS** (Future): App Store compliance, background app refresh

## Scalability Considerations

### File Size Limits

- **Telegram**: 2GB per file (handled via chunking)
- **Chunks**: 8-32MB optimal size for performance
- **Database**: SQLite handles millions of records efficiently
- **Memory**: Streaming processing for large files

### Concurrent Operations

- **Upload Parallelism**: 3-5 concurrent chunk uploads
- **Download Parallelism**: Configurable based on network
- **Database Connections**: Connection pooling for high throughput
- **Rate Limiting**: Adaptive throttling for Telegram API

## Disaster Recovery

### Data Redundancy

- **Local Metadata**: Backed up with file chunks
- **Telegram Storage**: Persistent cloud storage
- **Key Recovery**: Master password is the single point of recovery
- **Cross-Device Sync**: Multiple devices can access same data

### Recovery Procedures

1. **Password Recovery**: No password recovery (by design)
2. **Metadata Reconstruction**: Rebuild from Telegram if local DB lost
3. **Partial Recovery**: Individual files can be recovered independently
4. **Migration Support**: Export/import for account migration