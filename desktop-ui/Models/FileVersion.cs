using System;
using System.ComponentModel;

namespace TSCloud.Desktop.Models
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
}