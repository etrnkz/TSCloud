using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SecureCloud.Desktop.Models;
using System.Text.Json;

namespace SecureCloud.Desktop.Services
{
    public class VersioningService : IDisposable
    {
        private readonly Dictionary<string, VersionedFile> _versionedFiles = new();
        private readonly Dictionary<string, List<FileVersion>> _fileVersions = new();
        private readonly string _versioningDbPath;
        private bool _disposed = false;

        public event EventHandler<VersionCreatedEventArgs>? VersionCreated;
        public event EventHandler<VersionRestoredEventArgs>? VersionRestored;

        public VersioningService(string dbPath = "versioning.json")
        {
            _versioningDbPath = dbPath;
            LoadVersioningData();
        }

        public IEnumerable<VersionedFile> GetVersionedFiles() => _versionedFiles.Values;

        public IEnumerable<FileVersion> GetFileVersions(string fileId)
        {
            return _fileVersions.TryGetValue(fileId, out var versions) 
                ? versions.OrderByDescending(v => v.VersionNumber) 
                : Enumerable.Empty<FileVersion>();
        }

        public async Task<FileVersion?> CreateVersionAsync(
            string filePath, 
            long messageId, 
            string telegramFileId, 
            byte[] nonce, 
            byte[] fileHash,
            long originalSize,
            long encryptedSize,
            string changeDescription = "")
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var fileId = GetOrCreateFileId(filePath);
                
                // Get or create versioned file
                if (!_versionedFiles.TryGetValue(fileId, out var versionedFile))
                {
                    versionedFile = new VersionedFile
                    {
                        FileId = fileId,
                        FileName = fileName,
                        OriginalPath = filePath,
                        FirstUploaded = DateTime.Now,
                        TotalVersions = 0,
                        CurrentVersionNumber = 0
                    };
                    _versionedFiles[fileId] = versionedFile;
                    _fileVersions[fileId] = new List<FileVersion>();
                }

                // Create new version
                var newVersionNumber = versionedFile.CurrentVersionNumber + 1;
                var version = new FileVersion
                {
                    FileName = fileName,
                    FilePath = filePath,
                    VersionNumber = newVersionNumber,
                    Size = originalSize,
                    EncryptedSize = encryptedSize,
                    CreatedAt = DateTime.Now,
                    MessageId = messageId,
                    FileId = telegramFileId,
                    Nonce = nonce,
                    FileHash = fileHash,
                    ChangeDescription = changeDescription,
                    IsCurrentVersion = true,
                    IsAutoVersion = string.IsNullOrEmpty(changeDescription)
                };

                // Mark previous version as not current
                var existingVersions = _fileVersions[fileId];
                foreach (var existingVersion in existingVersions)
                {
                    existingVersion.IsCurrentVersion = false;
                }

                // Add new version
                existingVersions.Add(version);

                // Update versioned file info
                versionedFile.CurrentVersionNumber = newVersionNumber;
                versionedFile.TotalVersions = existingVersions.Count;
                versionedFile.LastModified = DateTime.Now;
                versionedFile.TotalSize = existingVersions.Sum(v => v.EncryptedSize);

                // Clean up old versions if needed
                await CleanupOldVersionsAsync(fileId, versionedFile.MaxVersions);

                // Save to disk
                await SaveVersioningDataAsync();

                // Raise event
                VersionCreated?.Invoke(this, new VersionCreatedEventArgs
                {
                    FileId = fileId,
                    Version = version,
                    VersionedFile = versionedFile
                });

                return version;
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error creating version: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RestoreVersionAsync(string fileId, int versionNumber, string restorePath)
        {
            try
            {
                if (!_fileVersions.TryGetValue(fileId, out var versions))
                    return false;

                var version = versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
                if (version == null)
                    return false;

                // TODO: Download and decrypt the version from Telegram
                // This would integrate with the existing download functionality

                // Raise event
                VersionRestored?.Invoke(this, new VersionRestoredEventArgs
                {
                    FileId = fileId,
                    Version = version,
                    RestorePath = restorePath
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring version: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteVersionAsync(string fileId, int versionNumber)
        {
            try
            {
                if (!_fileVersions.TryGetValue(fileId, out var versions))
                    return false;

                var version = versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
                if (version == null || version.IsCurrentVersion)
                    return false; // Can't delete current version

                versions.Remove(version);

                // Update versioned file info
                if (_versionedFiles.TryGetValue(fileId, out var versionedFile))
                {
                    versionedFile.TotalVersions = versions.Count;
                    versionedFile.TotalSize = versions.Sum(v => v.EncryptedSize);
                }

                await SaveVersioningDataAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting version: {ex.Message}");
                return false;
            }
        }

        public void SetVersioningEnabled(string fileId, bool enabled)
        {
            if (_versionedFiles.TryGetValue(fileId, out var versionedFile))
            {
                versionedFile.AutoVersioning = enabled;
                _ = SaveVersioningDataAsync();
            }
        }

        public void SetMaxVersions(string fileId, int maxVersions)
        {
            if (_versionedFiles.TryGetValue(fileId, out var versionedFile))
            {
                versionedFile.MaxVersions = Math.Max(1, maxVersions);
                _ = CleanupOldVersionsAsync(fileId, versionedFile.MaxVersions);
                _ = SaveVersioningDataAsync();
            }
        }

        private async Task CleanupOldVersionsAsync(string fileId, int maxVersions)
        {
            if (!_fileVersions.TryGetValue(fileId, out var versions))
                return;

            if (versions.Count <= maxVersions)
                return;

            // Keep the most recent versions, delete the oldest
            var versionsToDelete = versions
                .Where(v => !v.IsCurrentVersion)
                .OrderBy(v => v.VersionNumber)
                .Take(versions.Count - maxVersions)
                .ToList();

            foreach (var version in versionsToDelete)
            {
                versions.Remove(version);
                // TODO: Delete from Telegram if needed
            }

            // Update total size
            if (_versionedFiles.TryGetValue(fileId, out var versionedFile))
            {
                versionedFile.TotalVersions = versions.Count;
                versionedFile.TotalSize = versions.Sum(v => v.EncryptedSize);
            }
        }

        private string GetOrCreateFileId(string filePath)
        {
            // Use file path hash as consistent ID
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(normalizedPath))
                .Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }

        private void LoadVersioningData()
        {
            try
            {
                if (!File.Exists(_versioningDbPath))
                    return;

                var json = File.ReadAllText(_versioningDbPath);
                var data = JsonSerializer.Deserialize<VersioningData>(json);
                
                if (data != null)
                {
                    foreach (var kvp in data.VersionedFiles)
                    {
                        _versionedFiles[kvp.Key] = kvp.Value;
                    }
                    
                    foreach (var kvp in data.FileVersions)
                    {
                        _fileVersions[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading versioning data: {ex.Message}");
            }
        }

        private async Task SaveVersioningDataAsync()
        {
            try
            {
                var data = new VersioningData
                {
                    VersionedFiles = _versionedFiles,
                    FileVersions = _fileVersions
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_versioningDbPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving versioning data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _ = SaveVersioningDataAsync();
            _disposed = true;
        }
    }

    public class VersioningData
    {
        public Dictionary<string, VersionedFile> VersionedFiles { get; set; } = new();
        public Dictionary<string, List<FileVersion>> FileVersions { get; set; } = new();
    }

    public class VersionCreatedEventArgs : EventArgs
    {
        public string FileId { get; set; } = "";
        public FileVersion Version { get; set; } = null!;
        public VersionedFile VersionedFile { get; set; } = null!;
    }

    public class VersionRestoredEventArgs : EventArgs
    {
        public string FileId { get; set; } = "";
        public FileVersion Version { get; set; } = null!;
        public string RestorePath { get; set; } = "";
    }
}