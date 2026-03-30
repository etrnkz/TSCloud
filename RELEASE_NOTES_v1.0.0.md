# TSCloud v1.0.0 — Genesis

First public release of TSCloud: **zero-knowledge** encrypted storage on **your** Telegram bot and private channels.

## Highlights

- **Rust core**: XChaCha20-Poly1305, Argon2id, BLAKE3; chunking and compression before upload
- **Windows desktop**: WPF app with native FFI to the Rust library
- **Android**: Kotlin / Jetpack Compose client (API 26+)
- **Web dashboard**: Next.js management UI (self-hostable)

## Install

- **Windows**: Download `TSCloud-Windows-x64-v1.0.0.zip` (or x86) from this release, extract, run `TSCloud.Desktop.exe`
- **Android**: Install `TSCloud-Android-v1.0.0.apk` (enable install from unknown sources if needed)
- **Web**: Use `TSCloud-Web-Dashboard-v1.0.0.tar.gz` and follow `INSTALLATION_GUIDE.md` in the release assets

Verify downloads with `TSCloud-v1.0.0-checksums.txt`.

## Setup (all platforms)

1. Create a bot with [@BotFather](https://t.me/BotFather) and keep the token secret  
2. Create **private** channels, add the bot as admin  
3. Configure TSCloud with the token and channel IDs, then set your **master password**

## Docs

- [README](README.md) — overview and build commands  
- [CHANGELOG](CHANGELOG.md) — full 1.0.0 notes  
- [docs/](docs/) — setup, security, architecture  

## Security

Lost master password means **lost data** by design. Use strong passwords and secure devices.
