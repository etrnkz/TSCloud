using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SecureCloud.Desktop.Models;
using SecureCloud.Desktop.Services;

namespace SecureCloud.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SecureCloudService _secureCloudService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private SyncStatus? _syncStatus;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<FileItem> _files = new();

    [ObservableProperty]
    private ObservableCollection<string> _watchedFolders = new();

    public MainViewModel(SecureCloudService secureCloudService, ILogger<MainViewModel> logger)
    {
        _secureCloudService = secureCloudService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized)
            return;

        IsLoading = true;
        StatusMessage = "Initializing...";

        try
        {
            // This would typically come from a configuration dialog
            var config = new Config
            {
                TelegramApiId = 12345, // Replace with actual values
                TelegramApiHash = "your_api_hash",
                TelegramChannelId = -1001234567890,
                DatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecureCloud", "database.db"),
                ChunkSize = 16 * 1024 * 1024,
                CompressionLevel = 3
            };

            // This would typically come from a password dialog
            var password = "user_password"; // Replace with actual password input

            var success = _secureCloudService.Initialize(config, password);
            if (success)
            {
                IsInitialized = true;
                StatusMessage = "Initialized successfully";
                await RefreshSyncStatusAsync();
            }
            else
            {
                StatusMessage = "Failed to initialize";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initialization");
            StatusMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        if (!IsInitialized)
        {
            StatusMessage = "Please initialize first";
            return;
        }

        // This would typically open a file dialog
        var filePath = @"C:\path\to\your\file.txt"; // Replace with actual file selection

        if (!File.Exists(filePath))
        {
            StatusMessage = "File not found";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Adding file: {Path.GetFileName(filePath)}";

        try
        {
            var fileId = await _secureCloudService.AddFileAsync(filePath);
            if (fileId != null)
            {
                var fileInfo = new FileInfo(filePath);
                Files.Add(new FileItem
                {
                    Id = fileId,
                    Name = fileInfo.Name,
                    Path = filePath,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsUploaded = true,
                    UploadProgress = 100.0
                });

                StatusMessage = $"File added successfully: {fileInfo.Name}";
                await RefreshSyncStatusAsync();
            }
            else
            {
                StatusMessage = "Failed to add file";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file {FilePath}", filePath);
            StatusMessage = $"Error adding file: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync(FileItem? fileItem)
    {
        if (!IsInitialized || fileItem == null)
            return;

        // This would typically open a save dialog
        var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileItem.Name);

        IsLoading = true;
        StatusMessage = $"Downloading: {fileItem.Name}";

        try
        {
            var success = await _secureCloudService.DownloadFileAsync(fileItem.Id, outputPath);
            if (success)
            {
                StatusMessage = $"Downloaded: {fileItem.Name}";
            }
            else
            {
                StatusMessage = "Download failed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileItem.Id);
            StatusMessage = $"Download error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddWatchedFolderAsync()
    {
        if (!IsInitialized)
        {
            StatusMessage = "Please initialize first";
            return;
        }

        // This would typically open a folder dialog
        var folderPath = @"C:\path\to\watch"; // Replace with actual folder selection

        if (!Directory.Exists(folderPath))
        {
            StatusMessage = "Folder not found";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Adding watched folder: {folderPath}";

        try
        {
            var success = await _secureCloudService.StartFolderWatchingAsync(folderPath);
            if (success)
            {
                WatchedFolders.Add(folderPath);
                StatusMessage = $"Now watching: {folderPath}";
            }
            else
            {
                StatusMessage = "Failed to add watched folder";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding watched folder {FolderPath}", folderPath);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncPendingUploadsAsync()
    {
        if (!IsInitialized)
            return;

        IsLoading = true;
        StatusMessage = "Syncing pending uploads...";

        try
        {
            var success = await _secureCloudService.SyncPendingUploadsAsync();
            if (success)
            {
                StatusMessage = "Sync completed successfully";
                await RefreshSyncStatusAsync();
            }
            else
            {
                StatusMessage = "Sync failed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing pending uploads");
            StatusMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSyncStatusAsync()
    {
        if (!IsInitialized)
            return;

        try
        {
            SyncStatus = await _secureCloudService.GetSyncStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing sync status");
        }
    }
}