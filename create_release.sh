#!/bin/bash

# TSCloud Release Creation Script
# This script creates a GitHub release using the workflow

set -e

VERSION="v1.0.0"
RELEASE_NAME="Genesis"

echo "🚀 Creating TSCloud Release $VERSION"
echo "=================================="

# Check if we're in a git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo "❌ Not in a git repository"
    exit 1
fi

# Check if we have uncommitted changes
if ! git diff-index --quiet HEAD --; then
    echo "⚠️  You have uncommitted changes. Please commit or stash them first."
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Create and push tag
echo "📝 Creating git tag $VERSION..."
if git tag -a "$VERSION" -m "TSCloud $VERSION - $RELEASE_NAME"; then
    echo "✅ Tag created successfully"
else
    echo "⚠️  Tag might already exist, continuing..."
fi

echo "📤 Pushing tag to GitHub..."
if git push origin "$VERSION"; then
    echo "✅ Tag pushed successfully"
else
    echo "❌ Failed to push tag"
    exit 1
fi

echo ""
echo "🎉 Release process started!"
echo ""
echo "The GitHub Actions workflow will now:"
echo "1. Build Rust core for multiple targets"
echo "2. Build Windows desktop apps (32-bit & 64-bit)"
echo "3. Build Linux packages (deb, rpm, arch)"
echo "4. Build Android APK"
echo "5. Build web dashboard"
echo "6. Create comprehensive release with all assets"
echo ""
echo "📊 You can monitor the progress at:"
echo "https://github.com/etrnkz/TSCloud/actions"
echo ""
echo "🔗 Once complete, the release will be available at:"
echo "https://github.com/etrnkz/TSCloud/releases/tag/$VERSION"
echo ""
echo "⏱️  Expected completion time: 15-20 minutes"