using System;
using System.ComponentModel;

namespace SecureCloud.Desktop.Models
{
    public class FileVersion : INotifyPropertyChanged
    {
        public string VersionId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int VersionNumber { get; set; }
        public long Size { get; set; }
        public long EncryptedSize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public long MessageId { get; set; }
        public string FileId { get; set; } = "";
        public byte[] Nonce { get; set; } = Array.Empty<byte>();
        public byte[] FileHash { get; set; } = Array.Empty<byte>();
        public string ChangeDescription { get; set; } = "";
        public bool IsCurrentVersion { get; set; }
        public bool IsAutoVersion { get; set; }

        // Computed properties
        public string SizeFormatted => FormatBytes(Size);
        public string EncryptedSizeFormatted => FormatBytes(EncryptedSize);
        public bool IsEncrypted => Nonce.Length > 0;
        public string VersionLabel => $"v{VersionNumber}";
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

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

    public class VersionedFile : INotifyPropertyChanged
    {
        public string FileId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = "";
        public string OriginalPath { get; set; } = "";
        public DateTime FirstUploaded { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public int TotalVersions { get; set; }
        public int CurrentVersionNumber { get; set; }
        public long TotalSize { get; set; }
        public bool IsWatched { get; set; } = true;
        public int MaxVersions { get; set; } = 10;
        public bool AutoVersioning { get; set; } = true;

        // Computed properties
        public string TotalSizeFormatted => FormatBytes(TotalSize);
        public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm:ss");

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