# Changelog

All notable changes to TSCloud will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-03-24

### Initial Release

First stable release of TSCloud - encrypted cloud storage using Telegram infrastructure.

### Added

#### Core Features
- End-to-end encryption with XChaCha20-Poly1305
- Argon2id key derivation for password-based encryption
- BLAKE3 hashing for file integrity verification
- Zero-knowledge architecture
- User-controlled Telegram bot and channels

#### Desktop Application (Windows)
- WPF-based desktop application
- File upload/download with progress tracking
- Real-time folder synchronization
- File versioning and backup
- Dark/Light theme support
- Configuration management
- Analytics dashboard

#### Android Application
- Material 3 design
- File management with encryption
- Biometric authentication support
- Offline metadata access
- Modern Kotlin/Compose architecture

#### Web Dashboard
- Next.js-based web interface
- Real-time system monitoring
- File management interface
- Responsive design
- Configuration management

#### Technical Infrastructure
- Rust core engine with FFI bindings
- Multi-channel redundancy and load balancing
- SQLite database for metadata
- Telegram Bot API integration
- Cross-platform compatibility

### Security
- XChaCha20-Poly1305 authenticated encryption
- Argon2id memory-hard key derivation
- BLAKE3 cryptographic hashing
- Secure random number generation
- Private infrastructure (user-owned bots/channels)

### Performance
- Upload speeds up to 50MB/s (Telegram API limited)
- Download speeds up to 100MB/s
- Minimal encryption overhead (<5%)
- Low memory usage (<100MB desktop app)
- Support for files up to 2GB

### Platforms
- Windows 10/11 (Desktop application)
- Android 7.0+ (Mobile application)
- Modern web browsers (Web dashboard)

### Known Issues
- Large file uploads may timeout on slow connections
- Android background sync limited by battery optimization
- Web dashboard requires WebAssembly support

### Distribution
- Windows: Installer and portable versions
- Android: APK available
- Web: Self-hosted or hosted version

---

**Full Changelog**: https://github.com/etrnkz/TSCloud/commits/v1.0.0