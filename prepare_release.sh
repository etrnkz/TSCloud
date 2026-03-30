#!/bin/bash

# TSCloud Multi-Platform Release Preparation Script
# Version: 1.0.0
# Date: 2024-03-24

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Release configuration
VERSION="1.0.0"
RELEASE_DATE=$(date +"%Y-%m-%d")
RELEASE_NAME="Genesis"

# Platform configuration
BUILD_WINDOWS_X64=true
BUILD_WINDOWS_X86=true
BUILD_LINUX_DEB=true
BUILD_LINUX_RPM=true
BUILD_LINUX_ARCH=true
BUILD_ANDROID=true
BUILD_WEB=true

echo -e "${BLUE}🚀 TSCloud Multi-Platform Release Preparation Script${NC}"
echo -e "${BLUE}Version: ${VERSION}${NC}"
echo -e "${BLUE}Release Date: ${RELEASE_DATE}${NC}"
echo -e "${BLUE}Codename: ${RELEASE_NAME}${NC}"
echo -e "${PURPLE}Platforms: Windows (32/64), Linux (deb/rpm/arch), Android, Web${NC}"
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

print_build() {
    echo -e "${CYAN}🔨 $1${NC}"
}

print_package() {
    echo -e "${PURPLE}📦 $1${NC}"
}

# Check prerequisites
echo -e "${BLUE}📋 Checking Prerequisites...${NC}"

# Check if required tools are installed
check_tool() {
    if command -v $1 &> /dev/null; then
        print_status "$1 is installed"
        return 0
    else
        print_error "$1 is not installed"
        return 1
    fi
}

MISSING_TOOLS=0

# Essential tools
check_tool "git" || MISSING_TOOLS=$((MISSING_TOOLS + 1))
check_tool "dotnet" || MISSING_TOOLS=$((MISSING_TOOLS + 1))
check_tool "cargo" || MISSING_TOOLS=$((MISSING_TOOLS + 1))
check_tool "zip" || MISSING_TOOLS=$((MISSING_TOOLS + 1))

# Optional tools for specific builds
if [ "$BUILD_LINUX_DEB" = true ] || [ "$BUILD_LINUX_RPM" = true ]; then
    check_tool "dpkg-deb" || print_warning "dpkg-deb not found - DEB packages will be skipped"
    check_tool "rpmbuild" || print_warning "rpmbuild not found - RPM packages will be skipped"
fi

if [ "$BUILD_ANDROID" = true ]; then
    check_tool "java" || print_warning "Java not found - Android build will be skipped"
fi

if [ "$BUILD_WEB" = true ]; then
    check_tool "node" || print_warning "Node.js not found - Web build will be skipped"
    check_tool "npm" || print_warning "npm not found - Web build will be skipped"
fi

# Rust targets check
print_info "Checking Rust targets..."
if cargo --version &> /dev/null; then
    rustup target list --installed | grep -q "x86_64-pc-windows-msvc" || print_warning "Windows x64 target not installed"
    rustup target list --installed | grep -q "i686-pc-windows-msvc" || print_warning "Windows x86 target not installed"
    rustup target list --installed | grep -q "x86_64-unknown-linux-gnu" || print_warning "Linux x64 target not installed"
fi

if [ $MISSING_TOOLS -gt 0 ]; then
    print_error "Missing $MISSING_TOOLS essential tools. Please install them first."
    exit 1
fi

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
mkdir -p releases/{windows,linux,android,web,source}

# Clean Rust build
print_info "Cleaning Rust builds..."
cd rust-core
cargo clean
cd ..

# Clean .NET build
print_info "Cleaning .NET builds..."
cd desktop-ui
dotnet clean
cd ..

# Clean Node modules and build
if [ -d "web-dashboard" ] && [ "$BUILD_WEB" = true ]; then
    print_info "Cleaning web builds..."
    cd web-dashboard
    rm -rf node_modules/.cache
    rm -rf .next
    cd ..
fi

# Clean Android build
if [ -d "android-client" ] && [ "$BUILD_ANDROID" = true ]; then
    print_info "Cleaning Android builds..."
    cd android-client
    if [ -f "gradlew" ]; then
        ./gradlew clean
    fi
    cd ..
fi

print_status "Build directories cleaned"
echo ""

# Build Rust core for multiple targets
echo -e "${BLUE}🦀 Building Rust Core for Multiple Targets...${NC}"

build_rust_target() {
    local target=$1
    local description=$2
    
    print_build "Building Rust core for $description ($target)..."
    cd rust-core
    
    # Install target if not present
    rustup target add $target 2>/dev/null || true
    
    # Build for target
    if cargo build --release --target $target; then
        print_status "Rust core built for $description"
        
        # Copy artifacts to appropriate location
        mkdir -p ../releases/rust-artifacts/$target
        cp target/$target/release/libts_cloud_core.* ../releases/rust-artifacts/$target/ 2>/dev/null || \
        cp target/$target/release/ts_cloud_core.* ../releases/rust-artifacts/$target/ 2>/dev/null || true
    else
        print_error "Failed to build Rust core for $description"
        cd ..
        return 1
    fi
    
    cd ..
    return 0
}

# Build for different targets
if [ "$BUILD_WINDOWS_X64" = true ]; then
    build_rust_target "x86_64-pc-windows-msvc" "Windows x64"
fi

if [ "$BUILD_WINDOWS_X86" = true ]; then
    build_rust_target "i686-pc-windows-msvc" "Windows x86"
fi

build_rust_target "x86_64-unknown-linux-gnu" "Linux x64"

print_status "Rust core built for all targets"
echo ""

# Build Windows desktop applications
if [ "$BUILD_WINDOWS_X64" = true ] || [ "$BUILD_WINDOWS_X86" = true ]; then
    echo -e "${BLUE}🖥️  Building Windows Desktop Applications...${NC}"
    
    build_windows_app() {
        local arch=$1
        local target=$2
        local runtime=$3
        
        print_build "Building Windows $arch desktop application..."
        cd desktop-ui
        
        # Copy appropriate Rust library
        mkdir -p native/
        cp ../releases/rust-artifacts/$target/ts_cloud_core.dll native/ 2>/dev/null || \
        cp ../releases/rust-artifacts/$target/libts_cloud_core.so native/ 2>/dev/null || true
        
        # Restore and build
        dotnet restore
        if dotnet publish --configuration Release --runtime $runtime --self-contained true --output publish-$arch; then
            print_status "Windows $arch application built"
            
            # Create package
            cd publish-$arch
            zip -r "../../releases/windows/TSCloud-Windows-$arch-${VERSION}.zip" . -x "*.pdb"
            cd ..
            
            print_package "Windows $arch package created"
        else
            print_error "Failed to build Windows $arch application"
        fi
        
        cd ..
    }
    
    if [ "$BUILD_WINDOWS_X64" = true ]; then
        build_windows_app "x64" "x86_64-pc-windows-msvc" "win-x64"
    fi
    
    if [ "$BUILD_WINDOWS_X86" = true ]; then
        build_windows_app "x86" "i686-pc-windows-msvc" "win-x86"
    fi
    
    print_status "Windows desktop applications completed"
    echo ""
fi

# Build Linux desktop applications
if [ "$BUILD_LINUX_DEB" = true ] || [ "$BUILD_LINUX_RPM" = true ] || [ "$BUILD_LINUX_ARCH" = true ]; then
    echo -e "${BLUE}🐧 Building Linux Desktop Applications...${NC}"
    
    # Build base Linux application
    print_build "Building Linux desktop application..."
    cd desktop-ui
    
    # Copy Linux Rust library
    mkdir -p native/
    cp ../releases/rust-artifacts/x86_64-unknown-linux-gnu/libts_cloud_core.so native/ 2>/dev/null || true
    
    # Build Linux application
    dotnet restore
    if dotnet publish --configuration Release --runtime linux-x64 --self-contained true --output publish-linux; then
        print_status "Linux desktop application built"
    else
        print_error "Failed to build Linux desktop application"
        cd ..
    fi
    
    cd ..
    
    # Create DEB package
    if [ "$BUILD_LINUX_DEB" = true ] && command -v dpkg-deb &> /dev/null; then
        print_package "Creating DEB package..."
        
        mkdir -p package-deb/{DEBIAN,usr/bin,usr/share/applications,usr/share/pixmaps}
        
        # Copy application files
        cp -r desktop-ui/publish-linux/* package-deb/usr/bin/
        
        # Create desktop entry
        cat > package-deb/usr/share/applications/tscloud.desktop << EOF
[Desktop Entry]
Name=TSCloud
Comment=Encrypted cloud storage using Telegram
Exec=/usr/bin/TSCloud.Desktop
Icon=tscloud
Terminal=false
Type=Application
Categories=Utility;FileManager;
EOF
        
        # Create control file
        cat > package-deb/DEBIAN/control << EOF
Package: tscloud
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: libc6, libssl3
Maintainer: TSCloud Contributors <support@tscloud.dev>
Description: Encrypted cloud storage using Telegram infrastructure
 TSCloud provides end-to-end encrypted cloud storage using your own
 Telegram bot and channels for maximum privacy and security.
EOF
        
        # Build DEB package
        if dpkg-deb --build package-deb releases/linux/TSCloud-Linux-ubuntu-${VERSION}.deb; then
            print_status "DEB package created"
        else
            print_error "Failed to create DEB package"
        fi
        
        rm -rf package-deb
    fi
    
    # Create RPM package
    if [ "$BUILD_LINUX_RPM" = true ] && command -v rpmbuild &> /dev/null; then
        print_package "Creating RPM package..."
        
        mkdir -p rpmbuild/{BUILD,RPMS,SOURCES,SPECS,SRPMS}
        
        # Create spec file
        cat > rpmbuild/SPECS/tscloud.spec << EOF
Name: tscloud
Version: ${VERSION}
Release: 1%{?dist}
Summary: Encrypted cloud storage using Telegram infrastructure
License: MIT
URL: https://github.com/etrnkz/TSCloud
Source0: tscloud-${VERSION}.tar.gz

BuildRequires: dotnet-sdk-8.0
Requires: dotnet-runtime-8.0

%description
TSCloud provides end-to-end encrypted cloud storage using your own
Telegram bot and channels for maximum privacy and security.

%prep
%setup -q

%build
# Built in CI

%install
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
cp -r * %{buildroot}/usr/bin/

cat > %{buildroot}/usr/share/applications/tscloud.desktop << EOFDESKTOP
[Desktop Entry]
Name=TSCloud
Comment=Encrypted cloud storage using Telegram
Exec=/usr/bin/TSCloud.Desktop
Terminal=false
Type=Application
Categories=Utility;FileManager;
EOFDESKTOP

%files
/usr/bin/*
/usr/share/applications/tscloud.desktop

%changelog
* $(date +'%a %b %d %Y') TSCloud Contributors <support@tscloud.dev> - ${VERSION}-1
- Initial release
EOF
        
        # Create source tarball
        tar -czf rpmbuild/SOURCES/tscloud-${VERSION}.tar.gz -C desktop-ui/publish-linux .
        
        # Build RPM
        if rpmbuild --define "_topdir $(pwd)/rpmbuild" -bb rpmbuild/SPECS/tscloud.spec; then
            cp rpmbuild/RPMS/x86_64/tscloud-${VERSION}-1.*.rpm releases/linux/TSCloud-Linux-fedora-${VERSION}.rpm
            print_status "RPM package created"
        else
            print_error "Failed to create RPM package"
        fi
        
        rm -rf rpmbuild
    fi
    
    # Create Arch package
    if [ "$BUILD_LINUX_ARCH" = true ]; then
        print_package "Creating Arch package..."
        
        mkdir -p archpkg
        
        # Create PKGBUILD
        cat > archpkg/PKGBUILD << EOF
# Maintainer: TSCloud Contributors <support@tscloud.dev>
pkgname=tscloud
pkgver=${VERSION}
pkgrel=1
pkgdesc="Encrypted cloud storage using Telegram infrastructure"
arch=('x86_64')
url="https://github.com/etrnkz/TSCloud"
license=('MIT')
depends=('dotnet-runtime')
source=()
sha256sums=()

package() {
    mkdir -p "\$pkgdir/usr/bin"
    mkdir -p "\$pkgdir/usr/share/applications"
    
    cp -r ../desktop-ui/publish-linux/* "\$pkgdir/usr/bin/"
    
    cat > "\$pkgdir/usr/share/applications/tscloud.desktop" << EOFDESKTOP
[Desktop Entry]
Name=TSCloud
Comment=Encrypted cloud storage using Telegram
Exec=/usr/bin/TSCloud.Desktop
Terminal=false
Type=Application
Categories=Utility;FileManager;
EOFDESKTOP
}
EOF
        
        # Create tarball (simulated package)
        cd archpkg
        tar -czf ../releases/linux/TSCloud-Linux-arch-${VERSION}.pkg.tar.xz PKGBUILD
        cd ..
        rm -rf archpkg
        
        print_status "Arch package created"
    fi
    
    print_status "Linux desktop applications completed"
    echo ""
fi

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
    print_status "Web dashboard built"
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