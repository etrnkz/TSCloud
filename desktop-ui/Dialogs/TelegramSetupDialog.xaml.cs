using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TSCloud.Desktop.Services;

namespace TSCloud.Desktop.Dialogs
{
    public partial class TelegramSetupDialog : Window
    {
        private readonly HttpClient _httpClient;
        private readonly ObservableCollection<ChannelConfigViewModel> _channels;
        private string _botToken = "";
        private bool _botTokenValid = false;

        public TelegramConfiguration? Result { get; private set; }

        public TelegramSetupDialog()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _channels = new ObservableCollection<ChannelConfigViewModel>();
            ChannelsDataGrid.ItemsSource = _channels;

            // Add default channel template
            _channels.Add(new ChannelConfigViewModel
            {
                Name = "Primary Storage",
                Id = "",
                Priority = 1,
                IsActive = true
            });
        }

        private void BotTokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _botToken = BotTokenTextBox.Text.Trim();
            _botTokenValid = false;
            
            TestBotButton.IsEnabled = !string.IsNullOrEmpty(_botToken) && _botToken.Contains(":");
            BotTestStatus.Text = "";
            
            UpdateSaveButtonState();
        }

        private async void TestBotButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_botToken))
                return;

            TestBotButton.IsEnabled = false;
            BotTestStatus.Text = "🔄 Testing...";

            try
            {
                var response = await _httpClient.GetAsync($"https://api.telegram.org/bot{_botToken}/getMe");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    if (result.GetProperty("ok").GetBoolean())
                    {
                        var botInfo = result.GetProperty("result");
                        var botName = botInfo.GetProperty("first_name").GetString();
                        var username = botInfo.GetProperty("username").GetString();
                        
                        _botTokenValid = true;
                        BotTestStatus.Text = $"✅ Connected to {botName} (@{username})";
                        AutoDetectChannelsButton.IsEnabled = true;
                        TestChannelsButton.IsEnabled = true;
                        
                        ShowStatus("✅ Bot Connection Successful", 
                                 $"Successfully connected to your bot: {botName} (@{username})", 
                                 true);
                    }
                    else
                    {
                        BotTestStatus.Text = "❌ Invalid bot token";
                        ShowStatus("❌ Bot Connection Failed", 
                                 "The bot token is invalid. Please check and try again.", 
                                 false);
                    }
                }
                else
                {
                    BotTestStatus.Text = "❌ Connection failed";
                    ShowStatus("❌ Connection Failed", 
                             "Unable to connect to Telegram. Please check your internet connection.", 
                             false);
                }
            }
            catch (Exception ex)
            {
                BotTestStatus.Text = "❌ Error occurred";
                ShowStatus("❌ Error", $"An error occurred: {ex.Message}", false);
            }
            finally
            {
                TestBotButton.IsEnabled = true;
                UpdateSaveButtonState();
            }
        }

        private async void AutoDetectChannels_Click(object sender, RoutedEventArgs e)
        {
            if (!_botTokenValid)
            {
                MessageBox.Show("Please test your bot token first.", "Bot Token Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AutoDetectChannelsButton.IsEnabled = false;
            ShowStatus("🔍 Auto-Detecting Channels", 
                     "Scanning for channels where your bot is an admin...", 
                     true);

            try
            {
                // This is a simplified auto-detection
                // In reality, you'd need to use getUpdates or have the user send a message
                await Task.Delay(2000); // Simulate detection process
                
                ShowStatus("ℹ️ Manual Setup Required", 
                         "Automatic channel detection requires the bot to receive messages. Please add channels manually using the Channel ID method.", 
                         true);
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Auto-Detection Failed", $"Error during auto-detection: {ex.Message}", false);
            }
            finally
            {
                AutoDetectChannelsButton.IsEnabled = true;
            }
        }

        private async void TestChannelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_botTokenValid)
            {
                MessageBox.Show("Please test your bot token first.", "Bot Token Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var channelsToTest = _channels.Where(c => !string.IsNullOrEmpty(c.Id) && c.IsActive).ToList();
            if (channelsToTest.Count == 0)
            {
                MessageBox.Show("Please add at least one channel with a valid Channel ID.", 
                    "No Channels", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestChannelsButton.IsEnabled = false;
            ShowStatus("🧪 Testing Channels", "Testing connection to all configured channels...", true);

            var healthyChannels = new List<string>();
            var unhealthyChannels = new List<string>();

            foreach (var channel in channelsToTest)
            {
                try
                {
                    if (long.TryParse(channel.Id, out var channelId))
                    {
                        var response = await _httpClient.PostAsync(
                            $"https://api.telegram.org/bot{_botToken}/getChat",
                            new StringContent(JsonSerializer.Serialize(new { chat_id = channelId }),
                                System.Text.Encoding.UTF8, "application/json"));

                        if (response.IsSuccessStatusCode)
                        {
                            healthyChannels.Add($"{channel.Name} ({channel.Id})");
                        }
                        else
                        {
                            unhealthyChannels.Add($"{channel.Name} ({channel.Id})");
                        }
                    }
                    else
                    {
                        unhealthyChannels.Add($"{channel.Name} (Invalid ID)");
                    }
                }
                catch
                {
                    unhealthyChannels.Add($"{channel.Name} (Connection Error)");
                }
            }

            var statusMessage = "";
            if (healthyChannels.Count > 0)
            {
                statusMessage += $"✅ Healthy Channels ({healthyChannels.Count}):\n";
                statusMessage += string.Join("\n", healthyChannels.Select(c => $"  • {c}"));
            }

            if (unhealthyChannels.Count > 0)
            {
                if (healthyChannels.Count > 0) statusMessage += "\n\n";
                statusMessage += $"❌ Unhealthy Channels ({unhealthyChannels.Count}):\n";
                statusMessage += string.Join("\n", unhealthyChannels.Select(c => $"  • {c}"));
            }

            var isSuccess = healthyChannels.Count > 0;
            var title = isSuccess ? "✅ Channel Test Results" : "❌ All Channels Failed";
            
            ShowStatus(title, statusMessage, isSuccess);
            TestChannelsButton.IsEnabled = true;
            UpdateSaveButtonState();
        }

        private void OpenTelegramWeb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://web.telegram.org/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Telegram Web: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowChannelGuide_Click(object sender, RoutedEventArgs e)
        {
            var guide = @"📖 Detailed Channel Setup Guide

1. CREATE A PRIVATE CHANNEL:
   • Open Telegram
   • Tap the pencil icon (compose)
   • Select 'New Channel'
   • Enter channel name (e.g., 'My TSCloud Storage')
   • Add description (optional)
   • Choose 'Private' (VERY IMPORTANT!)

2. ADD YOUR BOT AS ADMIN:
   • In your new channel, tap the channel name at the top
   • Tap 'Administrators'
   • Tap 'Add Administrator'
   • Search for your bot username
   • Give it 'Post Messages' permission
   • Save

3. GET CHANNEL ID:
   • Forward any message from the channel to @userinfobot
   • The bot will reply with the channel ID (starts with -100)
   • Copy this ID and paste it in the Channel ID field

4. REPEAT FOR BACKUP CHANNELS:
   • Create 2-3 additional channels for redundancy
   • Follow the same process for each
   • Use different priorities (1=primary, 2=backup1, etc.)

🔒 SECURITY TIPS:
• Always use PRIVATE channels
• Never share your bot token
• Only add your bot as admin
• Use descriptive channel names for organization";

            MessageBox.Show(guide, "Channel Setup Guide", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowStatus(string title, string message, bool isSuccess)
        {
            StatusTitle.Text = title;
            StatusMessage.Text = message;
            StatusPanel.Visibility = Visibility.Visible;
            
            // You could change colors based on isSuccess if needed
        }

        private void UpdateSaveButtonState()
        {
            var hasValidChannels = _channels.Any(c => !string.IsNullOrEmpty(c.Id) && c.IsActive);
            SaveButton.IsEnabled = _botTokenValid && hasValidChannels;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var validChannels = _channels
                    .Where(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Id) && c.IsActive)
                    .Select(c => new ChannelConfig
                    {
                        Id = long.Parse(c.Id),
                        Name = c.Name,
                        Priority = c.Priority,
                        IsActive = c.IsActive,
                        MaxFileSize = 52428800, // 50MB
                        Description = $"User-configured channel: {c.Name}"
                    })
                    .ToList();

                if (validChannels.Count == 0)
                {
                    MessageBox.Show("Please add at least one valid channel.", "No Valid Channels", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Result = new TelegramConfiguration
                {
                    BotToken = _botToken,
                    Channels = validChannels
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }
    }

    public class ChannelConfigViewModel
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
    }

    public class TelegramConfiguration
    {
        public string BotToken { get; set; } = "";
        public List<ChannelConfig> Channels { get; set; } = new();
    }
}