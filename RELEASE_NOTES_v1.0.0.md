# TSCloud v1.0.0 Release Notes

**Release Date**: March 24, 2024  
**Version**: 1.0.0  

## Overview

TSCloud v1.0.0 is the initial stable release of an encrypted cloud storage system that uses Telegram as infrastructure. It provides end-to-end encryption with user-controlled bots and channels for maximum privacy and security.

## Key Features

### Security
- XChaCha20-Poly1305 authenticated encryption
- Argon2id key derivation from passwords
- BLAKE3 cryptographic hashing for integrity
- Zero-knowledge architecture
- User-controlled infrastructure (own bots and channels)

### Multi-Platform Support
- **Windows Desktop**: WPF application with full feature set
- **Android Mobile**: Material 3 design with Kotlin/Compose
- **Web Dashboard**: Next.js interface for management and monitoring

### Core Functionality
- File upload/download with encryption
- Real-time folder synchronization
- File versioning and backup
- Multi-channel redundancy
- Performance analytics and monitoring
- Dark/Light theme support

## Technical Specifications

### Architecture
- **Rust Core**: Cryptographic engine with FFI bindings
- **Desktop**: C# WPF application
- **Mobile**: Kotlin with Android Jetpack Compose
- **Web**: TypeScript with Next.js framework
- **Database**: SQLite for local metadata storage

### Performance
- Upload speeds: Up to 50MB/s (Telegram API limited)
- Download speeds: Up to 100MB/s
- Encryption overhead: Less than 5%
- Memory usage: Under 100MB for desktop application
- File size limit: 2GB per file (Telegram limitation)

### System Requirements

#### Windows Desktop
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- 4GB RAM minimum
- 100MB storage space

#### Android Mobile
- Android 7.0+ (API 24)
- 2GB RAM minimum
- 50MB storage space

#### Web Dashboard
- Modern browser with WebAssembly support
- Chrome 90+, Firefox 88+, Safari 14+

## Setup Process

1. **Create Telegram Bot**
   - Message @BotFather on Telegram
   - Create bot and obtain token
   - Create 1-3 private channels for storage
   - Add bot as admin to channels

2. **Install TSCloud**
   - Download appropriate version for your platform
   - Run installer or extract portable version
   - Launch application

3. **Configure**
   - Enter bot token and channel IDs
   - Test connectivity
   - Set master encryption password
   - Begin using the system

## Security Model

### Encryption Pipeline
1. File read from local storage
2. BLAKE3 hash calculation for integrity
3. XChaCha20-Poly1305 encryption with random nonce
4. Secure upload to Telegram channels
5. Encrypted metadata storage locally

### Privacy Protection
- No telemetry or tracking
- Local-only configuration storage
- Private channel requirements
- Keys never leave user devices
- No shared infrastructure

## Known Issues

- Large file uploads may timeout on slow connections (use chunked uploads)
- Android background sync may be limited by battery optimization
- Web dashboard requires modern browser with WebAssembly support

## Building from Source

```bash
git clone https://github.com/etrnkz/TSCloud.git
cd TSCloud

# Build Rust core
cd rust-core && cargo build --release

# Build desktop app
cd ../desktop-ui && dotnet build --configuration Release

# Build web dashboard
cd ../web-dashboard && npm install && npm run build

# Build Android app
cd ../android-client && ./gradlew assembleRelease
```

## License

TSCloud is released under the MIT License. See LICENSE file for details.

## Support

- **Issues**: Report bugs on GitHub Issues
- **Documentation**: Available in docs/ directory
- **Security**: Report security issues to security@tscloud.dev

## Acknowledgments

- Telegram for providing the infrastructure platform
- Rust cryptography community for excellent libraries
- Open source contributors and maintainers

---

**Download TSCloud v1.0.0 from the GitHub Releases page**