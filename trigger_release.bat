@echo off
setlocal enabledelayedexpansion

REM TSCloud Manual Release Trigger Script (Windows)
REM Use this if you need to manually trigger the release workflow

set VERSION=v1.0.0

echo.
echo 🔄 Manually triggering TSCloud release workflow...
echo Version: %VERSION%
echo.

REM Check if GitHub CLI is available
where gh >nul 2>nul
if %errorlevel% equ 0 (
    echo 📡 Using GitHub CLI to trigger workflow...
    gh workflow run release.yml --ref main -f version="%VERSION%"
    
    if %errorlevel% equ 0 (
        echo ✅ Workflow triggered successfully!
        echo.
        echo 📊 Monitor progress at:
        echo https://github.com/etrnkz/TSCloud/actions
        echo.
        echo 🔗 Release will be available at:
        echo https://github.com/etrnkz/TSCloud/releases/tag/%VERSION%
    ) else (
        echo ❌ Failed to trigger workflow
        echo Please check your GitHub CLI authentication
    )
) else (
    echo ⚠️  GitHub CLI not found
    echo.
    echo To manually trigger the workflow:
    echo 1. Go to https://github.com/etrnkz/TSCloud/actions
    echo 2. Click on 'Release' workflow
    echo 3. Click 'Run workflow'
    echo 4. Enter version: %VERSION%
    echo 5. Click 'Run workflow' button
    echo.
    echo Or install GitHub CLI:
    echo https://cli.github.com/
)

echo.
echo ℹ️  The workflow should also run automatically when the tag was pushed.
echo Check the Actions tab to see if it's already running.

pause