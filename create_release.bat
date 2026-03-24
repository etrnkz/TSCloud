@echo off
setlocal enabledelayedexpansion

REM TSCloud Release Creation Script (Windows)
REM This script creates a GitHub release using the workflow

set VERSION=v1.0.0
set RELEASE_NAME=Genesis

echo.
echo 🚀 Creating TSCloud Release %VERSION%
echo ==================================
echo.

REM Check if we're in a git repository
git rev-parse --git-dir >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ Not in a git repository
    exit /b 1
)

REM Check if we have uncommitted changes
git diff-index --quiet HEAD --
if %errorlevel% neq 0 (
    echo ⚠️  You have uncommitted changes. Please commit or stash them first.
    set /p "continue=Continue anyway? (y/N): "
    if /i not "!continue!"=="y" (
        exit /b 1
    )
)

REM Create and push tag
echo 📝 Creating git tag %VERSION%...
git tag -a "%VERSION%" -m "TSCloud %VERSION% - %RELEASE_NAME%"
if %errorlevel% equ 0 (
    echo ✅ Tag created successfully
) else (
    echo ⚠️  Tag might already exist, continuing...
)

echo 📤 Pushing tag to GitHub...
git push origin "%VERSION%"
if %errorlevel% equ 0 (
    echo ✅ Tag pushed successfully
) else (
    echo ❌ Failed to push tag
    exit /b 1
)

echo.
echo 🎉 Release process started!
echo.
echo The GitHub Actions workflow will now:
echo 1. Build Rust core for multiple targets
echo 2. Build Windows desktop apps (32-bit ^& 64-bit)
echo 3. Build Linux packages (deb, rpm, arch)
echo 4. Build Android APK
echo 5. Build web dashboard
echo 6. Create comprehensive release with all assets
echo.
echo 📊 You can monitor the progress at:
echo https://github.com/etrnkz/TSCloud/actions
echo.
echo 🔗 Once complete, the release will be available at:
echo https://github.com/etrnkz/TSCloud/releases/tag/%VERSION%
echo.
echo ⏱️  Expected completion time: 15-20 minutes

pause