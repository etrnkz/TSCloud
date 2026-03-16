using System;

namespace SecureCloud.Desktop.Models;

public class SyncStatus
{
    public uint TotalFiles { get; set; }
    public ulong TotalSize { get; set; }
    public uint PendingChunks { get; set; }
    public DateTime LastSync { get; set; }

    public string TotalSizeFormatted => FormatBytes(TotalSize);
    public bool HasPendingUploads => PendingChunks > 0;

    private static string FormatBytes(ulong bytes)
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
}

public class Config
{
    public int TelegramApiId { get; set; }
    public string TelegramApiHash { get; set; } = string.Empty;
    public long TelegramChannelId { get; set; }
    public string DatabasePath { get; set; } = "secure_cloud.db";
    public uint ChunkSize { get; set; } = 16 * 1024 * 1024; // 16MB
    public int CompressionLevel { get; set; } = 3;
}

public class FileItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsUploaded { get; set; }
    public double UploadProgress { get; set; }

    public string SizeFormatted => FormatBytes((ulong)Size);

    private static string FormatBytes(ulong bytes)
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
}