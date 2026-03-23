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
using TSCloud.Desktop.Models;
using TSCloud.Desktop.Services;
using TSCloud.Desktop.Themes;
using TSCloud.Desktop.Dialogs;

namespace TSCloud.Desktop
{
    // P/Invoke declarations for Rust crypto functions
    public static class NativeCrypto
    {
        private const string DllName = "ts_cloud_core";

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

namespace TSCloud.Desktop
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
        private string _botToken = "";
        private List<ChannelConfig> _channels = new();
        private MultiChannelManager? _multiChannelManager;
        private readonly ObservableCollection<FileItem> _files = new();
        private readonly ObservableCollection<SyncedFolder> _syncedFolders = new();
        private readonly CryptoManager _crypto = new();
        private readonly FolderSyncService _folderSyncService = new();
        private readonly VersioningService _versioningService = new();
        private readonly AnalyticsService _analyticsService = new();
        private bool _isConnected = false;
        private bool _isEncryptionEnabled = false;
        private bool _isConfigured = false;

        public SimpleMainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            
            // Initialize theme system
            ThemeManager.InitializeTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateThemeToggleButton();
            
            // Set up data binding
            FilesDataGrid.ItemsSource = _files;
            SyncedFoldersDataGrid.ItemsSource = _syncedFolders;
            
            // Set up folder sync service events
            _folderSyncService.FileChanged += OnFileChanged;
            _folderSyncService.SyncStatusChanged += OnSyncStatusChanged;
            
            // Set up versioning service events
            _versioningService.VersionCreated += OnVersionCreated;
            _versioningService.VersionRestored += OnVersionRestored;
            
            // Set up analytics service events
            _analyticsService.AnalyticsUpdated += OnAnalyticsUpdated;
            
            // Update initial status
            UpdateStatus("Ready - TSCloud starting up...");
            LogActivity("Application started");
            UpdateFileStats();
            UpdateFolderSyncStats();
            
            // Check for existing configuration or show setup dialog
            _ = Task.Run(InitializeApplicationAsync);
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                // Check for existing configuration
                if (await LoadConfigurationAsync())
                {
                    _isConfigured = true;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateStatus("Configuration loaded - Ready to connect");
                        LogActivity("✅ Configuration loaded from file");
                        UpdateBotInfo();
                    });
                    
                    // Initialize multi-channel manager with loaded config
                    InitializeMultiChannelManager();
                    
                    // Prompt for encryption password
                    await Dispatcher.InvokeAsync(PromptForEncryptionPassword);
                }
                else
                {
                    // Show setup dialog for first-time configuration
                    await Dispatcher.InvokeAsync(ShowTelegramSetupDialog);
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateStatus($"❌ Initialization error: {ex.Message}");
                    LogActivity($"❌ Initialization error: {ex.Message}");
                    MessageBox.Show($"Failed to initialize TSCloud: {ex.Message}", 
                        "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "TSCloud", "user_config.json");
                
                if (!File.Exists(configPath))
                    return false;

                var configJson = await File.ReadAllTextAsync(configPath);
                var config = JsonConvert.DeserializeObject<UserConfiguration>(configJson);
                
                if (config?.Telegram?.BotToken != null && config.Telegram.Channels?.Count > 0)
                {
                    _botToken = config.Telegram.BotToken;
                    _channels = config.Telegram.Channels.Select(c => new ChannelConfig
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Priority = c.Priority,
                        IsActive = c.IsActive,
                        MaxFileSize = c.MaxFileSize,
                        Description = c.Description
                    }).ToList();
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    LogActivity($"⚠️ Failed to load configuration: {ex.Message}");
                });
                return false;
            }
        }

        private async Task<bool> SaveConfigurationAsync()
        {
            try
            {
                var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TSCloud");
                Directory.CreateDirectory(configDir);
                
                var configPath = Path.Combine(configDir, "user_config.json");
                
                var config = new UserConfiguration
                {
                    Telegram = new TelegramConfig
                    {
                        BotToken = _botToken,
                        Channels = _channels.Select(c => new ChannelConfigData
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Priority = c.Priority,
                            IsActive = c.IsActive,
                            MaxFileSize = c.MaxFileSize,
                            Description = c.Description
                        }).ToList()
                    },
                    LastUpdated = DateTime.Now
                };
                
                var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                await File.WriteAllTextAsync(configPath, configJson);
                
                return true;
            }
            catch (Exception ex)
            {
                LogActivity($"❌ Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        private void ShowTelegramSetupDialog()
        {
            var setupDialog = new TelegramSetupDialog();
            if (setupDialog.ShowDialog() == true && setupDialog.Result != null)
            {
                _botToken = setupDialog.Result.BotToken;
                _channels = setupDialog.Result.Channels;
                _isConfigured = true;
                
                // Save configuration
                _ = Task.Run(SaveConfigurationAsync);
                
                // Initialize multi-channel manager
                InitializeMultiChannelManager();
                
                UpdateStatus("Configuration saved - Ready to connect");
                LogActivity("✅ Telegram bot configuration completed");
                UpdateBotInfo();
                
                // Prompt for encryption password
                PromptForEncryptionPassword();
                
                MessageBox.Show("Telegram bot configuration completed successfully!\n\n" +
                              $"Bot Token: {_botToken.Substring(0, 20)}...\n" +
                              $"Channels: {_channels.Count} configured\n\n" +
                              "You can now start uploading files securely.", 
                    "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // User cancelled setup
                UpdateStatus("❌ Setup cancelled - Application not configured");
                LogActivity("⚠️ User cancelled Telegram setup");
                
                var result = MessageBox.Show("TSCloud requires Telegram bot configuration to function.\n\n" +
                                           "Would you like to try the setup again?", 
                    "Setup Required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    ShowTelegramSetupDialog();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private void InitializeMultiChannelManager()
        {
            if (!string.IsNullOrEmpty(_botToken) && _channels.Count > 0)
            {
                _multiChannelManager?.Dispose();
                _multiChannelManager = new MultiChannelManager(_httpClient, _botToken, _channels);
                _multiChannelManager.ChannelHealthChanged += OnChannelHealthChanged;
                _multiChannelManager.FileUploaded += OnFileUploadedToChannel;
                
                LogActivity("🤖 Multi-channel manager initialized with user configuration");
            }
        }

        private void UpdateBotInfo()
        {
            if (!string.IsNullOrEmpty(_botToken) && _channels.Count > 0)
            {
                BotInfoText.Text = $"Bot: {_botToken.Substring(0, 20)}... (User Configured)";
                ChannelInfoText.Text = $"Channels: {_channels.Count} configured ({_channels.Count(c => c.IsActive)} active)";
            }
            else
            {
                BotInfoText.Text = "Bot: Not configured";
                ChannelInfoText.Text = "Channels: Not configured";
            }
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
            MessageBox.Show("TSCloud is working! 🎉\n\nYour Telegram bot is ready:\n@mtuconbot\nChannel: confess", 
                "TSCloud Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConfigured || _multiChannelManager == null)
            {
                MessageBox.Show("Please configure your Telegram bot first.\n\nGo to Settings → Reconfigure Telegram Bot", 
                    "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                UpdateStatus("Testing multi-channel connections...");
                LogActivity("Testing bot connection to all channels...");
                
                var healthyChannels = new List<string>();
                var unhealthyChannels = new List<string>();

                foreach (var channel in _channels.Where(c => c.IsActive))
                {
                    var isHealthy = await _multiChannelManager.TestChannelHealthAsync(channel.Id);
                    if (isHealthy)
                    {
                        healthyChannels.Add($"{channel.Name} ({channel.Id})");
                    }
                    else
                    {
                        unhealthyChannels.Add($"{channel.Name} ({channel.Id})");
                    }
                }

                if (healthyChannels.Count > 0)
                {
                    _isConnected = true;
                    var statusMessage = $"✅ Connected to {healthyChannels.Count}/{_channels.Count} channels";
                    UpdateStatus(statusMessage);
                    LogActivity(statusMessage);
                    
                    // Update channel info
                    UpdateBotInfo();
                    
                    var message = $"Multi-channel connection established!\n\n" +
                                 $"Healthy Channels ({healthyChannels.Count}):\n" +
                                 string.Join("\n", healthyChannels.Select(c => $"✅ {c}"));
                    
                    if (unhealthyChannels.Count > 0)
                    {
                        message += $"\n\nUnhealthy Channels ({unhealthyChannels.Count}):\n" +
                                  string.Join("\n", unhealthyChannels.Select(c => $"❌ {c}"));
                    }
                    
                    message += $"\n\nRedundancy: {(healthyChannels.Count >= 2 ? "✅ Enabled" : "⚠️ Limited")}";
                    
                    MessageBox.Show(message, "Multi-Channel Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _isConnected = false;
                    UpdateStatus("❌ No channels available");
                    LogActivity("❌ All channels are unhealthy");
                    MessageBox.Show("Failed to connect to any channels. Please check your configuration.", 
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!_isConfigured)
            {
                MessageBox.Show("Please configure your Telegram bot first.\n\nGo to Settings → Reconfigure Telegram Bot", 
                    "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                await UploadFileAsync(openFileDialog.FileName, isAutoSync: false);
            }
        }

        private async Task UploadFileAsync(string filePath, bool isAutoSync = false)
        {
            if (_multiChannelManager == null)
            {
                var syncPrefix = isAutoSync ? "🔄 Auto-sync: " : "";
                LogActivity($"❌ {syncPrefix}Multi-channel manager not initialized");
                if (!isAutoSync)
                {
                    MessageBox.Show("Multi-channel manager not initialized. Please check your configuration.", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            try
            {
                var fileName = Path.GetFileName(filePath);
                var syncPrefix = isAutoSync ? "🔄 Auto-sync: " : "";
                
                UpdateStatus($"Processing {fileName}...");
                LogActivity($"📤 {syncPrefix}Processing {fileName}...");
                
                var originalData = await File.ReadAllBytesAsync(filePath);
                byte[] uploadData;
                byte[]? nonce = null;
                byte[]? fileHash = null;
                string uploadCaption;

                if (_isEncryptionEnabled && _crypto.IsInitialized)
                {
                    // Encrypt the file
                    LogActivity($"🔐 {syncPrefix}Encrypting {fileName}...");
                    UpdateStatus($"Encrypting {fileName}...");
                    
                    var (encryptedData, encryptionNonce) = _crypto.EncryptData(originalData);
                    if (encryptedData != null && encryptionNonce != null)
                    {
                        uploadData = encryptedData;
                        nonce = encryptionNonce;
                        fileHash = _crypto.HashData(originalData);
                        
                        var syncIndicator = isAutoSync ? "🔄🔐" : "🔐";
                        uploadCaption = $"{syncIndicator} TSCloud Encrypted: {fileName} " +
                                      $"(Original: {FormatBytes(originalData.Length)}, " +
                                      $"Encrypted: {FormatBytes(encryptedData.Length)})";
                        
                        LogActivity($"🔐 {syncPrefix}File encrypted: {FormatBytes(originalData.Length)} → {FormatBytes(encryptedData.Length)}");
                    }
                    else
                    {
                        LogActivity($"❌ {syncPrefix}Encryption failed, uploading without encryption");
                        uploadData = originalData;
                        var syncIndicator = isAutoSync ? "🔄⚠️" : "⚠️";
                        uploadCaption = $"{syncIndicator} TSCloud (Unencrypted): {fileName} ({FormatBytes(originalData.Length)})";
                    }
                }
                else
                {
                    uploadData = originalData;
                    var syncIndicator = isAutoSync ? "🔄⚠️" : "⚠️";
                    uploadCaption = $"{syncIndicator} TSCloud (Unencrypted): {fileName} ({FormatBytes(originalData.Length)})";
                    LogActivity($"⚠️ {syncPrefix}Uploading without encryption");
                }

                UpdateStatus($"Uploading {fileName} to multiple channels...");
                LogActivity($"📤 {syncPrefix}Uploading to multiple channels with redundancy...");
                
                // Upload with redundancy
                var locations = await _multiChannelManager.UploadFileWithRedundancyAsync(uploadData, fileName, Guid.NewGuid().ToString());
                
                if (locations.Count > 0)
                {
                    var primaryLocation = locations.First(l => l.IsPrimary);
                    
                    // Add to file list
                    var fileItem = new FileItem
                    {
                        FileName = fileName,
                        Size = originalData.Length,
                        EncryptedSize = uploadData.Length,
                        UploadedAt = DateTime.Now,
                        MessageId = primaryLocation.MessageId,
                        FileId = primaryLocation.TelegramFileId,
                        FilePath = filePath,
                        Nonce = nonce ?? Array.Empty<byte>(),
                        FileHash = fileHash ?? Array.Empty<byte>()
                    };
                    
                    _files.Add(fileItem);
                    UpdateFileStats();
                    
                    // Record analytics
                    var uploadTime = TimeSpan.FromSeconds(2); // Approximate upload time
                    _analyticsService.RecordFileUpload(originalData.Length, uploadTime, fileItem.IsEncrypted);
                    
                    // Create version if file already exists
                    if (File.Exists(filePath))
                    {
                        var changeDescription = isAutoSync ? "Auto-sync update" : "Manual upload";
                        await _versioningService.CreateVersionAsync(
                            filePath, primaryLocation.MessageId, primaryLocation.TelegramFileId, nonce ?? Array.Empty<byte>(), 
                            fileHash ?? Array.Empty<byte>(), originalData.Length, uploadData.Length, changeDescription);
                    }
                    
                    var encryptionStatus = fileItem.IsEncrypted ? "🔐 Encrypted" : "⚠️ Unencrypted";
                    var syncStatus = isAutoSync ? " (Auto-sync)" : "";
                    var redundancyInfo = $" [{locations.Count} copies]";
                    UpdateStatus($"✅ Uploaded {fileName} ({encryptionStatus}){syncStatus}{redundancyInfo}");
                    LogActivity($"✅ {syncPrefix}Uploaded {fileName} - {encryptionStatus} to {locations.Count} channels");
                    
                    if (!isAutoSync)
                    {
                        var channelList = string.Join(", ", locations.Select(l => 
                            _channels.FirstOrDefault(c => c.Id == l.ChannelId)?.Name ?? l.ChannelId.ToString()));
                        
                        MessageBox.Show($"File uploaded successfully with redundancy!\n\n" +
                                      $"File: {fileName}\n" +
                                      $"Original Size: {FormatBytes(originalData.Length)}\n" +
                                      $"Encrypted Size: {FormatBytes(uploadData.Length)}\n" +
                                      $"Encryption: {(fileItem.IsEncrypted ? "✅ Enabled" : "❌ Disabled")}\n" +
                                      $"Copies: {locations.Count}\n" +
                                      $"Channels: {channelList}\n" +
                                      $"Primary Message ID: {primaryLocation.MessageId}", 
                            "Upload Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    UpdateStatus("❌ Upload failed to all channels");
                    LogActivity($"❌ {syncPrefix}Upload failed to all channels");
                    if (!isAutoSync)
                    {
                        MessageBox.Show("Upload failed to all channels", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                var syncPrefix = isAutoSync ? "🔄 Auto-sync: " : "";
                UpdateStatus($"❌ Error: {ex.Message}");
                LogActivity($"❌ {syncPrefix}Upload error: {ex.Message}");
                if (!isAutoSync)
                {
                    MessageBox.Show($"Upload error: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsMenu = new ContextMenu();
            
            // View Current Settings
            var viewSettingsItem = new MenuItem { Header = "📋 View Current Settings" };
            viewSettingsItem.Click += (s, args) =>
            {
                var settings = $"TSCloud Configuration\n\n" +
                              $"Bot Token: {(_isConfigured ? _botToken.Substring(0, 20) + "..." : "Not configured")}\n" +
                              $"Channels: {_channels.Count} configured ({_channels.Count(c => c.IsActive)} active)\n" +
                              $"Connection Status: {(_isConnected ? "Connected" : "Not Connected")}\n" +
                              $"Encryption: {(_isEncryptionEnabled ? "Enabled" : "Disabled")}\n" +
                              $"Files Tracked: {_files.Count}\n" +
                              $"Total Storage: {FormatBytes(_files.Sum(f => f.Size))}\n" +
                              $"Synced Folders: {_syncedFolders.Count}\n\n" +
                              "Channel Details:\n";
                
                foreach (var channel in _channels)
                {
                    var status = channel.IsActive ? "Active" : "Inactive";
                    settings += $"• {channel.Name} (Priority {channel.Priority}): {status}\n";
                }
                
                MessageBox.Show(settings, "Current Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            settingsMenu.Items.Add(viewSettingsItem);
            
            settingsMenu.Items.Add(new Separator());
            
            // Reconfigure Telegram Bot
            var reconfigureItem = new MenuItem { Header = "🤖 Reconfigure Telegram Bot" };
            reconfigureItem.Click += (s, args) =>
            {
                var result = MessageBox.Show("This will replace your current Telegram bot configuration.\n\n" +
                                           "Are you sure you want to continue?", 
                    "Reconfigure Bot", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    ShowTelegramSetupDialog();
                }
            };
            settingsMenu.Items.Add(reconfigureItem);
            
            // Reset Encryption
            var resetEncryptionItem = new MenuItem { Header = "🔐 Reset Encryption Password" };
            resetEncryptionItem.Click += (s, args) =>
            {
                var result = MessageBox.Show("This will reset your encryption password.\n\n" +
                                           "You will need to re-enter your password to decrypt existing files.\n\n" +
                                           "Continue?", 
                    "Reset Encryption", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    _isEncryptionEnabled = false;
                    PromptForEncryptionPassword();
                }
            };
            settingsMenu.Items.Add(resetEncryptionItem);
            
            settingsMenu.Items.Add(new Separator());
            
            // Export Configuration
            var exportConfigItem = new MenuItem { Header = "📤 Export Configuration" };
            exportConfigItem.Click += async (s, args) =>
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Export TSCloud Configuration",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FileName = $"TSCloud_config_{DateTime.Now:yyyyMMdd}.json"
                };
                
                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        var exportConfig = new
                        {
                            BotToken = _botToken,
                            Channels = _channels.Select(c => new
                            {
                                c.Id,
                                c.Name,
                                c.Priority,
                                c.IsActive,
                                c.Description
                            }),
                            ExportedAt = DateTime.Now,
                            Version = "1.0"
                        };
                        
                        var json = JsonConvert.SerializeObject(exportConfig, Formatting.Indented);
                        await File.WriteAllTextAsync(saveDialog.FileName, json);
                        
                        MessageBox.Show($"Configuration exported successfully!\n\nSaved to: {saveDialog.FileName}", 
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export configuration: {ex.Message}", 
                            "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            settingsMenu.Items.Add(exportConfigItem);
            
            // Import Configuration
            var importConfigItem = new MenuItem { Header = "📥 Import Configuration" };
            importConfigItem.Click += async (s, args) =>
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "Import TSCloud Configuration",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                };
                
                if (openDialog.ShowDialog() == true)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(openDialog.FileName);
                        var importConfig = JsonConvert.DeserializeObject<dynamic>(json);
                        
                        if (importConfig?.BotToken != null && importConfig.Channels != null)
                        {
                            var result = MessageBox.Show("This will replace your current configuration.\n\n" +
                                                       "Are you sure you want to continue?", 
                                "Import Configuration", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                _botToken = importConfig.BotToken;
                                _channels = JsonConvert.DeserializeObject<List<ChannelConfig>>(
                                    importConfig.Channels.ToString()) ?? new List<ChannelConfig>();
                                
                                await SaveConfigurationAsync();
                                InitializeMultiChannelManager();
                                UpdateBotInfo();
                                
                                MessageBox.Show("Configuration imported successfully!", 
                                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Invalid configuration file format.", 
                                "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import configuration: {ex.Message}", 
                            "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            settingsMenu.Items.Add(importConfigItem);
            
            settingsMenu.PlacementTarget = sender as Button;
            settingsMenu.IsOpen = true;
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
                                
                                // Record analytics
                                var downloadTime = TimeSpan.FromSeconds(1.5); // Approximate download time
                                _analyticsService.RecordFileDownload(finalData.Length, downloadTime, fileItem.IsEncrypted);
                                
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
                Title = $"TSCloud - {message}";
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
            ThemeManager.SaveThemePreference();
            _folderSyncService?.Dispose();
            _versioningService?.Dispose();
            _analyticsService?.Dispose();
            _multiChannelManager?.Dispose();
            _httpClient?.Dispose();
            base.OnClosed(e);
        }

        // Theme Management
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            LogActivity($"🎨 Switched to {ThemeManager.CurrentTheme} theme");
        }

        private void OnThemeChanged(object? sender, AppTheme newTheme)
        {
            UpdateThemeToggleButton();
        }

        private void UpdateThemeToggleButton()
        {
            if (ThemeToggleButton != null)
            {
                ThemeToggleButton.Content = ThemeManager.CurrentTheme == AppTheme.Dark ? "☀️" : "🌙";
                ThemeToggleButton.ToolTip = ThemeManager.CurrentTheme == AppTheme.Dark ? "Switch to Light Mode" : "Switch to Dark Mode";
            }
        }

        // Folder Sync Event Handlers
        private async void OnFileChanged(object? sender, FileChangedEventArgs e)
        {
            if (!_isConnected || !_isEncryptionEnabled)
                return;

            try
            {
                // Only handle file creation and modification for auto-sync
                if (e.ChangeType == FileChangeType.Created || e.ChangeType == FileChangeType.Modified)
                {
                    // Wait a bit to ensure file is fully written
                    await Task.Delay(1000);
                    
                    if (File.Exists(e.FilePath))
                    {
                        await UploadFileAsync(e.FilePath, isAutoSync: true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogActivity($"❌ Auto-sync error for {Path.GetFileName(e.FilePath)}: {ex.Message}");
            }
        }

        private void OnSyncStatusChanged(object? sender, FolderSyncEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LogActivity($"📁 {Path.GetFileName(e.FolderPath)}: {e.Message}");
                UpdateFolderSyncStats();
            });
        }

        // Folder Sync Button Handlers
        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to sync with TSCloud",
                ShowNewFolderButton = false
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folderPath = folderDialog.SelectedPath;
                
                if (_folderSyncService.AddFolder(folderPath))
                {
                    var syncedFolder = _folderSyncService.GetSyncedFolders().FirstOrDefault(f => f.FolderPath == folderPath);
                    if (syncedFolder != null)
                    {
                        _syncedFolders.Add(syncedFolder);
                        LogActivity($"📁 Added folder for sync: {Path.GetFileName(folderPath)}");
                        UpdateFolderSyncStats();
                        
                        MessageBox.Show($"Folder added for syncing!\n\nPath: {folderPath}\nFiles: {syncedFolder.FileCount}\nSize: {syncedFolder.TotalSizeFormatted}", 
                            "Folder Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to add folder for syncing. The folder may already be added or inaccessible.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (SyncedFoldersDataGrid.SelectedItem is SyncedFolder selectedFolder)
            {
                var result = MessageBox.Show($"Remove folder from sync?\n\nPath: {selectedFolder.FolderPath}\n\nThis will stop monitoring the folder but won't delete any uploaded files.", 
                    "Remove Folder Sync", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (_folderSyncService.RemoveFolder(selectedFolder.FolderPath))
                    {
                        _syncedFolders.Remove(selectedFolder);
                        LogActivity($"📁 Removed folder from sync: {selectedFolder.FolderName}");
                        UpdateFolderSyncStats();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a folder to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void ScanAllFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_syncedFolders.Count == 0)
            {
                MessageBox.Show("No folders are being synced.", "No Folders", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateStatus("Scanning all folders...");
            LogActivity("🔍 Starting scan of all synced folders");

            foreach (var folder in _syncedFolders)
            {
                await _folderSyncService.ScanFolderForChanges(folder.FolderPath);
            }

            UpdateStatus("Folder scan completed");
            LogActivity("✅ Completed scan of all synced folders");
        }

        private void PauseAllFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in _syncedFolders)
            {
                _folderSyncService.SetFolderActive(folder.FolderPath, false);
            }
            LogActivity("⏸️ Paused monitoring for all folders");
        }

        private void ResumeAllFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in _syncedFolders)
            {
                _folderSyncService.SetFolderActive(folder.FolderPath, true);
            }
            LogActivity("▶️ Resumed monitoring for all folders");
        }

        private void PauseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SyncedFolder folder)
            {
                _folderSyncService.SetFolderActive(folder.FolderPath, !folder.IsActive);
                var action = folder.IsActive ? "Resumed" : "Paused";
                LogActivity($"📁 {action} monitoring for {folder.FolderName}");
            }
        }

        private async void ScanFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SyncedFolder folder)
            {
                UpdateStatus($"Scanning {folder.FolderName}...");
                await _folderSyncService.ScanFolderForChanges(folder.FolderPath);
                UpdateStatus("Ready");
            }
        }

        private void RemoveSingleFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SyncedFolder folder)
            {
                var result = MessageBox.Show($"Remove '{folder.FolderName}' from sync?", 
                    "Remove Folder", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (_folderSyncService.RemoveFolder(folder.FolderPath))
                    {
                        _syncedFolders.Remove(folder);
                        LogActivity($"📁 Removed {folder.FolderName} from sync");
                        UpdateFolderSyncStats();
                    }
                }
            }
        }

        private void UpdateFolderSyncStats()
        {
            Dispatcher.Invoke(() =>
            {
                var activeFolders = _syncedFolders.Count(f => f.IsActive);
                var totalFiles = _syncedFolders.Sum(f => f.FileCount);
                var totalSize = _syncedFolders.Sum(f => f.TotalSize);

                if (_syncedFolders.Count == 0)
                {
                    FolderSyncStatusText.Text = "No folders are being synced. Click 'Add Folder' to start syncing a folder.";
                }
                else
                {
                    FolderSyncStatusText.Text = $"{_syncedFolders.Count} folders synced ({activeFolders} active). " +
                                              $"Total: {totalFiles} files, {FormatBytes(totalSize)}";
                }
            });
        }

        // Versioning Event Handlers
        private void OnVersionCreated(object? sender, VersionCreatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LogActivity($"📝 Version {e.Version.VersionNumber} created for {e.Version.FileName}");
            });
        }

        private void OnVersionRestored(object? sender, VersionRestoredEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LogActivity($"🔄 Restored version {e.Version.VersionNumber} of {e.Version.FileName}");
            });
        }

        // Analytics Event Handlers
        private void OnAnalyticsUpdated(object? sender, AnalyticsUpdatedEventArgs e)
        {
            // Update analytics display if needed
            // This could trigger UI updates for real-time metrics
        }

        // New button handlers for advanced features
        private void ViewVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (FilesDataGrid.SelectedItem is FileItem selectedFile)
            {
                var versions = _versioningService.GetFileVersions(selectedFile.FileId);
                var versionInfo = string.Join("\n", versions.Select(v => 
                    $"Version {v.VersionNumber}: {v.CreatedAt:yyyy-MM-dd HH:mm} - {v.ChangeDescription}"));
                
                if (string.IsNullOrEmpty(versionInfo))
                {
                    MessageBox.Show("No versions found for this file.", "File Versions", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Versions for {selectedFile.FileName}:\n\n{versionInfo}", 
                        "File Versions", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a file to view versions.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ViewAnalyticsButton_Click(object sender, RoutedEventArgs e)
        {
            var summary = _analyticsService.GetSummary();
            var health = _analyticsService.GetSystemHealth();
            var channelStats = _multiChannelManager?.GetChannelStatistics() ?? new Dictionary<long, ChannelHealth>();
            
            var analyticsInfo = $"Analytics Summary (Last 30 Days)\n\n" +
                              $"Files Uploaded: {summary.TotalFilesUploaded}\n" +
                              $"Files Downloaded: {summary.TotalFilesDownloaded}\n" +
                              $"Data Uploaded: {FormatBytes(summary.TotalBytesUploaded)}\n" +
                              $"Data Downloaded: {FormatBytes(summary.TotalBytesDownloaded)}\n" +
                              $"Encrypted Files: {summary.EncryptedFilesUploaded}\n" +
                              $"Sync Operations: {summary.SyncOperations}\n\n" +
                              $"Performance:\n" +
                              $"Avg Upload Speed: {FormatBytes((long)summary.AverageUploadSpeed)}/s\n" +
                              $"Avg Download Speed: {FormatBytes((long)summary.AverageDownloadSpeed)}/s\n\n" +
                              $"System Health: {health.OverallHealth}\n" +
                              $"Security Events: {health.CriticalEventsCount} critical\n" +
                              $"Last Updated: {health.LastUpdated:HH:mm:ss}\n\n" +
                              $"Channel Health:\n";
            
            if (_channels.Count > 0 && channelStats.Count > 0)
            {
                foreach (var channel in _channels)
                {
                    if (channelStats.TryGetValue(channel.Id, out var channelHealth))
                    {
                        var status = channelHealth.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
                        var errorInfo = channelHealth.ErrorCount > 0 ? $" ({channelHealth.ErrorCount} errors)" : "";
                        analyticsInfo += $"• {channel.Name}: {status}{errorInfo}\n";
                    }
                }
            }
            else
            {
                analyticsInfo += "• No channels configured\n";
            }
            
            MessageBox.Show(analyticsInfo, "Analytics Dashboard", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Multi-channel event handlers
        private void OnChannelHealthChanged(object? sender, ChannelHealthChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var channel = _channels.FirstOrDefault(c => c.Id == e.ChannelId);
                var channelName = channel?.Name ?? e.ChannelId.ToString();
                var status = e.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
                var errorInfo = !string.IsNullOrEmpty(e.Error) ? $" - {e.Error}" : "";
                
                LogActivity($"📡 Channel {channelName}: {status}{errorInfo}");
            });
        }

        private void OnFileUploadedToChannel(object? sender, FileUploadedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var copyType = e.Location.IsPrimary ? "Primary" : "Backup";
                LogActivity($"📤 {copyType} copy uploaded to {e.ChannelName} (Message ID: {e.Location.MessageId})");
            });
        }
    }

    // Configuration data classes
    public class UserConfiguration
    {
        public TelegramConfig? Telegram { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TelegramConfig
    {
        public string BotToken { get; set; } = "";
        public List<ChannelConfigData> Channels { get; set; } = new();
    }

    public class ChannelConfigData
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public long MaxFileSize { get; set; } = 52428800; // 50MB
        public string Description { get; set; } = "";
    }
}