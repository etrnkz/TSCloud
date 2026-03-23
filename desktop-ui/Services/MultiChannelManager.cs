using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace TSCloud.Desktop.Services
{
    public class ChannelConfig
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public long MaxFileSize { get; set; } = 52428800; // 50MB
        public string Description { get; set; } = "";
    }

    public class ChannelHealth
    {
        public long ChannelId { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastCheck { get; set; } = DateTime.Now;
        public TimeSpan ResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public int SuccessCount { get; set; }
        public string? LastError { get; set; }
    }

    public class FileLocation
    {
        public string FileId { get; set; } = "";
        public long ChannelId { get; set; }
        public long MessageId { get; set; }
        public string TelegramFileId { get; set; } = "";
        public bool IsPrimary { get; set; }
        public DateTime UploadTime { get; set; }
        public bool Verified { get; set; }
    }

    public class RedundancyConfig
    {
        public bool Enabled { get; set; } = true;
        public int MinCopies { get; set; } = 2;
        public int MaxCopies { get; set; } = 3;
        public bool VerifyUploads { get; set; } = true;
        public bool AutoRepair { get; set; } = true;
    }

    public class MultiChannelManager : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _botToken;
        private readonly List<ChannelConfig> _channels;
        private readonly Dictionary<long, ChannelHealth> _channelHealth;
        private readonly RedundancyConfig _redundancyConfig;
        private int _currentChannelIndex = 0;
        private bool _disposed = false;

        public event EventHandler<ChannelHealthChangedEventArgs>? ChannelHealthChanged;
        public event EventHandler<FileUploadedEventArgs>? FileUploaded;

        public MultiChannelManager(HttpClient httpClient, string botToken, List<ChannelConfig> channels)
        {
            _httpClient = httpClient;
            _botToken = botToken;
            _channels = channels;
            _channelHealth = new Dictionary<long, ChannelHealth>();
            _redundancyConfig = new RedundancyConfig();

            // Initialize channel health
            foreach (var channel in _channels)
            {
                _channelHealth[channel.Id] = new ChannelHealth
                {
                    ChannelId = channel.Id,
                    IsHealthy = true,
                    LastCheck = DateTime.Now
                };
            }

            // Start background health checks
            _ = Task.Run(StartHealthCheckLoop);
        }

        public async Task<List<FileLocation>> UploadFileWithRedundancyAsync(
            byte[] fileData, 
            string fileName, 
            string fileId)
        {
            var locations = new List<FileLocation>();
            var healthyChannels = GetHealthyChannels();

            if (healthyChannels.Count == 0)
            {
                throw new InvalidOperationException("No healthy channels available");
            }

            var copiesToCreate = _redundancyConfig.Enabled 
                ? Math.Min(_redundancyConfig.MaxCopies, Math.Max(_redundancyConfig.MinCopies, healthyChannels.Count))
                : 1;

            var selectedChannels = SelectChannelsForUpload(healthyChannels, copiesToCreate);

            for (int i = 0; i < selectedChannels.Count; i++)
            {
                var channel = selectedChannels[i];
                try
                {
                    var (messageId, telegramFileId) = await UploadToChannelAsync(channel.Id, fileData, fileName);
                    
                    var location = new FileLocation
                    {
                        FileId = fileId,
                        ChannelId = channel.Id,
                        MessageId = messageId,
                        TelegramFileId = telegramFileId,
                        IsPrimary = i == 0,
                        UploadTime = DateTime.Now,
                        Verified = true
                    };

                    locations.Add(location);
                    RecordSuccess(channel.Id);

                    FileUploaded?.Invoke(this, new FileUploadedEventArgs
                    {
                        Location = location,
                        ChannelName = channel.Name
                    });
                }
                catch (Exception ex)
                {
                    RecordError(channel.Id, ex.Message);
                    continue;
                }
            }

            if (locations.Count == 0)
            {
                throw new InvalidOperationException("Failed to upload to any channel");
            }

            return locations;
        }

        public async Task<byte[]> DownloadFileWithFallbackAsync(List<FileLocation> locations)
        {
            // Sort by priority: primary first, then by upload time
            var sortedLocations = locations
                .Where(loc => IsChannelHealthy(loc.ChannelId))
                .OrderByDescending(loc => loc.IsPrimary)
                .ThenByDescending(loc => loc.UploadTime)
                .ToList();

            Exception? lastException = null;

            foreach (var location in sortedLocations)
            {
                try
                {
                    var fileData = await DownloadFromChannelAsync(location.ChannelId, location.TelegramFileId);
                    RecordSuccess(location.ChannelId);
                    return fileData;
                }
                catch (Exception ex)
                {
                    RecordError(location.ChannelId, ex.Message);
                    lastException = ex;
                    continue;
                }
            }

            throw lastException ?? new InvalidOperationException("All download attempts failed");
        }

        public async Task<bool> TestChannelHealthAsync(long channelId)
        {
            try
            {
                var startTime = DateTime.Now;
                var url = $"https://api.telegram.org/bot{_botToken}/getChat";
                var content = new StringContent(
                    JsonSerializer.Serialize(new { chat_id = channelId }),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(url, content);
                var responseTime = DateTime.Now - startTime;

                var isHealthy = response.IsSuccessStatusCode;
                UpdateChannelHealth(channelId, isHealthy, responseTime, isHealthy ? null : $"HTTP {response.StatusCode}");

                return isHealthy;
            }
            catch (Exception ex)
            {
                UpdateChannelHealth(channelId, false, TimeSpan.Zero, ex.Message);
                return false;
            }
        }

        public async Task<int> RepairMissingCopiesAsync()
        {
            if (!_redundancyConfig.AutoRepair)
                return 0;

            // This would need to be implemented with a proper database
            // For now, return 0 as a placeholder
            return 0;
        }

        public Dictionary<long, ChannelHealth> GetChannelStatistics()
        {
            return new Dictionary<long, ChannelHealth>(_channelHealth);
        }

        public List<ChannelConfig> GetActiveChannels()
        {
            return _channels.Where(c => c.IsActive).ToList();
        }

        public List<ChannelConfig> GetHealthyChannels()
        {
            return _channels.Where(c => c.IsActive && IsChannelHealthy(c.Id)).ToList();
        }

        private async Task<(long messageId, string fileId)> UploadToChannelAsync(long channelId, byte[] fileData, string fileName)
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendDocument";
            
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(channelId.ToString()), "chat_id");
            form.Add(new ByteArrayContent(fileData), "document", $"{fileName}.encrypted");
            form.Add(new StringContent($"🔐 TSCloud: {fileName} ({fileData.Length:N0} bytes)"), "caption");
            
            var response = await _httpClient.PostAsync(url, form);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (!result.GetProperty("ok").GetBoolean())
            {
                throw new InvalidOperationException($"Telegram API error: {result.GetProperty("description").GetString()}");
            }
            
            var message = result.GetProperty("result");
            var messageId = message.GetProperty("message_id").GetInt64();
            var document = message.GetProperty("document");
            var fileId = document.GetProperty("file_id").GetString() ?? "";
            
            return (messageId, fileId);
        }

        private async Task<byte[]> DownloadFromChannelAsync(long channelId, string fileId)
        {
            // First get file info
            var getFileUrl = $"https://api.telegram.org/bot{_botToken}/getFile";
            var getFileContent = new StringContent(
                JsonSerializer.Serialize(new { file_id = fileId }),
                Encoding.UTF8,
                "application/json"
            );

            var getFileResponse = await _httpClient.PostAsync(getFileUrl, getFileContent);
            getFileResponse.EnsureSuccessStatusCode();

            var getFileResponseContent = await getFileResponse.Content.ReadAsStringAsync();
            var getFileResult = JsonSerializer.Deserialize<JsonElement>(getFileResponseContent);

            if (!getFileResult.GetProperty("ok").GetBoolean())
            {
                throw new InvalidOperationException("Failed to get file info");
            }

            var filePath = getFileResult.GetProperty("result").GetProperty("file_path").GetString();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new InvalidOperationException("No file path in response");
            }

            // Download the file
            var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";
            var downloadResponse = await _httpClient.GetAsync(downloadUrl);
            downloadResponse.EnsureSuccessStatusCode();

            return await downloadResponse.Content.ReadAsByteArrayAsync();
        }

        private List<ChannelConfig> SelectChannelsForUpload(List<ChannelConfig> healthyChannels, int count)
        {
            var selected = new List<ChannelConfig>();

            // Use round-robin selection
            for (int i = 0; i < count && healthyChannels.Count > 0; i++)
            {
                var channelIndex = _currentChannelIndex % healthyChannels.Count;
                selected.Add(healthyChannels[channelIndex]);
                _currentChannelIndex = (channelIndex + 1) % healthyChannels.Count;
            }

            return selected;
        }

        private bool IsChannelHealthy(long channelId)
        {
            return _channelHealth.TryGetValue(channelId, out var health) && health.IsHealthy;
        }

        private void RecordSuccess(long channelId)
        {
            if (_channelHealth.TryGetValue(channelId, out var health))
            {
                health.SuccessCount++;
                health.IsHealthy = true;
                health.LastError = null;
            }
        }

        private void RecordError(long channelId, string error)
        {
            if (_channelHealth.TryGetValue(channelId, out var health))
            {
                health.ErrorCount++;
                health.LastError = error;
                
                // Mark as unhealthy if too many errors
                if (health.ErrorCount > 5)
                {
                    health.IsHealthy = false;
                }
            }
        }

        private void UpdateChannelHealth(long channelId, bool isHealthy, TimeSpan responseTime, string? error)
        {
            if (_channelHealth.TryGetValue(channelId, out var health))
            {
                var wasHealthy = health.IsHealthy;
                health.IsHealthy = isHealthy;
                health.LastCheck = DateTime.Now;
                health.ResponseTime = responseTime;
                health.LastError = error;

                if (isHealthy)
                {
                    health.SuccessCount++;
                }
                else
                {
                    health.ErrorCount++;
                }

                if (wasHealthy != isHealthy)
                {
                    ChannelHealthChanged?.Invoke(this, new ChannelHealthChangedEventArgs
                    {
                        ChannelId = channelId,
                        IsHealthy = isHealthy,
                        Error = error
                    });
                }
            }
        }

        private async Task StartHealthCheckLoop()
        {
            while (!_disposed)
            {
                try
                {
                    foreach (var channel in _channels.Where(c => c.IsActive))
                    {
                        await TestChannelHealthAsync(channel.Id);
                        await Task.Delay(1000); // 1 second between checks
                    }
                    
                    await Task.Delay(TimeSpan.FromMinutes(5)); // Check every 5 minutes
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Health check error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1)); // Wait 1 minute on error
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
        }
    }

    public class ChannelHealthChangedEventArgs : EventArgs
    {
        public long ChannelId { get; set; }
        public bool IsHealthy { get; set; }
        public string? Error { get; set; }
    }

    public class FileUploadedEventArgs : EventArgs
    {
        public FileLocation Location { get; set; } = null!;
        public string ChannelName { get; set; } = "";
    }
}