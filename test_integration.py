#!/usr/bin/env python3
"""
SecureCloud Integration Test
Tests the core functionality without requiring full UI setup
"""

import os
import sys
import subprocess
import tempfile
import json
from pathlib import Path

def test_rust_core():
    """Test that Rust core builds and tests pass"""
    print("🧪 Testing Rust Core...")
    
    # Change to rust-core directory
    rust_dir = Path("rust-core")
    if not rust_dir.exists():
        print("❌ rust-core directory not found")
        return False
    
    # Run tests
    result = subprocess.run(
        ["cargo", "test", "--release"],
        cwd=rust_dir,
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        print("✅ Rust core tests passed")
        return True
    else:
        print(f"❌ Rust core tests failed: {result.stderr}")
        return False

def test_library_generation():
    """Test that the native library is generated"""
    print("🔧 Testing Library Generation...")
    
    # Check if library exists
    lib_path = Path("rust-core/target/release/secure_cloud_core.dll")
    if not lib_path.exists():
        # Try building
        result = subprocess.run(
            ["cargo", "build", "--release"],
            cwd="rust-core",
            capture_output=True,
            text=True
        )
        
        if result.returncode != 0:
            print(f"❌ Failed to build library: {result.stderr}")
            return False
    
    if lib_path.exists():
        print(f"✅ Native library generated: {lib_path}")
        print(f"   Size: {lib_path.stat().st_size} bytes")
        return True
    else:
        print("❌ Native library not found")
        return False

def test_desktop_build():
    """Test that desktop application builds"""
    print("🖥️  Testing Desktop Build...")
    
    desktop_dir = Path("desktop-ui")
    if not desktop_dir.exists():
        print("❌ desktop-ui directory not found")
        return False
    
    # Try building
    result = subprocess.run(
        ["dotnet", "build"],
        cwd=desktop_dir,
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        print("✅ Desktop application built successfully")
        return True
    else:
        print(f"❌ Desktop build failed: {result.stderr}")
        return False

def test_crypto_functionality():
    """Test basic cryptographic functionality"""
    print("🔐 Testing Crypto Functionality...")
    
    # This would ideally call into the Rust library directly
    # For now, we'll just verify the tests pass
    result = subprocess.run(
        ["cargo", "test", "crypto::", "--release"],
        cwd="rust-core",
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        print("✅ Cryptographic tests passed")
        return True
    else:
        print(f"❌ Crypto tests failed: {result.stderr}")
        return False

def test_database_functionality():
    """Test database operations"""
    print("🗄️  Testing Database Functionality...")
    
    result = subprocess.run(
        ["cargo", "test", "database::", "--release"],
        cwd="rust-core",
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        print("✅ Database tests passed")
        return True
    else:
        print(f"❌ Database tests failed: {result.stderr}")
        return False

def main():
    """Run all integration tests"""
    print("🚀 SecureCloud Integration Test Suite")
    print("=" * 50)
    
    tests = [
        ("Rust Core Tests", test_rust_core),
        ("Library Generation", test_library_generation),
        ("Desktop Build", test_desktop_build),
        ("Crypto Functionality", test_crypto_functionality),
        ("Database Functionality", test_database_functionality),
    ]
    
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        print(f"\n📋 Running: {test_name}")
        try:
            if test_func():
                passed += 1
            else:
                print(f"❌ {test_name} failed")
        except Exception as e:
            print(f"❌ {test_name} failed with exception: {e}")
    
    print("\n" + "=" * 50)
    print(f"📊 Test Results: {passed}/{total} tests passed")
    
    if passed == total:
        print("🎉 All tests passed! SecureCloud is working correctly.")
        return True
    else:
        print("⚠️  Some tests failed. Check the output above.")
        return False

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)