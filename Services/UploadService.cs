using Microsoft.Extensions.Logging;
using DatasiteUploader.Models;
using System.Collections.Concurrent;

namespace DatasiteUploader.Services;

/// <summary>
/// Production-ready upload service with comprehensive progress tracking and error handling
/// </summary>
public sealed class UploadService : IUploadService
{
    private readonly IDatasiteApiService _apiService;
    private readonly ILogger<UploadService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeUploads;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private const int MaxConcurrentUploads = 5;

    public UploadService(IDatasiteApiService apiService, ILogger<UploadService> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeUploads = new ConcurrentDictionary<string, CancellationTokenSource>();
        _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentUploads, MaxConcurrentUploads);
    }

    public async Task<(bool Success, string? ErrorMessage)> UploadFileAsync(
        string filePath, 
        Destination destination,
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUploadPath(filePath);
        if (!validation.IsValid)
        {
            _logger.LogError("File validation failed: {Error}", validation.ErrorMessage);
            return (false, validation.ErrorMessage);
        }

        var uploadId = Guid.NewGuid().ToString();
        var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeUploads[uploadId] = uploadCts;

        try
        {
            var statistics = new UploadStatistics
            {
                TotalFiles = 1,
                StartTime = DateTime.Now
            };

            var fileInfo = new FileInfo(filePath);
            statistics.TotalBytes = fileInfo.Length;

            _logger.LogInformation("Starting single file upload: {FileName} ({FileSize:N0} bytes)", 
                fileInfo.Name, fileInfo.Length);

            progress?.Report(statistics);

            await _concurrencyLimiter.WaitAsync(uploadCts.Token);
            
            try
            {
                var fileProgress = new Progress<UploadProgress>(p =>
                {
                    statistics.TransferredBytes = p.BytesTransferred;
                    if (p.IsCompleted)
                    {
                        statistics.CompletedFiles = 1;
                    }
                    else if (p.HasError)
                    {
                        statistics.FailedFiles = 1;
                    }
                    progress?.Report(statistics);
                });

                var (success, _, errorMessage) = await _apiService.UploadFileAsync(
                    filePath, 
                    destination.ProjectId, 
                    destination.Id,
                    null,
                    fileProgress,
                    uploadCts.Token);

                statistics.EndTime = DateTime.Now;
                
                if (success)
                {
                    statistics.CompletedFiles = 1;
                    statistics.TransferredBytes = statistics.TotalBytes;
                    _logger.LogInformation("Successfully uploaded file: {FileName}", fileInfo.Name);
                }
                else
                {
                    statistics.FailedFiles = 1;
                    _logger.LogError("Failed to upload file: {FileName}, Error: {Error}", fileInfo.Name, errorMessage);
                }

                progress?.Report(statistics);
                return (success, errorMessage);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File upload cancelled: {FileName}", Path.GetFileName(filePath));
            return (false, "Upload cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file upload: {FileName}", Path.GetFileName(filePath));
            return (false, $"Unexpected error: {ex.Message}");
        }
        finally
        {
            _activeUploads.TryRemove(uploadId, out _);
            uploadCts.Dispose();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UploadFolderAsync(
        string folderPath, 
        Destination destination,
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUploadPath(folderPath);
        if (!validation.IsValid)
        {
            _logger.LogError("Folder validation failed: {Error}", validation.ErrorMessage);
            return (false, validation.ErrorMessage);
        }

        var uploadId = Guid.NewGuid().ToString();
        var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeUploads[uploadId] = uploadCts;

        try
        {
            var statistics = await GetUploadPreviewAsync(folderPath);
            statistics.StartTime = DateTime.Now;

            _logger.LogInformation("Starting folder upload: {FolderPath} ({FileCount} files, {TotalSize:N0} bytes)", 
                folderPath, statistics.TotalFiles, statistics.TotalBytes);

            progress?.Report(statistics);

            var success = await UploadFolderRecursiveAsync(
                folderPath, 
                destination.ProjectId, 
                destination.Id,
                statistics,
                progress,
                uploadCts.Token);

            statistics.EndTime = DateTime.Now;
            progress?.Report(statistics);

            var hasErrors = statistics.FailedFiles > 0;
            var resultMessage = hasErrors 
                ? $"Upload completed with {statistics.FailedFiles} failed files out of {statistics.TotalFiles} total"
                : null;

            _logger.LogInformation("Folder upload completed: {FolderPath}, Success: {CompletedFiles}/{TotalFiles}, Failed: {FailedFiles}", 
                folderPath, statistics.CompletedFiles, statistics.TotalFiles, statistics.FailedFiles);

            return (success && !hasErrors, resultMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Folder upload cancelled: {FolderPath}", folderPath);
            return (false, "Upload cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during folder upload: {FolderPath}", folderPath);
            return (false, $"Unexpected error: {ex.Message}");
        }
        finally
        {
            _activeUploads.TryRemove(uploadId, out _);
            uploadCts.Dispose();
        }
    }

    private async Task<bool> UploadFolderRecursiveAsync(
        string localFolderPath,
        string projectId,
        string remoteParentId,
        UploadStatistics statistics,
        IProgress<UploadStatistics>? progress,
        CancellationToken cancellationToken)
    {
        var folderName = Path.GetFileName(localFolderPath);
        
        _logger.LogInformation("🗂️ STEP 1: Creating remote folder '{FolderName}' in parent ID '{ParentId}'", folderName, remoteParentId);

        // Create the folder remotely
        var (folderSuccess, remoteFolder, folderError) = await _apiService.CreateFolderAsync(
            projectId, remoteParentId, folderName, cancellationToken);

        if (!folderSuccess || remoteFolder == null)
        {
            _logger.LogError("❌ FAILED: Could not create remote folder '{FolderName}' in parent '{ParentId}': {Error}", 
                folderName, remoteParentId, folderError);
            return false;
        }

        _logger.LogInformation("✅ SUCCESS: Created remote folder '{FolderName}' with new ID '{NewFolderId}'", 
            folderName, remoteFolder.Id);

        var overallSuccess = true;

        // Upload all files in the current folder
        var files = Directory.GetFiles(localFolderPath);
        _logger.LogInformation("📁 STEP 2: Found {FileCount} files to upload to folder '{FolderName}' (ID: {FolderId})", 
            files.Length, folderName, remoteFolder.Id);

        var uploadTasks = new List<Task<bool>>();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            _logger.LogInformation("📤 Uploading file '{FileName}' to folder ID '{FolderId}'", fileName, remoteFolder.Id);
            
            var task = UploadFileWithProgressAsync(filePath, projectId, remoteFolder.Id, statistics, progress, cancellationToken);
            uploadTasks.Add(task);

            // Limit concurrent file uploads
            if (uploadTasks.Count >= MaxConcurrentUploads)
            {
                var completedTask = await Task.WhenAny(uploadTasks);
                var fileSuccess = await completedTask;
                if (!fileSuccess) overallSuccess = false;
                uploadTasks.Remove(completedTask);
            }
        }

        // Wait for remaining file uploads to complete
        var remainingResults = await Task.WhenAll(uploadTasks);
        if (remainingResults.Any(r => !r)) 
        {
            _logger.LogWarning("⚠️ Some file uploads failed in folder '{FolderName}'", folderName);
            overallSuccess = false;
        }
        else if (files.Length > 0)
        {
            _logger.LogInformation("✅ All {FileCount} files uploaded successfully to folder '{FolderName}'", files.Length, folderName);
        }

        // Recursively upload subfolders
        var subfolders = Directory.GetDirectories(localFolderPath);
        _logger.LogInformation("📂 STEP 3: Found {SubfolderCount} subfolders to process in '{FolderName}'", 
            subfolders.Length, folderName);

        foreach (var subfolderPath in subfolders)
        {
            var subfolderName = Path.GetFileName(subfolderPath);
            _logger.LogInformation("🔄 RECURSIVELY processing subfolder '{SubfolderName}' with parent ID '{ParentId}'", 
                subfolderName, remoteFolder.Id);
                
            var subfolderSuccess = await UploadFolderRecursiveAsync(
                subfolderPath, projectId, remoteFolder.Id, statistics, progress, cancellationToken);
            
            if (!subfolderSuccess) 
            {
                _logger.LogError("❌ Subfolder '{SubfolderName}' processing failed", subfolderName);
                overallSuccess = false;
            }
            else
            {
                _logger.LogInformation("✅ Subfolder '{SubfolderName}' processed successfully", subfolderName);
            }
        }

        _logger.LogInformation("🏁 COMPLETED processing folder '{FolderName}' - Success: {Success}", folderName, overallSuccess);
        return overallSuccess;
    }

    private async Task<bool> UploadFileWithProgressAsync(
        string filePath,
        string projectId,
        string destinationId,
        UploadStatistics statistics,
        IProgress<UploadStatistics>? progress,
        CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        
        try
        {
            var fileProgress = new Progress<UploadProgress>(p =>
            {
                // Only update transferred bytes for progress, don't modify file counts here
                // File counts should only be updated when the entire file operation completes
                statistics.TransferredBytes = p.BytesTransferred;
                progress?.Report(statistics);
            });

            var fileName = Path.GetFileName(filePath);
            var (success, uploadedFile, errorMessage) = await _apiService.UploadFileAsync(
                filePath, projectId, destinationId, null, fileProgress, cancellationToken);

            if (!success)
            {
                statistics.FailedFiles++;
                _logger.LogError("❌ FILE UPLOAD FAILED: '{FileName}' to destination '{DestinationId}': {Error}", 
                    fileName, destinationId, errorMessage);
            }
            else
            {
                statistics.CompletedFiles++;
                _logger.LogInformation("✅ FILE UPLOADED: '{FileName}' to destination '{DestinationId}' with file ID '{FileId}'", 
                    fileName, destinationId, uploadedFile?.Id ?? "unknown");
            }

            // Report final progress for this file
            progress?.Report(statistics);
            return success;
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidateUploadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "Path cannot be empty");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return (false, "Path must be fully qualified (absolute path)");
        }

        if (File.Exists(path))
        {
            // Validate file
            if (!_apiService.IsFileTypeAllowed(path))
            {
                var extension = Path.GetExtension(path);
                return (false, $"File type '{extension}' is not allowed");
            }

            if (!_apiService.IsFileSizeAllowed(path))
            {
                var fileSize = new FileInfo(path).Length;
                return (false, $"File size ({fileSize:N0} bytes) exceeds maximum allowed size");
            }

            return (true, null);
        }

        if (Directory.Exists(path))
        {
            // Validate folder
            try
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    if (!_apiService.IsFileTypeAllowed(file))
                    {
                        var extension = Path.GetExtension(file);
                        var relativePath = Path.GetRelativePath(path, file);
                        return (false, $"File '{relativePath}' has disallowed type '{extension}'");
                    }

                    if (!_apiService.IsFileSizeAllowed(file))
                    {
                        var fileSize = new FileInfo(file).Length;
                        var relativePath = Path.GetRelativePath(path, file);
                        return (false, $"File '{relativePath}' size ({fileSize:N0} bytes) exceeds maximum allowed size");
                    }
                }

                return (true, null);
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "Access denied to folder or its contents");
            }
            catch (Exception ex)
            {
                return (false, $"Error validating folder: {ex.Message}");
            }
        }

        return (false, "Path does not exist or is not accessible");
    }

    public async Task<UploadStatistics> GetUploadPreviewAsync(string path)
    {
        var statistics = new UploadStatistics();

        try
        {
            if (File.Exists(path))
            {
                // Single file
                var fileInfo = new FileInfo(path);
                statistics.TotalFiles = 1;
                statistics.TotalBytes = fileInfo.Length;
            }
            else if (Directory.Exists(path))
            {
                // Folder - count all files recursively
                await Task.Run(() =>
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    statistics.TotalFiles = files.Length;
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            statistics.TotalBytes += fileInfo.Length;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not get file info for: {FilePath}", file);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upload preview for path: {Path}", path);
        }

        return statistics;
    }

    public void CancelAllUploads()
    {
        _logger.LogInformation("Cancelling {UploadCount} active uploads", _activeUploads.Count);

        foreach (var kvp in _activeUploads)
        {
            try
            {
                kvp.Value.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling upload: {UploadId}", kvp.Key);
            }
        }

        _activeUploads.Clear();
        _logger.LogInformation("All uploads cancelled");
    }
}