using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace SecureCloud.Desktop
{
    // P/Invoke declarations for Rust crypto functions
    public static class NativeCrypto
    {
        private const string DllName = "secure_cloud_core";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sc_generate_salt(byte[] saltOut, uint saltLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sc_derive_key_from_password(
            [MarshalAs(UnmanagedType.LPStr)] string password,
            byte[] salt,
            uint saltLen,
            byte[] keyOut,
            uint keyLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sc_encrypt_data(
            byte[] data,
            uint dataLen,
            byte[] key,
            uint keyLen,
            byte[] nonceOut,
            uint nonceLen,
            byte[] encryptedOut,
            ref uint encryptedLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sc_decrypt_data(
            byte[] encryptedData,
            uint encryptedLen,
            byte[] key,
            uint keyLen,
            byte[] nonce,
            uint nonceLen,
            byte[] decryptedOut,
            ref uint decryptedLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sc_hash_data(
            byte[] data,
            uint dataLen,
            byte[] hashOut,
            uint hashLen);

        // Error codes
        public const int SC_SUCCESS = 0;
        public const int SC_ERROR_INVALID_PARAM = -1;
        public const int SC_ERROR_CRYPTO = -2;
    }

    public class CryptoManager
    {
        private byte[]? _masterKey;
        private byte[]? _salt;

        public bool IsInitialized => _masterKey != null && _salt != null;

        public bool InitializeWithPassword(string password)
        {
            try
            {
                // Generate salt
                _salt = new byte[32];
                var result = NativeCrypto.sc_generate_salt(_salt, 32);
                if (result != NativeCrypto.SC_SUCCESS)
                {
                    return false;
                }

                // Derive key from password
                _masterKey = new byte[32];
                result = NativeCrypto.sc_derive_key_from_password(password, _salt, 32, _masterKey, 32);
                return result == NativeCrypto.SC_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        public bool InitializeWithExistingSalt(string password, byte[] salt)
        {
            try
            {
                if (salt.Length != 32) return false;

                _salt = new byte[32];
                Array.Copy(salt, _salt, 32);

                _masterKey = new byte[32];
                var result = NativeCrypto.sc_derive_key_from_password(password, _salt, 32, _masterKey, 32);
                return result == NativeCrypto.SC_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        public byte[]? GetSalt() => _salt?.ToArray();

        public (byte[]? encryptedData, byte[]? nonce) EncryptData(byte[] data)
        {
            if (!IsInitialized || _masterKey == null) return (null, null);

            try
            {
                var nonce = new byte[24]; // XChaCha20 nonce size
                var encrypted = new byte[data.Length + 16]; // Data + Poly1305 tag
                uint encryptedLen = (uint)encrypted.Length;

                var result = NativeCrypto.sc_encrypt_data(
                    data, (uint)data.Length,
                    _masterKey, 32,
                    nonce, 24,
                    encrypted, ref encryptedLen);

                if (result == NativeCrypto.SC_SUCCESS)
                {
                    Array.Resize(ref encrypted, (int)encryptedLen);
                    return (encrypted, nonce);
                }
                return (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        public byte[]? DecryptData(byte[] encryptedData, byte[] nonce)
        {
            if (!IsInitialized || _masterKey == null) return null;

            try
            {
                var decrypted = new byte[encryptedData.Length]; // Max possible size
                uint decryptedLen = (uint)decrypted.Length;

                var result = NativeCrypto.sc_decrypt_data(
                    encryptedData, (uint)encryptedData.Length,
                    _masterKey, 32,
                    nonce, (uint)nonce.Length,
                    decrypted, ref decryptedLen);

                if (result == NativeCrypto.SC_SUCCESS)
                {
                    Array.Resize(ref decrypted, (int)decryptedLen);
                    return decrypted;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public byte[]? HashData(byte[] data)
        {
            try
            {
                var hash = new byte[32]; // BLAKE3 hash size
                var result = NativeCrypto.sc_hash_data(data, (uint)data.Length, hash, 32);
                return result == NativeCrypto.SC_SUCCESS ? hash : null;
            }
            catch
            {
                return null;
            }
        }
    }
}

namespace SecureCloud.Desktop
{
    public class FileItem : INotifyPropertyChanged
    {
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public long EncryptedSize { get; set; }
        public string SizeFormatted => FormatBytes(Size);
        public string EncryptedSizeFormatted => FormatBytes(EncryptedSize);
        public DateTime UploadedAt { get; set; }
        public long MessageId { get; set; }
        public string FileId { get; set; } = "";
        public string FilePath { get; set; } = "";
        public byte[] Nonce { get; set; } = Array.Empty<byte>();
        public byte[] FileHash { get; set; } = Array.Empty<byte>();
        public bool IsEncrypted => Nonce.Length > 0;

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class SimpleMainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _botToken = "8269631844:AAGULg5zlyNTTjlf35WtqRjhI9cQ5NztRdA";
        private readonly long _channelId = -1003876315930;
        private readonly ObservableCollection<FileItem> _files = new();
        private readonly CryptoManager _crypto = new();
        private bool _isConnected = false;
        private bool _isEncryptionEnabled = false;

        public SimpleMainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            
            // Set up data binding
            FilesDataGrid.ItemsSource = _files;
            
            // Update initial status
            UpdateStatus("Ready - SecureCloud with encryption");
            LogActivity("Application started");
            UpdateFileStats();
            
            // Prompt for encryption password
            PromptForEncryptionPassword();
        }

        private void PromptForEncryptionPassword()
        {
            var passwordDialog = new PasswordDialog();
            if (passwordDialog.ShowDialog() == true)
            {
                var password = passwordDialog.Password;
                if (!string.IsNullOrEmpty(password))
                {
                    if (_crypto.InitializeWithPassword(password))
                    {
                        _isEncryptionEnabled = true;
                        LogActivity("🔐 Encryption initialized with master password");
                        UpdateStatus("Ready - Encryption enabled");
                    }
                    else
                    {
                        LogActivity("❌ Failed to initialize encryption");
                        MessageBox.Show("Failed to initialize encryption. Files will be uploaded without encryption.", 
                            "Encryption Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else
            {
                LogActivity("⚠️ Encryption skipped - files will be uploaded without encryption");
                MessageBox.Show("Encryption disabled. Files will be uploaded without encryption.", 
                    "Encryption Disabled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("SecureCloud is working! 🎉\n\nYour Telegram bot is ready:\n@mtuconbot\nChannel: confess", 
                "SecureCloud Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Testing Telegram connection...");
                LogActivity("Testing bot connection...");
                
                // Test bot connection
                var response = await _httpClient.GetAsync($"https://api.telegram.org/bot{_botToken}/getMe");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(content);
                    
                    if (result?.ok == true)
                    {
                        var botName = result.result.first_name;
                        var username = result.result.username;
                        
                        _isConnected = true;
                        UpdateStatus($"✅ Connected to bot: {botName} (@{username})");
                        LogActivity($"✅ Connected to bot: {botName} (@{username})");
                        
                        // Update bot info
                        BotInfoText.Text = $"Bot: {botName} (@{username})";
                        ChannelInfoText.Text = $"Channel: confess ({_channelId})";
                        
                        // Try to load existing files from channel
                        await LoadExistingFilesAsync();
                        
                        MessageBox.Show($"Successfully connected to Telegram!\n\nBot: {botName} (@{username})\nChannel ID: {_channelId}", 
                            "Connection Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        _isConnected = false;
                        UpdateStatus("❌ Bot connection failed");
                        LogActivity("❌ Bot connection failed");
                        MessageBox.Show("Failed to connect to Telegram bot", "Connection Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UpdateStatus($"❌ Error: {ex.Message}");
                LogActivity($"❌ Connection error: {ex.Message}");
                MessageBox.Show($"Connection error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Please initialize the connection first by clicking 'Initialize'", 
                    "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Title = "Select File to Upload",
                Filter = "All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var fileName = Path.GetFileName(openFileDialog.FileName);
                    UpdateStatus($"Processing {fileName}...");
                    LogActivity($"📤 Processing {fileName}...");
                    
                    var originalData = await File.ReadAllBytesAsync(openFileDialog.FileName);
                    byte[] uploadData;
                    byte[]? nonce = null;
                    byte[]? fileHash = null;
                    string uploadCaption;

                    if (_isEncryptionEnabled && _crypto.IsInitialized)
                    {
                        // Encrypt the file
                        LogActivity($"🔐 Encrypting {fileName}...");
                        UpdateStatus($"Encrypting {fileName}...");
                        
                        var (encryptedData, encryptionNonce) = _crypto.EncryptData(originalData);
                        if (encryptedData != null && encryptionNonce != null)
                        {
                            uploadData = encryptedData;
                            nonce = encryptionNonce;
                            fileHash = _crypto.HashData(originalData);
                            
                            uploadCaption = $"🔐 SecureCloud Encrypted: {fileName} " +
                                          $"(Original: {FormatBytes(originalData.Length)}, " +
                                          $"Encrypted: {FormatBytes(encryptedData.Length)})";
                            
                            LogActivity($"🔐 File encrypted: {FormatBytes(originalData.Length)} → {FormatBytes(encryptedData.Length)}");
                        }
                        else
                        {
                            LogActivity("❌ Encryption failed, uploading without encryption");
                            uploadData = originalData;
                            uploadCaption = $"⚠️ SecureCloud (Unencrypted): {fileName} ({FormatBytes(originalData.Length)})";
                        }
                    }
                    else
                    {
                        uploadData = originalData;
                        uploadCaption = $"⚠️ SecureCloud (Unencrypted): {fileName} ({FormatBytes(originalData.Length)})";
                        LogActivity("⚠️ Uploading without encryption");
                    }

                    UpdateStatus($"Uploading {fileName}...");
                    LogActivity($"📤 Uploading to Telegram...");
                    
                    // Create multipart form data
                    using var form = new MultipartFormDataContent();
                    form.Add(new StringContent(_channelId.ToString()), "chat_id");
                    form.Add(new ByteArrayContent(uploadData), "document", $"{fileName}.encrypted");
                    form.Add(new StringContent(uploadCaption), "caption");
                    
                    var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendDocument", form);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonConvert.DeserializeObject<dynamic>(content);
                        
                        if (result?.ok == true)
                        {
                            var messageId = (long)result.result.message_id;
                            var uploadedSize = (long)result.result.document.file_size;
                            var fileId = (string)result.result.document.file_id;
                            
                            // Add to file list
                            var fileItem = new FileItem
                            {
                                FileName = fileName,
                                Size = originalData.Length,
                                EncryptedSize = uploadData.Length,
                                UploadedAt = DateTime.Now,
                                MessageId = messageId,
                                FileId = fileId,
                                FilePath = openFileDialog.FileName,
                                Nonce = nonce ?? Array.Empty<byte>(),
                                FileHash = fileHash ?? Array.Empty<byte>()
                            };
                            
                            _files.Add(fileItem);
                            UpdateFileStats();
                            
                            var encryptionStatus = fileItem.IsEncrypted ? "🔐 Encrypted" : "⚠️ Unencrypted";
                            UpdateStatus($"✅ Uploaded {fileName} ({encryptionStatus})");
                            LogActivity($"✅ Uploaded {fileName} - {encryptionStatus} (Message ID: {messageId})");
                            
                            MessageBox.Show($"File uploaded successfully!\n\n" +
                                          $"File: {fileName}\n" +
                                          $"Original Size: {FormatBytes(originalData.Length)}\n" +
                                          $"Uploaded Size: {FormatBytes(uploadedSize)}\n" +
                                          $"Encryption: {(fileItem.IsEncrypted ? "✅ Enabled" : "❌ Disabled")}\n" +
                                          $"Message ID: {messageId}", 
                                "Upload Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            UpdateStatus("❌ Upload failed");
                            LogActivity("❌ Upload failed - API error");
                            MessageBox.Show("Upload failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        UpdateStatus($"❌ HTTP Error: {response.StatusCode}");
                        LogActivity($"❌ Upload failed - HTTP {response.StatusCode}");
                        MessageBox.Show($"Upload failed: {response.StatusCode}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"❌ Error: {ex.Message}");
                    LogActivity($"❌ Upload error: {ex.Message}");
                    MessageBox.Show($"Upload error: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = $"SecureCloud Settings\n\n" +
                          $"Bot Token: {_botToken.Substring(0, 20)}...\n" +
                          $"Channel ID: {_channelId}\n" +
                          $"Connection Status: {(_isConnected ? "Connected" : "Not Connected")}\n" +
                          $"Files Tracked: {_files.Count}\n" +
                          $"Total Storage: {FormatBytes(_files.Sum(f => f.Size))}";
            
            MessageBox.Show(settings, "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FileItem fileItem)
            {
                try
                {
                    UpdateStatus($"Downloading {fileItem.FileName}...");
                    LogActivity($"📥 Downloading {fileItem.FileName}...");

                    // Get file info from Telegram
                    var getFileResponse = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/getFile",
                        new StringContent($"{{\"file_id\":\"{fileItem.FileId}\"}}", Encoding.UTF8, "application/json"));

                    if (getFileResponse.IsSuccessStatusCode)
                    {
                        var getFileContent = await getFileResponse.Content.ReadAsStringAsync();
                        var getFileResult = JsonConvert.DeserializeObject<dynamic>(getFileContent);

                        if (getFileResult?.ok == true && getFileResult.result?.file_path != null)
                        {
                            var filePath = (string)getFileResult.result.file_path;
                            var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";

                            // Download the file
                            LogActivity($"📥 Downloading from Telegram...");
                            var downloadedData = await _httpClient.GetByteArrayAsync(downloadUrl);
                            
                            byte[] finalData;
                            
                            if (fileItem.IsEncrypted && _crypto.IsInitialized)
                            {
                                // Decrypt the file
                                LogActivity($"🔓 Decrypting {fileItem.FileName}...");
                                UpdateStatus($"Decrypting {fileItem.FileName}...");
                                
                                var decryptedData = _crypto.DecryptData(downloadedData, fileItem.Nonce);
                                if (decryptedData != null)
                                {
                                    // Verify file integrity
                                    if (fileItem.FileHash.Length > 0)
                                    {
                                        var computedHash = _crypto.HashData(decryptedData);
                                        if (computedHash != null && !computedHash.SequenceEqual(fileItem.FileHash))
                                        {
                                            LogActivity("❌ File integrity check failed");
                                            MessageBox.Show("File integrity check failed. The file may be corrupted.", 
                                                "Integrity Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                            return;
                                        }
                                        LogActivity("✅ File integrity verified");
                                    }
                                    
                                    finalData = decryptedData;
                                    LogActivity($"🔓 File decrypted successfully");
                                }
                                else
                                {
                                    LogActivity("❌ Decryption failed");
                                    MessageBox.Show("Failed to decrypt file. Check your password.", 
                                        "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }
                            else
                            {
                                finalData = downloadedData;
                                if (fileItem.IsEncrypted)
                                {
                                    LogActivity("⚠️ File is encrypted but encryption is not initialized");
                                }
                            }

                            // Save file dialog
                            var saveFileDialog = new SaveFileDialog
                            {
                                Title = "Save Downloaded File",
                                FileName = fileItem.FileName,
                                Filter = "All files (*.*)|*.*"
                            };

                            if (saveFileDialog.ShowDialog() == true)
                            {
                                await File.WriteAllBytesAsync(saveFileDialog.FileName, finalData);
                                
                                var encryptionStatus = fileItem.IsEncrypted ? "🔓 Decrypted" : "📄 Plain";
                                UpdateStatus($"✅ Downloaded {fileItem.FileName} ({encryptionStatus})");
                                LogActivity($"✅ Downloaded {fileItem.FileName} to {saveFileDialog.FileName} ({encryptionStatus})");
                                
                                MessageBox.Show($"File downloaded successfully!\n\n" +
                                              $"Saved to: {saveFileDialog.FileName}\n" +
                                              $"Size: {FormatBytes(finalData.Length)}\n" +
                                              $"Encryption: {(fileItem.IsEncrypted ? "✅ Decrypted" : "❌ Was not encrypted")}\n" +
                                              $"Integrity: {(fileItem.FileHash.Length > 0 ? "✅ Verified" : "⚠️ Not checked")}", 
                                    "Download Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            UpdateStatus("❌ Download failed - Could not get file info");
                            LogActivity("❌ Download failed - Could not get file info");
                            MessageBox.Show("Download failed - Could not get file information", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        UpdateStatus($"❌ Download failed - HTTP {getFileResponse.StatusCode}");
                        LogActivity($"❌ Download failed - HTTP {getFileResponse.StatusCode}");
                        MessageBox.Show($"Download failed: {getFileResponse.StatusCode}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"❌ Download error: {ex.Message}");
                    LogActivity($"❌ Download error: {ex.Message}");
                    MessageBox.Show($"Download error: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task LoadExistingFilesAsync()
        {
            try
            {
                LogActivity("🔍 Loading existing files from channel...");
                
                // This is a simplified approach - in a real app you'd store file metadata
                // For now, we'll just show that the functionality exists
                LogActivity("ℹ️ File history will be loaded from local database in full version");
            }
            catch (Exception ex)
            {
                LogActivity($"❌ Error loading files: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                Title = $"SecureCloud - {message}";
                StatusText.Text = $"Status: {message}";
            });
        }

        private void LogActivity(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var currentLog = ActivityLogText.Text;
                
                if (currentLog == "No activity yet.")
                {
                    ActivityLogText.Text = $"[{timestamp}] {message}";
                }
                else
                {
                    ActivityLogText.Text = $"[{timestamp}] {message}\n{currentLog}";
                }
            });
        }

        private void UpdateFileStats()
        {
            Dispatcher.Invoke(() =>
            {
                FilesCountText.Text = $"Files: {_files.Count}";
                StorageText.Text = $"Storage: {FormatBytes(_files.Sum(f => f.Size))}";
                
                if (_files.Count == 0)
                {
                    FileStatusText.Text = "No files uploaded yet. Click 'Add File' to upload your first file.";
                }
                else
                {
                    FileStatusText.Text = $"{_files.Count} files uploaded. Total size: {FormatBytes(_files.Sum(f => f.Size))}";
                }
            });
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }
    }
}