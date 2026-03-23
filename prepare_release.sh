#!/bin/bash

# TSCloud Release Preparation Script
# Version: 1.0.0
# Date: 2024-03-21

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Release configuration
VERSION="1.0.0"
RELEASE_DATE=$(date +"%Y-%m-%d")
RELEASE_NAME="Genesis"

echo -e "${BLUE}🚀 TSCloud Release Preparation Script${NC}"
echo -e "${BLUE}Version: ${VERSION}${NC}"
echo -e "${BLUE}Release Date: ${RELEASE_DATE}${NC}"
echo -e "${BLUE}Codename: ${RELEASE_NAME}${NC}"
echo ""

# Function to print status
print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Check prerequisites
echo -e "${BLUE}📋 Checking Prerequisites...${NC}"

# Check if required tools are installed
check_tool() {
    if command -v $1 &> /dev/null; then
        print_status "$1 is installed"
    else
        print_error "$1 is not installed"
        exit 1
    fi
}

check_tool "git"
check_tool "dotnet"
check_tool "cargo"
check_tool "node"
check_tool "zip"

# Check if we're in the right directory
if [ ! -f "README.md" ] || [ ! -d "rust-core" ] || [ ! -d "desktop-ui" ]; then
    print_error "Please run this script from the TSCloud root directory"
    exit 1
fi

print_status "All prerequisites met"
echo ""

# Clean previous builds
echo -e "${BLUE}🧹 Cleaning Previous Builds...${NC}"
rm -rf build/
rm -rf dist/
rm -rf releases/
mkdir -p releases

# Clean Rust build
cd rust-core
cargo clean
cd ..

# Clean .NET build
cd desktop-ui
dotnet clean
cd ..

# Clean Node modules and build
if [ -d "web-dashboard" ]; then
    cd web-dashboard
    rm -rf node_modules/.cache
    rm -rf .next
    cd ..
fi

print_status "Build directories cleaned"
echo ""

# Build Rust core
echo -e "${BLUE}🦀 Building Rust Core...${NC}"
cd rust-core
cargo build --release
cargo test --release
print_status "Rust core built and tested"
cd ..

# Build desktop application
echo -e "${BLUE}🖥️  Building Desktop Application...${NC}"
cd desktop-ui
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal
print_status "Desktop application built and tested"
cd ..

# Build Android application
if [ -d "android-client" ]; then
    echo -e "${BLUE}📱 Building Android Application...${NC}"
    cd android-client
    ./gradlew clean
    ./gradlew assembleRelease
    print_status "Android application built"
    cd ..
fi

# Build web dashboard
if [ -d "web-dashboard" ]; then
    echo -e "${BLUE}🌐 Building Web Dashboard...${NC}"
    cd web-dashboard
    npm install
    npm run build
    npm run test
    print_status "Web dashboard built and tested"
    cd ..
fi

# Create release packages
echo -e "${BLUE}📦 Creating Release Packages...${NC}"

# Create Windows installer
echo -e "${BLUE}Creating Windows installer...${NC}"
cd desktop-ui/bin/Release/net8.0-windows
zip -r "../../../../releases/TSCloud-Portable-${VERSION}.zip" . -x "*.pdb" "*.xml"
cd ../../../..

# Copy Android APK if it exists
if [ -f "android-client/app/build/outputs/apk/release/app-release.apk" ]; then
    cp "android-client/app/build/outputs/apk/release/app-release.apk" "releases/TSCloud-${VERSION}.apk"
    print_status "Android APK copied"
fi

# Create web dashboard package
if [ -d "web-dashboard/.next" ]; then
    cd web-dashboard
    zip -r "../releases/TSCloud-Web-${VERSION}.zip" .next public package.json
    cd ..
    print_status "Web dashboard package created"
fi

# Create source code archive
git archive --format=zip --prefix="TSCloud-${VERSION}/" HEAD > "releases/TSCloud-Source-${VERSION}.zip"
print_status "Source code archive created"

# Generate checksums
echo -e "${BLUE}🔐 Generating Checksums...${NC}"
cd releases
for file in *.zip *.apk; do
    if [ -f "$file" ]; then
        sha256sum "$file" >> "TSCloud-${VERSION}-checksums.txt"
    fi
done
cd ..
print_status "Checksums generated"

# Create release documentation
echo -e "${BLUE}📄 Creating Release Documentation...${NC}"

# Update version in files
sed -i "s/Version: .*/Version: ${VERSION}/" README.md
sed -i "s/Release Date: .*/Release Date: ${RELEASE_DATE}/" RELEASE_NOTES_v${VERSION}.md

# Create installation guide
cat > "releases/INSTALLATION_GUIDE.md" << EOF
# TSCloud v${VERSION} Installation Guide

## Windows Desktop Application

### Option 1: Portable Version (Recommended)
1. Download \`TSCloud-Portable-${VERSION}.zip\`
2. Extract to your preferred location
3. Run \`TSCloud.Desktop.exe\`
4. Follow the setup wizard

### System Requirements
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (will be installed automatically)
- 4GB RAM minimum
- 100MB free disk space

## Android Mobile Application

### Installation
1. Download \`TSCloud-${VERSION}.apk\`
2. Enable "Install from unknown sources" in Android settings
3. Install the APK file
4. Grant necessary permissions

### System Requirements
- Android 7.0+ (API 24)
- 2GB RAM minimum
- 50MB free storage space

## Web Dashboard

### Access
- Visit: https://dashboard.TSCloud.dev
- Or self-host using \`TSCloud-Web-${VERSION}.zip\`

### Browser Requirements
- Chrome 90+ / Firefox 88+ / Safari 14+
- WebAssembly support required

## First-Time Setup

1. **Create Telegram Bot**
   - Message @BotFather on Telegram
   - Use /newbot command
   - Save the bot token

2. **Create Storage Channels**
   - Create 2-3 private channels
   - Add your bot as admin
   - Get channel IDs using @userinfobot

3. **Configure TSCloud**
   - Launch the application
   - Enter bot token and channel IDs
   - Test connectivity
   - Set encryption password

4. **Start Using**
   - Upload your first file
   - Set up folder synchronization
   - Explore advanced features

## Support

For help with installation:
- Check documentation in /docs folder
- Visit GitHub Issues
- Join our Discord community

EOF

print_status "Installation guide created"

# Create quick start guide
cat > "releases/QUICK_START.md" << EOF
# TSCloud v${VERSION} Quick Start Guide

## 🚀 Get Started in 5 Minutes

### Step 1: Download & Install
- **Windows**: Download and extract \`TSCloud-Portable-${VERSION}.zip\`
- **Android**: Install \`TSCloud-${VERSION}.apk\`
- **Web**: Visit https://dashboard.TSCloud.dev

### Step 2: Create Your Telegram Bot
1. Open Telegram and message [@BotFather](https://t.me/BotFather)
2. Send \`/newbot\` command
3. Choose a name: "My TSCloud Bot"
4. Choose a username: "myTSCloud_bot"
5. **Save the bot token** - you'll need it!

### Step 3: Create Storage Channels
1. In Telegram, create a new channel
2. Make it **Private** (very important!)
3. Add your bot as admin with "Post Messages" permission
4. Get the channel ID by forwarding a message to [@userinfobot](https://t.me/userinfobot)
5. Repeat for 2-3 channels for redundancy

### Step 4: Configure TSCloud
1. Launch TSCloud
2. Setup wizard will appear automatically
3. Enter your bot token
4. Add your channel IDs
5. Test the connection
6. Set your encryption password
7. Done! 🎉

### Step 5: Upload Your First File
1. Click "Add File" or drag & drop
2. Select a file to upload
3. Watch it encrypt and upload securely
4. Your file is now stored encrypted in your private channels!

## 🔐 Security Tips

- **Use a strong encryption password** - this protects all your files
- **Keep your bot token secret** - never share it with anyone
- **Use private channels only** - public channels are not secure
- **Create multiple channels** - for redundancy and reliability

## 🆘 Need Help?

- **Documentation**: Check the /docs folder
- **Issues**: Report problems on GitHub
- **Community**: Join our Discord server
- **Security**: Email security@TSCloud.dev

## 🎯 What's Next?

- Set up **folder synchronization** for automatic backups
- Explore **file versioning** to track changes
- Try the **mobile app** for on-the-go access
- Check out **analytics** to monitor your usage

**Welcome to TSCloud - Your files, your bot, your security!** 🔐
EOF

print_status "Quick start guide created"

# Generate release summary
echo -e "${BLUE}📊 Generating Release Summary...${NC}"

TOTAL_SIZE=$(du -sh releases/ | cut -f1)
FILE_COUNT=$(ls -1 releases/ | wc -l)

cat > "releases/RELEASE_SUMMARY.md" << EOF
# TSCloud v${VERSION} Release Summary

**Release Date**: ${RELEASE_DATE}  
**Codename**: ${RELEASE_NAME}  
**Total Package Size**: ${TOTAL_SIZE}  
**Files**: ${FILE_COUNT}

## 📦 Release Packages

$(ls -la releases/ | grep -v "^total" | grep -v "^d" | awk '{print "- **" $9 "** (" $5 " bytes)"}')

## 🔐 Security Verification

### Checksums (SHA256)
\`\`\`
$(cat releases/TSCloud-${VERSION}-checksums.txt)
\`\`\`

### Code Signing
- **Windows**: Authenticode signed (when available)
- **Android**: Release keystore signed
- **Source**: Git tag signed with GPG

## 🚀 What's New in v${VERSION}

This is the initial release of TSCloud featuring:

- **Military-grade encryption** with XChaCha20-Poly1305
- **User-controlled infrastructure** with private Telegram bots
- **Multi-platform support** (Windows, Android, Web)
- **Real-time folder synchronization**
- **File versioning** with automatic backup
- **Professional UI** with dark/light themes
- **Analytics dashboard** with performance monitoring

## 📋 Installation

See \`INSTALLATION_GUIDE.md\` for detailed installation instructions.

## 🆘 Support

- **Documentation**: /docs folder in source
- **Issues**: GitHub Issues
- **Community**: Discord server
- **Security**: security@TSCloud.dev

## 🔮 Next Release

Version 1.1 is planned for Q2 2024 with:
- Team collaboration features
- Advanced file sharing
- Plugin system
- Enhanced mobile capabilities

---

**Thank you for using TSCloud!** 🙏
EOF

print_status "Release summary generated"

# Final verification
echo -e "${BLUE}🔍 Final Verification...${NC}"

# Check if all expected files exist
EXPECTED_FILES=(
    "releases/TSCloud-Portable-${VERSION}.zip"
    "releases/TSCloud-Source-${VERSION}.zip"
    "releases/TSCloud-${VERSION}-checksums.txt"
    "releases/INSTALLATION_GUIDE.md"
    "releases/QUICK_START.md"
    "releases/RELEASE_SUMMARY.md"
)

for file in "${EXPECTED_FILES[@]}"; do
    if [ -f "$file" ]; then
        print_status "$(basename "$file") created"
    else
        print_warning "$(basename "$file") missing"
    fi
done

# Check optional files
if [ -f "releases/TSCloud-${VERSION}.apk" ]; then
    print_status "Android APK included"
fi

if [ -f "releases/TSCloud-Web-${VERSION}.zip" ]; then
    print_status "Web dashboard included"
fi

echo ""
echo -e "${GREEN}🎉 Release v${VERSION} prepared successfully!${NC}"
echo ""
echo -e "${BLUE}📁 Release files are in the 'releases/' directory${NC}"
echo -e "${BLUE}📊 Total size: ${TOTAL_SIZE}${NC}"
echo -e "${BLUE}📦 Files: ${FILE_COUNT}${NC}"
echo ""

# Next steps
echo -e "${YELLOW}📋 Next Steps:${NC}"
echo -e "${YELLOW}1. Review all files in releases/ directory${NC}"
echo -e "${YELLOW}2. Test installation on clean systems${NC}"
echo -e "${YELLOW}3. Create GitHub release with these files${NC}"
echo -e "${YELLOW}4. Update website and documentation${NC}"
echo -e "${YELLOW}5. Announce release to community${NC}"
echo ""

# GitHub release command suggestion
echo -e "${BLUE}💡 GitHub Release Command:${NC}"
echo "gh release create v${VERSION} releases/* --title \"TSCloud v${VERSION} - ${RELEASE_NAME}\" --notes-file RELEASE_NOTES_v${VERSION}.md"
echo ""

print_status "Release preparation complete! 🚀"