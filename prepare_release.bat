@echo off
setlocal enabledelayedexpansion

REM TSCloud Release Preparation Script (Windows)
REM Version: 1.0.0
REM Date: 2024-03-21

set VERSION=1.0.0
set RELEASE_NAME=Genesis

echo.
echo 🚀 TSCloud Release Preparation Script
echo Version: %VERSION%
echo Codename: %RELEASE_NAME%
echo.

REM Check prerequisites
echo 📋 Checking Prerequisites...

where git >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ Git is not installed
    exit /b 1
)
echo ✅ Git is installed

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ .NET is not installed
    exit /b 1
)
echo ✅ .NET is installed

where cargo >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ Rust/Cargo is not installed
    exit /b 1
)
echo ✅ Rust/Cargo is installed

REM Check if we're in the right directory
if not exist "README.md" (
    echo ❌ Please run this script from the TSCloud root directory
    exit /b 1
)
if not exist "rust-core" (
    echo ❌ rust-core directory not found
    exit /b 1
)
if not exist "desktop-ui" (
    echo ❌ desktop-ui directory not found
    exit /b 1
)

echo ✅ All prerequisites met
echo.

REM Clean previous builds
echo 🧹 Cleaning Previous Builds...
if exist "build" rmdir /s /q "build"
if exist "dist" rmdir /s /q "dist"
if exist "releases" rmdir /s /q "releases"
mkdir releases

REM Clean Rust build
cd rust-core
cargo clean
cd ..

REM Clean .NET build
cd desktop-ui
dotnet clean
cd ..

echo ✅ Build directories cleaned
echo.

REM Build Rust core
echo 🦀 Building Rust Core...
cd rust-core
cargo build --release
if %errorlevel% neq 0 (
    echo ❌ Rust build failed
    exit /b 1
)
cargo test --release
if %errorlevel% neq 0 (
    echo ❌ Rust tests failed
    exit /b 1
)
echo ✅ Rust core built and tested
cd ..

REM Build desktop application
echo 🖥️ Building Desktop Application...
cd desktop-ui
dotnet restore
if %errorlevel% neq 0 (
    echo ❌ .NET restore failed
    exit /b 1
)
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ❌ .NET build failed
    exit /b 1
)
echo ✅ Desktop application built
cd ..

REM Create release packages
echo 📦 Creating Release Packages...

REM Create Windows portable package
echo Creating Windows portable package...
cd desktop-ui\bin\Release\net8.0-windows
powershell -command "Compress-Archive -Path * -DestinationPath '..\..\..\..\releases\TSCloud-Portable-%VERSION%.zip' -Force"
cd ..\..\..\..

REM Create source code archive
echo Creating source code archive...
git archive --format=zip --prefix=TSCloud-%VERSION%/ HEAD > releases\TSCloud-Source-%VERSION%.zip

echo ✅ Release packages created
echo.

REM Generate checksums
echo 🔐 Generating Checksums...
cd releases
for %%f in (*.zip *.apk) do (
    if exist "%%f" (
        powershell -command "Get-FileHash '%%f' -Algorithm SHA256 | Select-Object Hash,Path | Format-Table -HideTableHeaders" >> TSCloud-%VERSION%-checksums.txt
    )
)
cd ..
echo ✅ Checksums generated
echo.

REM Create installation guide
echo 📄 Creating Release Documentation...
(
echo # TSCloud v%VERSION% Installation Guide
echo.
echo ## Windows Desktop Application
echo.
echo ### Portable Version ^(Recommended^)
echo 1. Download `TSCloud-Portable-%VERSION%.zip`
echo 2. Extract to your preferred location
echo 3. Run `TSCloud.Desktop.exe`
echo 4. Follow the setup wizard
echo.
echo ### System Requirements
echo - Windows 10/11 ^(64-bit^)
echo - .NET 8.0 Runtime ^(will be installed automatically^)
echo - 4GB RAM minimum
echo - 100MB free disk space
echo.
echo ## First-Time Setup
echo.
echo 1. **Create Telegram Bot**
echo    - Message @BotFather on Telegram
echo    - Use /newbot command
echo    - Save the bot token
echo.
echo 2. **Create Storage Channels**
echo    - Create 2-3 private channels
echo    - Add your bot as admin
echo    - Get channel IDs using @userinfobot
echo.
echo 3. **Configure TSCloud**
echo    - Launch the application
echo    - Enter bot token and channel IDs
echo    - Test connectivity
echo    - Set encryption password
echo.
echo 4. **Start Using**
echo    - Upload your first file
echo    - Set up folder synchronization
echo    - Explore advanced features
) > releases\INSTALLATION_GUIDE.md

REM Create quick start guide
(
echo # TSCloud v%VERSION% Quick Start Guide
echo.
echo ## 🚀 Get Started in 5 Minutes
echo.
echo ### Step 1: Download ^& Install
echo - **Windows**: Download and extract `TSCloud-Portable-%VERSION%.zip`
echo.
echo ### Step 2: Create Your Telegram Bot
echo 1. Open Telegram and message [@BotFather]^(https://t.me/BotFather^)
echo 2. Send `/newbot` command
echo 3. Choose a name: "My TSCloud Bot"
echo 4. Choose a username: "myTSCloud_bot"
echo 5. **Save the bot token** - you'll need it!
echo.
echo ### Step 3: Create Storage Channels
echo 1. In Telegram, create a new channel
echo 2. Make it **Private** ^(very important!^)
echo 3. Add your bot as admin with "Post Messages" permission
echo 4. Get the channel ID by forwarding a message to [@userinfobot]^(https://t.me/userinfobot^)
echo 5. Repeat for 2-3 channels for redundancy
echo.
echo ### Step 4: Configure TSCloud
echo 1. Launch TSCloud
echo 2. Setup wizard will appear automatically
echo 3. Enter your bot token
echo 4. Add your channel IDs
echo 5. Test the connection
echo 6. Set your encryption password
echo 7. Done! 🎉
echo.
echo ### Step 5: Upload Your First File
echo 1. Click "Add File" or drag ^& drop
echo 2. Select a file to upload
echo 3. Watch it encrypt and upload securely
echo 4. Your file is now stored encrypted in your private channels!
echo.
echo ## 🔐 Security Tips
echo.
echo - **Use a strong encryption password** - this protects all your files
echo - **Keep your bot token secret** - never share it with anyone
echo - **Use private channels only** - public channels are not secure
echo - **Create multiple channels** - for redundancy and reliability
echo.
echo **Welcome to TSCloud - Your files, your bot, your security!** 🔐
) > releases\QUICK_START.md

echo ✅ Documentation created
echo.

REM Final summary
echo 🎉 Release v%VERSION% prepared successfully!
echo.
echo 📁 Release files are in the 'releases' directory
echo.
echo 📋 Next Steps:
echo 1. Review all files in releases/ directory
echo 2. Test installation on clean systems
echo 3. Create GitHub release with these files
echo 4. Update website and documentation
echo 5. Announce release to community
echo.
echo 💡 GitHub Release Command:
echo gh release create v%VERSION% releases/* --title "TSCloud v%VERSION% - %RELEASE_NAME%" --notes-file RELEASE_NOTES_v%VERSION%.md
echo.
echo ✅ Release preparation complete! 🚀

pause