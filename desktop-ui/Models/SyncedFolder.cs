using System;
using System.ComponentModel;
using System.IO;

namespace TSCloud.Desktop.Models
{
    public class SyncedFolder : INotifyPropertyChanged
    {
        private bool _isActive;
        private DateTime _lastSync;
        private int _fileCount;
        private long _totalSize;
        private string _status = "Ready";

        public string FolderPath { get; set; } = "";
        public string FolderName => Path.GetFileName(FolderPath);
        public DateTime AddedAt { get; set; } = DateTime.Now;
        
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public DateTime LastSync
        {
            get => _lastSync;
            set
            {
                _lastSync = value;
                OnPropertyChanged(nameof(LastSync));
                OnPropertyChanged(nameof(LastSyncFormatted));
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set
            {
                _fileCount = value;
                OnPropertyChanged(nameof(FileCount));
            }
        }

        public long TotalSize
        {
            get => _totalSize;
            set
            {
                _totalSize = value;
                OnPropertyChanged(nameof(TotalSize));
                OnPropertyChanged(nameof(TotalSizeFormatted));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        // Computed properties for UI
        public string StatusIcon => IsActive ? "🟢" : "🔴";
        public string LastSyncFormatted => LastSync == DateTime.MinValue ? "Never" : LastSync.ToString("yyyy-MM-dd HH:mm:ss");
        public string TotalSizeFormatted => FormatBytes(TotalSize);

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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}