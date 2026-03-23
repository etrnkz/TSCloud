using System;

namespace TSCloud.Desktop.Models
{
    public class VersionedFile
    {
        public string FileId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string OriginalPath { get; set; } = "";
        public DateTime FirstUploaded { get; set; }
        public DateTime LastModified { get; set; }
        public int TotalVersions { get; set; }
        public int CurrentVersionNumber { get; set; }
        public long TotalSize { get; set; }
        public bool AutoVersioning { get; set; } = true;
        public int MaxVersions { get; set; } = 10;
    }
}