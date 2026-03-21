using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Timers;

namespace SecureCloud.Desktop.Services
{
    public class AnalyticsService : IDisposable
    {
        private readonly Dictionary<DateTime, DailyStats> _dailyStats = new();
        private readonly List<PerformanceMetric> _performanceMetrics = new();
        private readonly List<SecurityEvent> _securityEvents = new();
        private readonly string _analyticsDbPath;
        private readonly Timer _metricsTimer;
        private bool _disposed = false;

        public event EventHandler<AnalyticsUpdatedEventArgs>? AnalyticsUpdated;

        public AnalyticsService(string dbPath = "analytics.json")
        {
            _analyticsDbPath = dbPath;
            LoadAnalyticsData();
            
            // Update metrics every minute
            _metricsTimer = new Timer(60000);
            _metricsTimer.Elapsed += UpdateMetrics;
            _metricsTimer.Start();
        }

        public void RecordFileUpload(long fileSize, TimeSpan uploadTime, bool encrypted)
        {
            var today = DateTime.Today;
            if (!_dailyStats.TryGetValue(today, out var stats))
            {
                stats = new DailyStats { Date = today };
                _dailyStats[today] = stats;
            }

            stats.FilesUploaded++;
            stats.BytesUploaded += fileSize;
            stats.EncryptedFilesUploaded += encrypted ? 1 : 0;
            
            // Record performance metric
            _performanceMetrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.Now,
                Type = MetricType.UploadSpeed,
                Value = fileSize / uploadTime.TotalSeconds,
                FileSize = fileSize
            });

            // Keep only last 1000 metrics
            if (_performanceMetrics.Count > 1000)
            {
                _performanceMetrics.RemoveRange(0, _performanceMetrics.Count - 1000);
            }

            _ = SaveAnalyticsDataAsync();
            OnAnalyticsUpdated();
        }

        public void RecordFileDownload(long fileSize, TimeSpan downloadTime, bool encrypted)
        {
            var today = DateTime.Today;
            if (!_dailyStats.TryGetValue(today, out var stats))
            {
                stats = new DailyStats { Date = today };
                _dailyStats[today] = stats;
            }

            stats.FilesDownloaded++;
            stats.BytesDownloaded += fileSize;
            stats.EncryptedFilesDownloaded += encrypted ? 1 : 0;

            _performanceMetrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.Now,
                Type = MetricType.DownloadSpeed,
                Value = fileSize / downloadTime.TotalSeconds,
                FileSize = fileSize
            });

            if (_performanceMetrics.Count > 1000)
            {
                _performanceMetrics.RemoveRange(0, _performanceMetrics.Count - 1000);
            }

            _ = SaveAnalyticsDataAsync();
            OnAnalyticsUpdated();
        }

        public void RecordSyncOperation(int filesProcessed, long totalSize, TimeSpan duration)
        {
            var today = DateTime.Today;
            if (!_dailyStats.TryGetValue(today, out var stats))
            {
                stats = new DailyStats { Date = today };
                _dailyStats[today] = stats;
            }

            stats.SyncOperations++;
            stats.FilesSynced += filesProcessed;
            stats.BytesSynced += totalSize;

            _performanceMetrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.Now,
                Type = MetricType.SyncSpeed,
                Value = totalSize / duration.TotalSeconds,
                FileSize = totalSize
            });

            _ = SaveAnalyticsDataAsync();
            OnAnalyticsUpdated();
        }

        public void RecordSecurityEvent(SecurityEventType eventType, string description, string? filePath = null)
        {
            _securityEvents.Add(new SecurityEvent
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Description = description,
                FilePath = filePath
            });

            // Keep only last 500 security events
            if (_securityEvents.Count > 500)
            {
                _securityEvents.RemoveRange(0, _securityEvents.Count - 500);
            }

            _ = SaveAnalyticsDataAsync();
            OnAnalyticsUpdated();
        }

        public AnalyticsSummary GetSummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today;

            var relevantStats = _dailyStats.Values
                .Where(s => s.Date >= startDate && s.Date <= endDate)
                .ToList();

            var recentMetrics = _performanceMetrics
                .Where(m => m.Timestamp >= startDate && m.Timestamp <= endDate)
                .ToList();

            return new AnalyticsSummary
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalFilesUploaded = relevantStats.Sum(s => s.FilesUploaded),
                TotalFilesDownloaded = relevantStats.Sum(s => s.FilesDownloaded),
                TotalFilesSynced = relevantStats.Sum(s => s.FilesSynced),
                TotalBytesUploaded = relevantStats.Sum(s => s.BytesUploaded),
                TotalBytesDownloaded = relevantStats.Sum(s => s.BytesDownloaded),
                TotalBytesSynced = relevantStats.Sum(s => s.BytesSynced),
                EncryptedFilesUploaded = relevantStats.Sum(s => s.EncryptedFilesUploaded),
                EncryptedFilesDownloaded = relevantStats.Sum(s => s.EncryptedFilesDownloaded),
                SyncOperations = relevantStats.Sum(s => s.SyncOperations),
                AverageUploadSpeed = recentMetrics.Where(m => m.Type == MetricType.UploadSpeed).Average(m => m.Value),
                AverageDownloadSpeed = recentMetrics.Where(m => m.Type == MetricType.DownloadSpeed).Average(m => m.Value),
                AverageSyncSpeed = recentMetrics.Where(m => m.Type == MetricType.SyncSpeed).Average(m => m.Value),
                DailyStats = relevantStats.OrderBy(s => s.Date).ToList(),
                RecentSecurityEvents = _securityEvents.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate).OrderByDescending(e => e.Timestamp).Take(20).ToList()
            };
        }

        public List<PerformanceMetric> GetPerformanceMetrics(MetricType? type = null, DateTime? since = null)
        {
            var query = _performanceMetrics.AsQueryable();
            
            if (type.HasValue)
                query = query.Where(m => m.Type == type.Value);
                
            if (since.HasValue)
                query = query.Where(m => m.Timestamp >= since.Value);
                
            return query.OrderByDescending(m => m.Timestamp).ToList();
        }

        public SystemHealthStatus GetSystemHealth()
        {
            var recentMetrics = _performanceMetrics.Where(m => m.Timestamp >= DateTime.Now.AddHours(-1)).ToList();
            var recentSecurityEvents = _securityEvents.Where(e => e.Timestamp >= DateTime.Now.AddHours(-24)).ToList();

            var avgUploadSpeed = recentMetrics.Where(m => m.Type == MetricType.UploadSpeed).DefaultIfEmpty().Average(m => m?.Value ?? 0);
            var avgDownloadSpeed = recentMetrics.Where(m => m.Type == MetricType.DownloadSpeed).DefaultIfEmpty().Average(m => m?.Value ?? 0);

            var criticalEvents = recentSecurityEvents.Count(e => e.EventType == SecurityEventType.EncryptionFailure || e.EventType == SecurityEventType.IntegrityCheckFailed);

            return new SystemHealthStatus
            {
                OverallHealth = criticalEvents == 0 ? HealthLevel.Good : HealthLevel.Warning,
                UploadPerformance = avgUploadSpeed > 100000 ? HealthLevel.Good : avgUploadSpeed > 50000 ? HealthLevel.Warning : HealthLevel.Poor,
                DownloadPerformance = avgDownloadSpeed > 200000 ? HealthLevel.Good : avgDownloadSpeed > 100000 ? HealthLevel.Warning : HealthLevel.Poor,
                SecurityStatus = criticalEvents == 0 ? HealthLevel.Good : criticalEvents < 5 ? HealthLevel.Warning : HealthLevel.Poor,
                LastUpdated = DateTime.Now,
                CriticalEventsCount = criticalEvents,
                AverageUploadSpeed = avgUploadSpeed,
                AverageDownloadSpeed = avgDownloadSpeed
            };
        }

        private void UpdateMetrics(object? sender, ElapsedEventArgs e)
        {
            // Record system metrics
            var memoryUsage = GC.GetTotalMemory(false);
            
            _performanceMetrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.Now,
                Type = MetricType.MemoryUsage,
                Value = memoryUsage
            });

            OnAnalyticsUpdated();
        }

        private void LoadAnalyticsData()
        {
            try
            {
                if (!File.Exists(_analyticsDbPath))
                    return;

                var json = File.ReadAllText(_analyticsDbPath);
                var data = JsonSerializer.Deserialize<AnalyticsData>(json);
                
                if (data != null)
                {
                    foreach (var kvp in data.DailyStats)
                    {
                        _dailyStats[kvp.Key] = kvp.Value;
                    }
                    
                    _performanceMetrics.AddRange(data.PerformanceMetrics);
                    _securityEvents.AddRange(data.SecurityEvents);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading analytics data: {ex.Message}");
            }
        }

        private async Task SaveAnalyticsDataAsync()
        {
            try
            {
                var data = new AnalyticsData
                {
                    DailyStats = _dailyStats,
                    PerformanceMetrics = _performanceMetrics,
                    SecurityEvents = _securityEvents
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_analyticsDbPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving analytics data: {ex.Message}");
            }
        }

        private void OnAnalyticsUpdated()
        {
            AnalyticsUpdated?.Invoke(this, new AnalyticsUpdatedEventArgs
            {
                Timestamp = DateTime.Now
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _metricsTimer?.Stop();
            _metricsTimer?.Dispose();
            _ = SaveAnalyticsDataAsync();
            _disposed = true;
        }
    }

    // Data Models
    public class DailyStats
    {
        public DateTime Date { get; set; }
        public int FilesUploaded { get; set; }
        public int FilesDownloaded { get; set; }
        public int FilesSynced { get; set; }
        public long BytesUploaded { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesSynced { get; set; }
        public int EncryptedFilesUploaded { get; set; }
        public int EncryptedFilesDownloaded { get; set; }
        public int SyncOperations { get; set; }
    }

    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public MetricType Type { get; set; }
        public double Value { get; set; }
        public long FileSize { get; set; }
    }

    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public SecurityEventType EventType { get; set; }
        public string Description { get; set; } = "";
        public string? FilePath { get; set; }
    }

    public class AnalyticsSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalFilesUploaded { get; set; }
        public int TotalFilesDownloaded { get; set; }
        public int TotalFilesSynced { get; set; }
        public long TotalBytesUploaded { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public long TotalBytesSynced { get; set; }
        public int EncryptedFilesUploaded { get; set; }
        public int EncryptedFilesDownloaded { get; set; }
        public int SyncOperations { get; set; }
        public double AverageUploadSpeed { get; set; }
        public double AverageDownloadSpeed { get; set; }
        public double AverageSyncSpeed { get; set; }
        public List<DailyStats> DailyStats { get; set; } = new();
        public List<SecurityEvent> RecentSecurityEvents { get; set; } = new();
    }

    public class SystemHealthStatus
    {
        public HealthLevel OverallHealth { get; set; }
        public HealthLevel UploadPerformance { get; set; }
        public HealthLevel DownloadPerformance { get; set; }
        public HealthLevel SecurityStatus { get; set; }
        public DateTime LastUpdated { get; set; }
        public int CriticalEventsCount { get; set; }
        public double AverageUploadSpeed { get; set; }
        public double AverageDownloadSpeed { get; set; }
    }

    public class AnalyticsData
    {
        public Dictionary<DateTime, DailyStats> DailyStats { get; set; } = new();
        public List<PerformanceMetric> PerformanceMetrics { get; set; } = new();
        public List<SecurityEvent> SecurityEvents { get; set; } = new();
    }

    // Enums
    public enum MetricType
    {
        UploadSpeed,
        DownloadSpeed,
        SyncSpeed,
        MemoryUsage,
        CpuUsage,
        DiskUsage
    }

    public enum SecurityEventType
    {
        EncryptionSuccess,
        EncryptionFailure,
        DecryptionSuccess,
        DecryptionFailure,
        IntegrityCheckPassed,
        IntegrityCheckFailed,
        UnauthorizedAccess,
        PasswordChanged,
        LoginAttempt,
        LoginSuccess,
        LoginFailure
    }

    public enum HealthLevel
    {
        Good,
        Warning,
        Poor,
        Critical
    }

    // Event Args
    public class AnalyticsUpdatedEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
    }
}