#!/usr/bin/env python3
"""
Test script to verify the desktop app encryption functionality
"""

import requests
import json
import time
import os

# Test configuration
BOT_TOKEN = "8269631844:AAGULg5zlyNTTjlf35WtqRjhI9cQ5NztRdA"
CHANNEL_ID = -1003876315930

def test_bot_connection():
    """Test if the bot is working"""
    print("🔍 Testing bot connection...")
    
    try:
        response = requests.get(f"https://api.telegram.org/bot{BOT_TOKEN}/getMe")
        if response.status_code == 200:
            data = response.json()
            if data.get('ok'):
                bot_info = data['result']
                print(f"✅ Bot connected: {bot_info['first_name']} (@{bot_info['username']})")
                return True
            else:
                print(f"❌ Bot API error: {data}")
                return False
        else:
            print(f"❌ HTTP error: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Connection error: {e}")
        return False

def get_recent_messages():
    """Get recent messages from the channel"""
    print(f"📥 Getting recent messages from channel {CHANNEL_ID}...")
    
    try:
        # Get updates to see recent messages
        response = requests.get(f"https://api.telegram.org/bot{BOT_TOKEN}/getUpdates")
        if response.status_code == 200:
            data = response.json()
            if data.get('ok'):
                updates = data['result']
                print(f"📊 Found {len(updates)} recent updates")
                
                # Look for channel messages
                channel_messages = []
                for update in updates:
                    if 'channel_post' in update:
                        msg = update['channel_post']
                        if msg.get('chat', {}).get('id') == CHANNEL_ID:
                            channel_messages.append(msg)
                
                print(f"📨 Found {len(channel_messages)} messages in target channel")
                
                # Show recent files
                for msg in channel_messages[-5:]:  # Last 5 messages
                    if 'document' in msg:
                        doc = msg['document']
                        caption = msg.get('caption', 'No caption')
                        print(f"📄 File: {doc.get('file_name', 'Unknown')} - {caption}")
                        
                        # Check if it's encrypted
                        if '🔐' in caption:
                            print(f"   🔐 ENCRYPTED FILE DETECTED!")
                        elif '⚠️' in caption and 'Unencrypted' in caption:
                            print(f"   ⚠️ Unencrypted file")
                
                return True
            else:
                print(f"❌ API error: {data}")
                return False
        else:
            print(f"❌ HTTP error: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Error getting messages: {e}")
        return False

def main():
    print("🔐 SecureCloud Encryption Test")
    print("=" * 40)
    
    # Test bot connection
    if not test_bot_connection():
        print("❌ Bot connection failed. Cannot proceed.")
        return
    
    print()
    
    # Get recent messages to check for encrypted files
    if not get_recent_messages():
        print("❌ Failed to get recent messages.")
        return
    
    print()
    print("✅ Test completed!")
    print()
    print("📋 Next steps:")
    print("1. Run the desktop app: dotnet run --project desktop-ui")
    print("2. Click 'Initialize' to connect to Telegram")
    print("3. Upload a file and check if it shows as encrypted (🔐)")
    print("4. Download the file to verify decryption works")
    print()
    print("🔍 Look for these indicators:")
    print("   🔐 = File is encrypted")
    print("   ⚠️ = File is not encrypted")

if __name__ == "__main__":
    main()