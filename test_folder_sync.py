#!/usr/bin/env python3
"""
Test script to demonstrate TSCloud folder syncing functionality
"""

import os
import time
import tempfile
import shutil
from pathlib import Path

def create_test_folder_structure():
    """Create a test folder structure for syncing"""
    print("🗂️ Creating test folder structure...")
    
    # Create temporary directory
    test_dir = Path(tempfile.mkdtemp(prefix="TSCloud_Test_"))
    print(f"📁 Test directory: {test_dir}")
    
    # Create subdirectories
    (test_dir / "documents").mkdir()
    (test_dir / "images").mkdir()
    (test_dir / "code").mkdir()
    
    # Create test files
    test_files = [
        ("documents/readme.txt", "This is a test document for TSCloud folder sync."),
        ("documents/notes.md", "# Test Notes\n\nThis file will be automatically synced."),
        ("images/test.txt", "Placeholder for image file"),
        ("code/main.py", "print('Hello from TSCloud auto-sync!')"),
        ("code/config.json", '{"app": "TSCloud", "version": "1.0"}'),
    ]
    
    for file_path, content in test_files:
        full_path = test_dir / file_path
        full_path.write_text(content, encoding='utf-8')
        print(f"📄 Created: {file_path}")
    
    return test_dir

def simulate_file_changes(test_dir):
    """Simulate file changes that should trigger auto-sync"""
    print("\n🔄 Simulating file changes...")
    
    changes = [
        ("documents/new_file.txt", "This is a new file that should auto-sync."),
        ("code/update.py", "# This file was added after folder sync started\nprint('Auto-sync test')"),
        ("documents/readme.txt", "This is an UPDATED test document for TSCloud folder sync."),
    ]
    
    for file_path, content in changes:
        full_path = test_dir / file_path
        full_path.write_text(content, encoding='utf-8')
        print(f"✏️ Modified/Created: {file_path}")
        time.sleep(2)  # Give time for file system watcher to detect

def cleanup_test_folder(test_dir):
    """Clean up the test folder"""
    try:
        shutil.rmtree(test_dir)
        print(f"🗑️ Cleaned up test directory: {test_dir}")
    except Exception as e:
        print(f"❌ Error cleaning up: {e}")

def main():
    print("🔄 TSCloud Folder Sync Test")
    print("=" * 40)
    
    # Create test folder
    test_dir = create_test_folder_structure()
    
    print(f"\n📋 Test Instructions:")
    print(f"1. Run the TSCloud desktop app: dotnet run --project desktop-ui")
    print(f"2. Click 'Initialize' to connect to Telegram")
    print(f"3. Click 'Add Folder' and select: {test_dir}")
    print(f"4. Watch the activity log for folder sync messages")
    print(f"5. Press Enter here to simulate file changes...")
    
    input("\nPress Enter to continue...")
    
    # Simulate changes
    simulate_file_changes(test_dir)
    
    print(f"\n✅ File changes simulated!")
    print(f"📊 Check the desktop app for:")
    print(f"   - 🔄 Auto-sync messages in activity log")
    print(f"   - Updated file counts in folder sync tab")
    print(f"   - New files uploaded to Telegram with 🔄🔐 indicators")
    
    print(f"\n🔍 Expected behavior:")
    print(f"   - New files should auto-upload within seconds")
    print(f"   - Modified files should trigger re-upload")
    print(f"   - All files should be encrypted (🔐 indicator)")
    print(f"   - Activity log should show auto-sync messages")
    
    input("\nPress Enter to clean up test folder...")
    cleanup_test_folder(test_dir)
    
    print("\n🎉 Folder sync test completed!")
    print("\n📋 What to verify:")
    print("   ✅ Folder appears in 'Folder Sync' tab with 🟢 status")
    print("   ✅ File count and size are calculated correctly")
    print("   ✅ New/modified files trigger automatic uploads")
    print("   ✅ Auto-synced files show 🔄🔐 indicators in Telegram")
    print("   ✅ Activity log shows detailed sync information")
    print("   ✅ Folder can be paused/resumed/removed")

if __name__ == "__main__":
    main()