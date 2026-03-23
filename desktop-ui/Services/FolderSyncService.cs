using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TSCloud.Desktop.Models;

namespace TSCloud.Desktop.Services
{
    public class FolderSyncService : IDisposable
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, SyncedFolder> _syncedFolders = new();
        private bool _disposed = false;

        public event EventHandler<FileChangedEventArgs>? FileChanged;
        public event EventHandler<FolderSyncEventArgs>? SyncStatusChanged;

        public IEnumerable<SyncedFolder> GetSyncedFolders() => _syncedFolders.Values;

        public bool AddFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return false;

            if (_syncedFolders.ContainsKey(folderPath))
                return false; // Already added

            try
            {
                var syncedFolder = new SyncedFolder
                {
                    FolderPath = folderPath,
                    IsActive = true,
                    AddedAt = DateTime.Now
                };

                // Calculate initial stats
                UpdateFolderStats(syncedFolder);

                // Create file system watcher
                var watcher = new FileSystemWatcher(folderPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };

                // Subscribe to events
                watcher.Created += (s, e) => OnFileChanged(folderPath, e.FullPath, FileChangeType.Created);
                watcher.Changed += (s, e) => OnFileChanged(folderPath, e.FullPath, FileChangeType.Modified);
                watcher.Deleted += (s, e) => OnFileChanged(folderPath, e.FullPath, FileChangeType.Deleted);
                watcher.Renamed += (s, e) => OnFileChanged(folderPath, e.FullPath, FileChangeType.Renamed, e.OldFullPath);

                _watchers[folderPath] = watcher;
                _syncedFolders[folderPath] = syncedFolder;

                OnSyncStatusChanged(folderPath, $"Folder added and monitoring started");
                return true;
            }
            catch (Exception ex)
            {
                OnSyncStatusChanged(folderPath, $"Error adding folder: {ex.Message}");
                return false;
            }
        }

        public bool RemoveFolder(string folderPath)
        {
            if (!_syncedFolders.ContainsKey(folderPath))
                return false;

            try
            {
                if (_watchers.TryGetValue(folderPath, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    _watchers.Remove(folderPath);
                }

                _syncedFolders.Remove(folderPath);
                OnSyncStatusChanged(folderPath, "Folder removed from sync");
                return true;
            }
            catch (Exception ex)
            {
                OnSyncStatusChanged(folderPath, $"Error removing folder: {ex.Message}");
                return false;
            }
        }

        public void SetFolderActive(string folderPath, bool isActive)
        {
            if (_syncedFolders.TryGetValue(folderPath, out var folder) && 
                _watchers.TryGetValue(folderPath, out var watcher))
            {
                folder.IsActive = isActive;
                watcher.EnableRaisingEvents = isActive;
                
                var status = isActive ? "Monitoring resumed" : "Monitoring paused";
                folder.Status = status;
                OnSyncStatusChanged(folderPath, status);
            }
        }

        public void UpdateFolderStats(SyncedFolder folder)
        {
            try
            {
                if (!Directory.Exists(folder.FolderPath))
                {
                    folder.FileCount = 0;
                    folder.TotalSize = 0;
                    folder.Status = "Folder not found";
                    return;
                }

                var files = Directory.GetFiles(folder.FolderPath, "*", SearchOption.AllDirectories);
                folder.FileCount = files.Length;
                folder.TotalSize = files.Sum(f => new FileInfo(f).Length);
                folder.Status = "Ready";
            }
            catch (Exception ex)
            {
                folder.Status = $"Error: {ex.Message}";
            }
        }

        public async Task ScanFolderForChanges(string folderPath)
        {
            if (!_syncedFolders.TryGetValue(folderPath, out var folder))
                return;

            try
            {
                folder.Status = "Scanning...";
                OnSyncStatusChanged(folderPath, "Scanning folder for changes");

                await Task.Run(() =>
                {
                    var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        // Check if file was modified since last sync
                        if (fileInfo.LastWriteTime > folder.LastSync)
                        {
                            OnFileChanged(folderPath, file, FileChangeType.Modified);
                        }
                    }
                });

                folder.LastSync = DateTime.Now;
                folder.Status = "Scan complete";
                OnSyncStatusChanged(folderPath, "Folder scan completed");
            }
            catch (Exception ex)
            {
                folder.Status = $"Scan error: {ex.Message}";
                OnSyncStatusChanged(folderPath, $"Scan error: {ex.Message}");
            }
        }

        private void OnFileChanged(string folderPath, string filePath, FileChangeType changeType, string? oldPath = null)
        {
            // Ignore temporary files and system files
            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith(".") || fileName.StartsWith("~") || fileName.EndsWith(".tmp"))
                return;

            // Ignore directory changes for now
            if (Directory.Exists(filePath))
                return;

            var args = new FileChangedEventArgs
            {
                FolderPath = folderPath,
                FilePath = filePath,
                ChangeType = changeType,
                OldPath = oldPath,
                Timestamp = DateTime.Now
            };

            FileChanged?.Invoke(this, args);

            // Update folder stats
            if (_syncedFolders.TryGetValue(folderPath, out var folder))
            {
                UpdateFolderStats(folder);
                folder.LastSync = DateTime.Now;
            }
        }

        private void OnSyncStatusChanged(string folderPath, string message)
        {
            var args = new FolderSyncEventArgs
            {
                FolderPath = folderPath,
                Message = message,
                Timestamp = DateTime.Now
            };

            SyncStatusChanged?.Invoke(this, args);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _syncedFolders.Clear();
            _disposed = true;
        }
    }

    public enum FileChangeType
    {
        Created,
        Modified,
        Deleted,
        Renamed
    }

    public class FileChangedEventArgs : EventArgs
    {
        public string FolderPath { get; set; } = "";
        public string FilePath { get; set; } = "";
        public FileChangeType ChangeType { get; set; }
        public string? OldPath { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class FolderSyncEventArgs : EventArgs
    {
        public string FolderPath { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}