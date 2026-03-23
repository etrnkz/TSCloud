using System;
using System.Runtime.InteropServices;

namespace TSCloud.Desktop;

public static class NativeMethods
{
    private const string DllName = "secure_cloud_core";

    // Error codes
    public const int SC_SUCCESS = 0;
    public const int SC_ERROR_INVALID_PARAM = -1;
    public const int SC_ERROR_CRYPTO = -2;
    public const int SC_ERROR_DATABASE = -3;
    public const int SC_ERROR_IO = -4;
    public const int SC_ERROR_TELEGRAM = -5;
    public const int SC_ERROR_NOT_FOUND = -6;

    [StructLayout(LayoutKind.Sequential)]
    public struct CSyncStatus
    {
        public uint TotalFiles;
        public ulong TotalSize;
        public uint PendingChunks;
        public ulong LastSync;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CConfig
    {
        public int TelegramApiId;
        public IntPtr TelegramApiHash;
        public ulong TelegramChannelId;
        public IntPtr DatabasePath;
        public uint ChunkSize;
        public int CompressionLevel;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_init_engine(
        ref CConfig config,
        [MarshalAs(UnmanagedType.LPStr)] string password,
        byte[] salt,
        uint saltLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_add_file(
        int engineId,
        [MarshalAs(UnmanagedType.LPStr)] string filePath,
        IntPtr fileIdOut,
        uint fileIdLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_download_file(
        int engineId,
        [MarshalAs(UnmanagedType.LPStr)] string fileId,
        [MarshalAs(UnmanagedType.LPStr)] string outputPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_get_sync_status(
        int engineId,
        out CSyncStatus statusOut);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_sync_pending_uploads(int engineId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_start_folder_watching(
        int engineId,
        [MarshalAs(UnmanagedType.LPStr)] string folderPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_generate_salt(byte[] saltOut, uint saltLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_cleanup_engine(int engineId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int sc_get_last_error(IntPtr errorOut, uint errorLen);
}