using Microsoft.Extensions.Logging;
using DatasiteUploader.Models;

namespace DatasiteUploader.Services;

/// <summary>
/// Service for managing upload destinations with hierarchical browsing
/// </summary>
public sealed class DestinationService : IDestinationService
{
    private readonly IDatasiteApiService _apiService;
    private readonly ILogger<DestinationService> _logger;

    public DestinationService(IDatasiteApiService apiService, ILogger<DestinationService> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets filerooms for initial navigation (fast initialization)
    /// </summary>
    public async Task<(bool Success, List<Destination> Filerooms, string? ErrorMessage)> GetFileroomsAsync(
        Project project, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(project);
            
            _logger.LogInformation("Getting filerooms for project: {ProjectName} ({ProjectId})", 
                project.Name, project.Id);

            var destinations = new List<Destination>();

            // Get filerooms only (no subfolders)
            var (fileroomsSuccess, filerooms, fileroomsError) = await _apiService.GetFileroomsAsync(
                project.Id, cancellationToken);

            if (!fileroomsSuccess)
            {
                _logger.LogError("Failed to fetch filerooms for project {ProjectId}: {Error}", 
                    project.Id, fileroomsError);
                return (false, destinations, fileroomsError);
            }

            if (!filerooms.Any())
            {
                _logger.LogWarning("No filerooms found in project: {ProjectName}", project.Name);
                return (true, destinations, "No filerooms available in this project");
            }

            // Add each fileroom as a destination
            foreach (var fileroom in filerooms)
            {
                var fileroomDestination = new Destination
                {
                    Type = "Fileroom",
                    Name = fileroom.Name,
                    Id = fileroom.Id,
                    ProjectId = project.Id,
                    Path = fileroom.Name,
                    CanUpload = true
                };
                destinations.Add(fileroomDestination);
                _logger.LogDebug("Added fileroom destination: {FileroomName}", fileroom.Name);
            }

            _logger.LogInformation("Successfully loaded {FileroomCount} filerooms for project {ProjectName}", 
                destinations.Count, project.Name);

            return (true, destinations, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error getting filerooms for project: {ex.Message}";
            _logger.LogError(ex, "Unexpected error getting filerooms for project {ProjectId}", project.Id);
            return (false, new List<Destination>(), errorMessage);
        }
    }

    /// <summary>
    /// Gets folders within a specific parent (fileroom or folder) for hierarchical navigation
    /// </summary>
    public async Task<(bool Success, List<Destination> Folders, string? ErrorMessage)> GetFoldersAsync(
        Destination parentDestination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(parentDestination);
            
            _logger.LogInformation("Getting folders in: {ParentPath} ({ParentId})", 
                parentDestination.Path, parentDestination.Id);

            var folders = new List<Destination>();

            var (success, children, errorMessage) = await _apiService.GetMetadataChildrenAsync(
                parentDestination.ProjectId, parentDestination.Id, cancellationToken);

            if (!success)
            {
                _logger.LogError("Failed to get folders in {ParentPath}: {Error}", 
                    parentDestination.Path, errorMessage);
                return (false, folders, errorMessage);
            }

            // Filter to only folders and create destinations
            var folderItems = children.Where(item => item.IsFolder).ToList();
            
            foreach (var folder in folderItems)
            {
                var folderDestination = new Destination
                {
                    Type = "Folder",
                    Name = folder.Name,
                    Id = folder.Id,
                    ProjectId = parentDestination.ProjectId,
                    Path = $"{parentDestination.Path}/{folder.Name}",
                    ParentId = parentDestination.Id,
                    CanUpload = true
                };
                folders.Add(folderDestination);
                _logger.LogDebug("Added folder destination: {FolderPath}", folderDestination.Path);
            }

            _logger.LogInformation("Successfully loaded {FolderCount} folders in {ParentPath}", 
                folders.Count, parentDestination.Path);

            return (true, folders, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error getting folders: {ex.Message}";
            _logger.LogError(ex, "Unexpected error getting folders in {ParentPath}", 
                parentDestination?.Path);
            return (false, new List<Destination>(), errorMessage);
        }
    }

    [Obsolete("Use GetFileroomsAsync and GetFoldersAsync for hierarchical navigation")]
    public async Task<(bool Success, List<Destination> Destinations, string? ErrorMessage)> GetDestinationsAsync(
        Project project, 
        CancellationToken cancellationToken = default)
    {
        // For backward compatibility, return filerooms only
        return await GetFileroomsAsync(project, cancellationToken);
    }

    private async Task AddFolderDestinationsAsync(
        string projectId,
        string parentId,
        string currentPath,
        List<Destination> destinations,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        // Prevent infinite recursion and excessive depth
        const int maxDepth = 10;
        if (depth >= maxDepth)
        {
            _logger.LogWarning("Maximum folder depth ({MaxDepth}) reached for path: {Path}", maxDepth, currentPath);
            return;
        }

        try
        {
            var (success, children, errorMessage) = await _apiService.GetMetadataChildrenAsync(
                projectId, parentId, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Failed to get children for parent {ParentId}: {Error}", parentId, errorMessage);
                return;
            }

            // Filter to only folders
            var folders = children.Where(item => item.IsFolder).ToList();

            foreach (var folder in folders)
            {
                var folderPath = $"{currentPath}/{folder.Name}";
                var folderDestination = new Destination
                {
                    Type = "Folder",
                    Name = folder.Name,
                    Id = folder.Id,
                    ProjectId = projectId,
                    Path = folderPath,
                    ParentId = parentId
                };

                destinations.Add(folderDestination);
                _logger.LogDebug("Added folder destination: {FolderPath}", folderPath);

                // Recursively add subfolders
                await AddFolderDestinationsAsync(
                    projectId, 
                    folder.Id, 
                    folderPath, 
                    destinations, 
                    cancellationToken, 
                    depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing folders for parent {ParentId} at path {Path}", 
                parentId, currentPath);
        }
    }

    public async Task<(bool Success, Destination? Destination, string? ErrorMessage)> CreateDestinationAsync(
        Destination parentDestination,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(parentDestination);
            ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

            // Validate folder name
            if (!IsValidFolderName(folderName))
            {
                var validationError = "Invalid folder name. Folder names cannot contain: \\ / : * ? \" < > |";
                _logger.LogError("Invalid folder name provided: {FolderName}", folderName);
                return (false, null, validationError);
            }

            _logger.LogInformation("Creating new folder '{FolderName}' in destination: {ParentPath}", 
                folderName, parentDestination.Path);

            var (success, createdFolder, errorMessage) = await _apiService.CreateFolderAsync(
                parentDestination.ProjectId, 
                parentDestination.Id, 
                folderName, 
                cancellationToken);

            if (!success || createdFolder == null)
            {
                _logger.LogError("Failed to create folder '{FolderName}': {Error}", folderName, errorMessage);
                return (false, null, errorMessage);
            }

            var newDestination = new Destination
            {
                Type = "Folder",
                Name = createdFolder.Name,
                Id = createdFolder.Id,
                ProjectId = parentDestination.ProjectId,
                Path = $"{parentDestination.Path}/{createdFolder.Name}",
                ParentId = parentDestination.Id
            };

            _logger.LogInformation("Successfully created folder destination: {FolderPath}", newDestination.Path);
            return (true, newDestination, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error creating destination folder: {ex.Message}";
            _logger.LogError(ex, "Unexpected error creating folder '{FolderName}' in destination {ParentPath}", 
                folderName, parentDestination?.Path);
            return (false, null, errorMessage);
        }
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateDestinationAsync(
        Destination destination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(destination);

            _logger.LogDebug("Validating destination: {DestinationPath}", destination.Path);

            // Basic validation
            if (string.IsNullOrWhiteSpace(destination.Id) || 
                string.IsNullOrWhiteSpace(destination.ProjectId))
            {
                const string validationError = "Destination has invalid ID or Project ID";
                _logger.LogError("Destination validation failed: {Error}", validationError);
                return (false, validationError);
            }

            // Try to fetch destination metadata to ensure it exists and is accessible
            var (success, children, errorMessage) = await _apiService.GetMetadataChildrenAsync(
                destination.ProjectId, 
                destination.Id, 
                cancellationToken);

            if (!success)
            {
                var validationError = $"Destination is not accessible: {errorMessage}";
                _logger.LogError("Destination validation failed for {DestinationPath}: {Error}", 
                    destination.Path, validationError);
                return (false, validationError);
            }

            _logger.LogDebug("Destination validation successful: {DestinationPath}", destination.Path);
            return (true, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error validating destination: {ex.Message}";
            _logger.LogError(ex, "Unexpected error validating destination {DestinationPath}", 
                destination?.Path);
            return (false, errorMessage);
        }
    }

    private static bool IsValidFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        // Check for invalid characters
        var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        if (folderName.IndexOfAny(invalidChars) >= 0)
            return false;

        // Check for reserved names
        var reservedNames = new[] 
        { 
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        
        if (reservedNames.Contains(folderName.ToUpperInvariant()))
            return false;

        // Check length
        if (folderName.Length > 255)
            return false;

        // Check for leading/trailing periods or spaces
        if (folderName.StartsWith('.') || folderName.EndsWith('.') ||
            folderName.StartsWith(' ') || folderName.EndsWith(' '))
            return false;

        return true;
    }
}