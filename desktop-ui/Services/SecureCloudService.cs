using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SecureCloud.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace SecureCloud.Desktop.Services;

public class SecureCloudService : IDisposable
{
    private readonly ILogger<SecureCloudService> _logger;
    private int _engineId = -1;
    private bool _disposed = false;

    public SecureCloudService(ILogger<SecureCloudService> logger)
    {
        _logger = logger;
    }

    public bool Initialize(Config config, string password)
    {
        try
        {
            // Generate salt for new installations or load existing
            var salt = new byte[32];
            var result = NativeMethods.sc_generate_salt(salt, 32);
            if (result != NativeMethods.SC_SUCCESS)
            {
                _logger.LogError("Failed to generate salt: {ErrorCode}", result);
                return false;
            }

            // Convert managed config to native config
            var apiHashPtr = Marshal.StringToHGlobalAnsi(config.TelegramApiHash);
            var dbPathPtr = Marshal.StringToHGlobalAnsi(config.DatabasePath);

            try
            {
                var nativeConfig = new NativeMethods.CConfig
                {
                    TelegramApiId = config.TelegramApiId,
                    TelegramApiHash = apiHashPtr,
                    TelegramChannelId = (ulong)config.TelegramChannelId,
                    DatabasePath = dbPathPtr,
                    ChunkSize = config.ChunkSize,
                    CompressionLevel = config.CompressionLevel
                };

                _engineId = NativeMethods.sc_init_engine(ref nativeConfig, password, salt, 32);
                
                if (_engineId < 0)
                {
                    _logger.LogError("Failed to initialize engine: {ErrorCode}", _engineId);
                    return false;
                }

                _logger.LogInformation("SecureCloud engine initialized with ID: {EngineId}", _engineId);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(apiHashPtr);
                Marshal.FreeHGlobal(dbPathPtr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during engine initialization");
            return false;
        }
    }

    public async Task<string?> AddFileAsync(string filePath)
    {
        if (_engineId < 0)
        {
            _logger.LogError("Engine not initialized");
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                var fileIdBuffer = Marshal.AllocHGlobal(256);
                try
                {
                    var result = NativeMethods.sc_add_file(_engineId, filePath, fileIdBuffer, 256);
                    if (result != NativeMethods.SC_SUCCESS)
                    {
                        _logger.LogError("Failed to add file {FilePath}: {ErrorCode}", filePath, result);
                        return null;
                    }

                    var fileId = Marshal.PtrToStringAnsi(fileIdBuffer);
                    _logger.LogInformation("Added file {FilePath} with ID: {FileId}", filePath, fileId);
                    return fileId;
                }
                finally
                {
                    Marshal.FreeHGlobal(fileIdBuffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception adding file {FilePath}", filePath);
                return null;
            }
        });
    }

    public async Task<bool> DownloadFileAsync(string fileId, string outputPath)
    {
        if (_engineId < 0)
        {
            _logger.LogError("Engine not initialized");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = NativeMethods.sc_download_file(_engineId, fileId, outputPath);
                if (result != NativeMethods.SC_SUCCESS)
                {
                    _logger.LogError("Failed to download file {FileId}: {ErrorCode}", fileId, result);
                    return false;
                }

                _logger.LogInformation("Downloaded file {FileId} to {OutputPath}", fileId, outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception downloading file {FileId}", fileId);
                return false;
            }
        });
    }

    public async Task<SyncStatus?> GetSyncStatusAsync()
    {
        if (_engineId < 0)
        {
            _logger.LogError("Engine not initialized");
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = NativeMethods.sc_get_sync_status(_engineId, out var nativeStatus);
                if (result != NativeMethods.SC_SUCCESS)
                {
                    _logger.LogError("Failed to get sync status: {ErrorCode}", result);
                    return null;
                }

                return new SyncStatus
                {
                    TotalFiles = nativeStatus.TotalFiles,
                    TotalSize = nativeStatus.TotalSize,
                    PendingChunks = nativeStatus.PendingChunks,
                    LastSync = DateTimeOffset.FromUnixTimeSeconds((long)nativeStatus.LastSync).DateTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting sync status");
                return null;
            }
        });
    }

    public async Task<bool> SyncPendingUploadsAsync()
    {
        if (_engineId < 0)
        {
            _logger.LogError("Engine not initialized");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = NativeMethods.sc_sync_pending_uploads(_engineId);
                if (result != NativeMethods.SC_SUCCESS)
                {
                    _logger.LogError("Failed to sync pending uploads: {ErrorCode}", result);
                    return false;
                }

                _logger.LogInformation("Successfully synced pending uploads");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception syncing pending uploads");
                return false;
            }
        });
    }

    public async Task<bool> StartFolderWatchingAsync(string folderPath)
    {
        if (_engineId < 0)
        {
            _logger.LogError("Engine not initialized");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                var result = NativeMethods.sc_start_folder_watching(_engineId, folderPath);
                if (result != NativeMethods.SC_SUCCESS)
                {
                    _logger.LogError("Failed to start folder watching for {FolderPath}: {ErrorCode}", folderPath, result);
                    return false;
                }

                _logger.LogInformation("Started watching folder: {FolderPath}", folderPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception starting folder watching for {FolderPath}", folderPath);
                return false;
            }
        });
    }

    public void Dispose()
    {
        if (!_disposed && _engineId >= 0)
        {
            try
            {
                NativeMethods.sc_cleanup_engine(_engineId);
                _logger.LogInformation("Cleaned up engine {EngineId}", _engineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during engine cleanup");
            }
            finally
            {
                _engineId = -1;
                _disposed = true;
            }
        }
    }
}