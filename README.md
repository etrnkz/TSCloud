# SecureCloud - Encrypted Cloud Storage via Telegram

A secure, cross-platform encrypted cloud storage system using Telegram as the storage backend with zero-knowledge architecture.

## Architecture Overview

- **Rust Core Engine**: High-performance cryptographic operations, file processing, and Telegram integration
- **C# Desktop UI**: WPF/Avalonia interface for Windows/Linux/macOS
- **Android Client**: Kotlin + Jetpack Compose mobile application
- **Zero-Knowledge**: All encryption happens locally, keys never leave device

## Key Features

- XChaCha20-Poly1305 encryption with Argon2id key derivation
- Zstandard compression before encryption
- File chunking (8-32MB) for efficient transfers
- BLAKE3 integrity verification
- Resumable uploads/downloads
- Parallel transfers with streaming
- Local SQLite metadata database (encrypted)
- Cross-platform key compatibility

## Security Model

1. User password → Argon2id → Master key
2. File content → Zstd compress → Chunk → Encrypt → Upload to Telegram
3. Metadata (filenames, structure) encrypted locally in SQLite
4. Telegram only sees encrypted blobs, cannot decrypt content

## Project Structure

```
SecureCloud/
├── rust-core/          # Rust engine with C ABI
├── desktop-ui/         # C# WPF/Avalonia application  
├── android-client/     # Kotlin Android application
├── shared-schemas/     # Common data structures
└── docs/              # Documentation
```