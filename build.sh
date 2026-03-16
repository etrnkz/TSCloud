#!/bin/bash

# SecureCloud Build Script
set -e

echo "Building SecureCloud - Encrypted Cloud Storage System"
echo "=================================================="

# Build Rust core
echo "Building Rust core engine..."
cd rust-core
cargo build --release
cargo test
cd ..

# Copy generated header and library for C# project
echo "Copying native libraries for C# project..."
mkdir -p desktop-ui/native
cp rust-core/target/release/secure_cloud_core.dll desktop-ui/native/ 2>/dev/null || \
cp rust-core/target/release/libsecure_cloud_core.so desktop-ui/native/ 2>/dev/null || \
cp rust-core/target/release/libsecure_cloud_core.dylib desktop-ui/native/ 2>/dev/null || true

cp rust-core/secure_cloud_core.h desktop-ui/native/

# Build C# Desktop Application
echo "Building C# desktop application..."
cd desktop-ui
dotnet restore
dotnet build --configuration Release
cd ..

# Build Android Application
echo "Building Android application..."
cd android-client
./gradlew assembleDebug
cd ..

echo ""
echo "Build completed successfully!"
echo ""
echo "Outputs:"
echo "- Rust core: rust-core/target/release/"
echo "- C# Desktop: desktop-ui/bin/Release/"
echo "- Android APK: android-client/app/build/outputs/apk/debug/"
echo ""
echo "Next steps:"
echo "1. Configure Telegram API credentials"
echo "2. Set up private Telegram channel"
echo "3. Run the desktop application or install Android APK"