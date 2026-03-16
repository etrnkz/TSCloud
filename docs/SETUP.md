# SecureCloud Setup Guide

## Prerequisites

### Development Environment
- **Rust**: Latest stable version (1.70+)
- **.NET**: .NET 8.0 SDK
- **Android Studio**: Latest version with Android SDK 26+
- **Git**: For version control

### Telegram Setup
1. Create a Telegram Bot:
   - Message @BotFather on Telegram
   - Create a new bot with `/newbot`
   - Save the bot token

2. Create a Private Channel:
   - Create a new private channel
   - Add your bot as an administrator
   - Get the channel ID (use @userinfobot)

3. Get Telegram API Credentials:
   - Visit https://my.telegram.org/apps
   - Create a new application
   - Save API ID and API Hash

## Building the Project

### 1. Clone and Build Rust Core
```bash
git clone <repository-url>
cd SecureCloud
chmod +x build.sh
./build.sh
```

### 2. Configure Telegram Credentials

Create `config.json` in the project root:
```json
{
  "telegram": {
    "api_id": 12345,
    "api_hash": "your_api_hash_here",
    "bot_token": "your_bot_token_here",
    "channel_id": -1001234567890
  },
  "security": {
    "chunk_size": 16777216,
    "compression_level": 3
  }
}
```

### 3. Desktop Application Setup

```bash
cd desktop-ui
dotnet restore
dotnet run
```

On first run:
1. Enter your master password
2. Configure Telegram settings
3. Select folders to watch

### 4. Android Application Setup

```bash
cd android-client
./gradlew assembleDebug
adb install app/build/outputs/apk/debug/app-debug.apk
```

On first run:
1. Grant storage permissions
2. Enter the same master password as desktop
3. Sync files from Telegram

## Security Considerations

### Key Management
- Master password never leaves your device
- Keys are derived using Argon2id with high memory cost
- Android uses Keystore for secure key storage
- Desktop stores encrypted keys in user profile

### File Processing Pipeline
1. **Compression**: Zstandard level 3 (configurable)
2. **Chunking**: 8-32MB pieces (16MB default)
3. **Encryption**: XChaCha20-Poly1305 per chunk
4. **Integrity**: BLAKE3 hashing for verification
5. **Upload**: Encrypted chunks to Telegram

### Zero-Knowledge Architecture
- Telegram only sees encrypted blobs
- Filenames and structure encrypted locally
- No plaintext metadata on Telegram servers
- Cross-device sync via encrypted metadata

## Usage

### Desktop Application
- **Add Files**: Drag & drop or use file picker
- **Watch Folders**: Automatic sync on file changes
- **Download**: Click download button for any file
- **Sync Status**: Monitor upload/download progress

### Android Application
- **Sync**: Pull files from Telegram channel
- **Download**: Decrypt and save files locally
- **View**: Browse encrypted file collection
- **Security**: Biometric unlock (optional)

## Troubleshooting

### Common Issues

**Build Errors**:
- Ensure Rust toolchain is up to date
- Check .NET SDK version compatibility
- Verify Android SDK and build tools

**Connection Issues**:
- Verify Telegram credentials
- Check network connectivity
- Ensure bot has channel permissions

**Encryption Errors**:
- Verify master password consistency
- Check file integrity hashes
- Ensure sufficient storage space

### Debug Mode

Enable debug logging:
```bash
# Rust core
RUST_LOG=debug cargo run

# C# desktop
dotnet run --configuration Debug

# Android
adb logcat | grep SecureCloud
```

## Performance Optimization

### Recommended Settings
- **Chunk Size**: 16MB for most use cases
- **Compression**: Level 3 (balance of speed/ratio)
- **Parallel Uploads**: 3-5 concurrent chunks
- **Memory Usage**: ~100MB per active transfer

### Hardware Requirements
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 2x file size for processing
- **Network**: Stable internet for Telegram API
- **CPU**: Multi-core for parallel processing

## Advanced Configuration

### Custom Chunk Sizes
```rust
// Rust core configuration
Config {
    chunk_size: 32 * 1024 * 1024, // 32MB chunks
    compression_level: 1,          // Faster compression
    // ...
}
```

### Telegram Rate Limiting
- Default: 20 requests/minute
- Large files: Use larger chunks
- Batch operations: Implement delays

### Cross-Platform Compatibility
- Key derivation parameters identical
- Chunk format standardized
- Metadata schema versioned
- Migration support built-in