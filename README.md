# TSCloud

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)]()
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android%20%7C%20Web-blue.svg)]()

Encrypted cloud storage system using Telegram as infrastructure with zero-knowledge architecture.

### Features

- **End-to-end encryption** with XChaCha20-Poly1305
- **User-controlled infrastructure** - your own Telegram bot and channels
- **Multi-platform support** - Windows, Android, Web
- **File versioning** and automatic backup
- **Real-time folder synchronization**
- **Multi-channel redundancy** for reliability

### Architecture

- **Rust Core**: Cryptographic engine and core logic
- **Desktop App**: Windows WPF application
- **Android App**: Native Kotlin mobile application  
- **Web Dashboard**: Next.js management interface

All platforms use the same Rust core for encryption and Telegram integration.

### Quick Start

#### Prerequisites

- Telegram account
- Windows 10/11 (for desktop app)
- Android 7.0+ (for mobile app)

#### Setup

1. **Create Telegram Bot**
   - Message [@BotFather](https://t.me/BotFather)
   - Create a new bot and save the token
   - Create 1-3 private channels for storage
   - Add your bot as admin to each channel

2. **Install TSCloud**
   ```bash
   git clone https://github.com/etrnkz/TSCloud.git
   cd TSCloud
   ./build.sh
   ```

3. **Configure**
   - Launch the application
   - Enter your bot token and channel IDs
   - Set a master password for encryption
   - Test the connection

### Configuration

Basic configuration in `config.json`:

```json
{
  "telegram": {
    "bot_token": "your_bot_token",
    "channels": [
      {
        "id": -1001234567890,
        "name": "Primary Storage",
        "priority": 1,
        "active": true
      }
    ]
  },
  "security": {
    "chunk_size": 16777216,
    "compression_level": 3
  }
}
```

### Security

- **XChaCha20-Poly1305**: Authenticated encryption
- **Argon2id**: Key derivation from password
- **BLAKE3**: File integrity verification
- **Zero-knowledge**: Files encrypted before upload

### Building

#### Requirements

- .NET 8.0 SDK
- Rust 1.70+
- Node.js 18+ (for web dashboard)
- Android Studio (for mobile app)

#### Build Commands

```bash
# Rust core
cd rust-core && cargo build --release

# Desktop app
cd desktop-ui && dotnet build --configuration Release

# Web dashboard
cd web-dashboard && npm install && npm run build

# Android app
cd android-client && ./gradlew assembleRelease
```

## Support

- **Issues**: [GitHub Issues](https://github.com/etrnkz/TSCloud/issues)
- **Documentation**: [docs/](docs/)


## Acknowledgments

- Telegram for providing the infrastructure
- Rust crypto community for excellent libraries
- Open source contributors and maintainers