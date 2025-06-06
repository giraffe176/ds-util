using DatasiteUploader.Models;
using DatasiteUploader.Models.Permissions;
using Microsoft.Extensions.Logging;

namespace DatasiteUploader.Services;

/// <summary>
/// Authorization wrapper around existing Datasite services
/// Enforces role-based permissions for all dataroom operations
/// </summary>
public sealed class AuthorizedDataroomService
{
    private readonly IDatasiteApiService _datasiteApiService;
    private readonly IUploadService _uploadService;
    private readonly IDestinationService _destinationService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<AuthorizedDataroomService> _logger;
    private readonly UserContext _userContext;

    public AuthorizedDataroomService(
        IDatasiteApiService datasiteApiService,
        IUploadService uploadService,
        IDestinationService destinationService,
        IPermissionService permissionService,
        ILogger<AuthorizedDataroomService> logger,
        UserContext userContext)
    {
        _datasiteApiService = datasiteApiService ?? throw new ArgumentNullException(nameof(datasiteApiService));
        _uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        _destinationService = destinationService ?? throw new ArgumentNullException(nameof(destinationService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    #region Project Operations

    /// <summary>
    /// Get projects the current user has access to
    /// </summary>
    public async Task<(bool Success, List<Project> Projects, string? ErrorMessage)> GetAccessibleProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (success, allProjects, errorMessage) = await _datasiteApiService.GetProjectsAsync(cancellationToken);
            
            if (!success)
                return (false, new List<Project>(), errorMessage);

            // Filter projects based on user roles
            var accessibleProjects = new List<Project>();
            
            foreach (var project in allProjects)
            {
                var userRoles = await _permissionService.GetUserRolesAsync(_userContext.UserId, project.Id);
                if (userRoles.Any())
                {
                    accessibleProjects.Add(project);
                }
            }

            _logger.LogInformation("User {UserId} has access to {ProjectCount} of {TotalProjects} projects", 
                _userContext.UserId, accessibleProjects.Count, allProjects.Count);

            return (true, accessibleProjects, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible projects for user {UserId}", _userContext.UserId);
            return (false, new List<Project>(), $"Error retrieving projects: {ex.Message}");
        }
    }

    #endregion

    #region Fileroom Operations

    /// <summary>
    /// Get filerooms the current user can access in a project
    /// </summary>
    public async Task<(bool Success, List<Destination> AccessibleFilerooms, string? ErrorMessage)> 
        GetAccessibleFileroomsAsync(Project project, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user has any role in this project
            var userRoles = await _permissionService.GetUserRolesAsync(_userContext.UserId, project.Id);
            if (!userRoles.Any())
            {
                _logger.LogWarning("User {UserId} has no roles in project {ProjectId}", 
                    _userContext.UserId, project.Id);
                return (false, new List<Destination>(), "Access denied: No roles in this project");
            }

            var (success, filerooms, errorMessage) = await _destinationService.GetFileroomsAsync(
                project, cancellationToken);

            if (!success)
                return (false, new List<Destination>(), errorMessage);

            // Filter filerooms based on permissions
            var accessibleFilerooms = new List<Destination>();
            
            foreach (var fileroom in filerooms)
            {
                // Register fileroom in permission system if not already registered
                await EnsureContentRegisteredAsync(fileroom, project.Id);

                var hasAccess = await _permissionService.HasContentPermissionAsync(
                    _userContext.UserId, fileroom.Id, ContentPermissionLevel.View);

                if (hasAccess)
                {
                    accessibleFilerooms.Add(fileroom);
                }
                else
                {
                    _logger.LogDebug("User {UserId} denied access to fileroom {FileroomId}", 
                        _userContext.UserId, fileroom.Id);
                }
            }

            _logger.LogInformation("User {UserId} has access to {AccessibleCount} of {TotalCount} filerooms in project {ProjectId}", 
                _userContext.UserId, accessibleFilerooms.Count, filerooms.Count, project.Id);

            return (true, accessibleFilerooms, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible filerooms for user {UserId} in project {ProjectId}", 
                _userContext.UserId, project.Id);
            return (false, new List<Destination>(), $"Error retrieving filerooms: {ex.Message}");
        }
    }

    #endregion

    #region Folder Operations

    /// <summary>
    /// Get folders the current user can access in a destination
    /// </summary>
    public async Task<(bool Success, List<Destination> AccessibleFolders, string? ErrorMessage)> 
        GetAccessibleFoldersAsync(Destination destination, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check user has access to parent destination
            var hasParentAccess = await _permissionService.HasContentPermissionAsync(
                _userContext.UserId, destination.Id, ContentPermissionLevel.View);

            if (!hasParentAccess)
            {
                _logger.LogWarning("User {UserId} denied access to destination {DestinationId}", 
                    _userContext.UserId, destination.Id);
                return (false, new List<Destination>(), "Access denied: No permission to view this location");
            }

            var (success, folders, errorMessage) = await _destinationService.GetFoldersAsync(
                destination, cancellationToken);

            if (!success)
                return (false, new List<Destination>(), errorMessage);

            // Filter folders based on permissions
            var accessibleFolders = new List<Destination>();
            
            foreach (var folder in folders)
            {
                // Register folder in permission system if not already registered
                await EnsureContentRegisteredAsync(folder, destination.ProjectId);

                var hasAccess = await _permissionService.HasContentPermissionAsync(
                    _userContext.UserId, folder.Id, ContentPermissionLevel.View);

                if (hasAccess)
                {
                    accessibleFolders.Add(folder);
                }
                else
                {
                    _logger.LogDebug("User {UserId} denied access to folder {FolderId}", 
                        _userContext.UserId, folder.Id);
                }
            }

            _logger.LogDebug("User {UserId} has access to {AccessibleCount} of {TotalCount} folders in {DestinationId}", 
                _userContext.UserId, accessibleFolders.Count, folders.Count, destination.Id);

            return (true, accessibleFolders, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible folders for user {UserId} in destination {DestinationId}", 
                _userContext.UserId, destination.Id);
            return (false, new List<Destination>(), $"Error retrieving folders: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a new folder (requires Manage permission)
    /// </summary>
    public async Task<(bool Success, Destination? NewFolder, string? ErrorMessage)> 
        CreateFolderAsync(Destination parentDestination, string folderName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check user has Manage permission on parent destination
            var hasManagePermission = await _permissionService.HasContentPermissionAsync(
                _userContext.UserId, parentDestination.Id, ContentPermissionLevel.Manage);

            if (!hasManagePermission)
            {
                _logger.LogWarning("User {UserId} denied folder creation in {DestinationId} - insufficient permissions", 
                    _userContext.UserId, parentDestination.Id);
                return (false, null, "Access denied: Manage permission required to create folders");
            }

            var (success, newFolder, errorMessage) = await _destinationService.CreateDestinationAsync(
                parentDestination, folderName, cancellationToken);

            if (!success || newFolder == null)
                return (false, null, errorMessage);

            // Register new folder in permission system
            await EnsureContentRegisteredAsync(newFolder, parentDestination.ProjectId);

            _logger.LogInformation("User {UserId} created folder '{FolderName}' in {ParentDestinationId}", 
                _userContext.UserId, folderName, parentDestination.Id);

            return (true, newFolder, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder for user {UserId}", _userContext.UserId);
            return (false, null, $"Error creating folder: {ex.Message}");
        }
    }

    #endregion

    #region Upload Operations

    /// <summary>
    /// Upload file with permission checking
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> UploadFileAsync(
        string filePath, 
        Destination destination, 
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check user has Manage permission on destination
            var hasUploadPermission = await _permissionService.HasContentPermissionAsync(
                _userContext.UserId, destination.Id, ContentPermissionLevel.Manage);

            if (!hasUploadPermission)
            {
                _logger.LogWarning("User {UserId} denied file upload to {DestinationId} - insufficient permissions", 
                    _userContext.UserId, destination.Id);
                return (false, "Access denied: Manage permission required to upload files");
            }

            var (success, errorMessage) = await _uploadService.UploadFileAsync(
                filePath, destination, progress, cancellationToken);

            if (success)
            {
                // Register uploaded file in permission system
                var fileInfo = new FileInfo(filePath);
                var contentMetadata = new ContentMetadata
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = fileInfo.Name,
                    Type = "file",
                    ProjectId = destination.ProjectId,
                    ParentId = destination.Id,
                    Path = $"{destination.Path}/{fileInfo.Name}",
                    FileExtension = fileInfo.Extension,
                    FileSize = fileInfo.Length,
                    CreatedAt = DateTime.UtcNow
                };

                await _permissionService.RegisterContentAsync(contentMetadata, _userContext.UserId);

                _logger.LogInformation("User {UserId} uploaded file '{FileName}' to {DestinationId}", 
                    _userContext.UserId, fileInfo.Name, destination.Id);
            }

            return (success, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for user {UserId}", _userContext.UserId);
            return (false, $"Error uploading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Upload folder with permission checking
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> UploadFolderAsync(
        string folderPath, 
        Destination destination, 
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check user has Manage permission on destination
            var hasUploadPermission = await _permissionService.HasContentPermissionAsync(
                _userContext.UserId, destination.Id, ContentPermissionLevel.Manage);

            if (!hasUploadPermission)
            {
                _logger.LogWarning("User {UserId} denied folder upload to {DestinationId} - insufficient permissions", 
                    _userContext.UserId, destination.Id);
                return (false, "Access denied: Manage permission required to upload folders");
            }

            var (success, errorMessage) = await _uploadService.UploadFolderAsync(
                folderPath, destination, progress, cancellationToken);

            if (success)
            {
                // Register uploaded folder structure in permission system
                await RegisterFolderStructureAsync(folderPath, destination);

                var folderInfo = new DirectoryInfo(folderPath);
                _logger.LogInformation("User {UserId} uploaded folder '{FolderName}' to {DestinationId}", 
                    _userContext.UserId, folderInfo.Name, destination.Id);
            }

            return (success, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading folder for user {UserId}", _userContext.UserId);
            return (false, $"Error uploading folder: {ex.Message}");
        }
    }

    #endregion

    #region Permission Management

    /// <summary>
    /// Create a new role (requires CreateRoles feature permission)
    /// </summary>
    public async Task<(bool Success, DataroomRole? Role, string? ErrorMessage)> CreateRoleAsync(
        string projectId, 
        string roleName, 
        string? description, 
        FeaturePermissions featurePermissions)
    {
        try
        {
            var canCreateRoles = await _permissionService.HasFeaturePermissionAsync(
                _userContext.UserId, FeaturePermissions.CreateRoles);

            if (!canCreateRoles)
            {
                _logger.LogWarning("User {UserId} denied role creation - insufficient permissions", _userContext.UserId);
                return (false, null, "Access denied: Role creation permission required");
            }

            var role = await _permissionService.CreateRoleAsync(
                projectId, roleName, description, featurePermissions, _userContext.UserId);

            _logger.LogInformation("User {UserId} created role '{RoleName}' in project {ProjectId}", 
                _userContext.UserId, roleName, projectId);

            return (true, role, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role for user {UserId}", _userContext.UserId);
            return (false, null, $"Error creating role: {ex.Message}");
        }
    }

    /// <summary>
    /// Set content permissions for a role (requires ManagePermissions feature permission)
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> SetContentPermissionAsync(
        string projectId,
        string roleId, 
        string contentId, 
        ContentPermissionLevel permissionLevel, 
        bool applyToChildren = true)
    {
        try
        {
            var canManagePermissions = await _permissionService.HasFeaturePermissionAsync(
                _userContext.UserId, FeaturePermissions.ManagePermissions);

            if (!canManagePermissions)
            {
                _logger.LogWarning("User {UserId} denied permission management - insufficient permissions", _userContext.UserId);
                return (false, "Access denied: Permission management feature required");
            }

            var success = await _permissionService.SetContentPermissionAsync(
                projectId, roleId, contentId, permissionLevel, applyToChildren, _userContext.UserId);

            if (success)
            {
                _logger.LogInformation("User {UserId} set {PermissionLevel} permission for role {RoleId} on content {ContentId}", 
                    _userContext.UserId, permissionLevel, roleId, contentId);
            }

            return (success, success ? null : "Failed to set content permission");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting content permission for user {UserId}", _userContext.UserId);
            return (false, $"Error setting permission: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private async Task EnsureContentRegisteredAsync(Destination destination, string projectId)
    {
        var existingContent = await _permissionService.GetContentMetadataAsync(destination.Id);
        
        if (existingContent == null)
        {
            var contentMetadata = new ContentMetadata
            {
                Id = destination.Id,
                Name = destination.Name,
                Type = destination.Type.ToLowerInvariant(),
                ProjectId = projectId,
                ParentId = destination.ParentId,
                Path = destination.Path,
                CreatedAt = DateTime.UtcNow
            };

            await _permissionService.RegisterContentAsync(contentMetadata, _userContext.UserId);
        }
    }

    private async Task RegisterFolderStructureAsync(string folderPath, Destination parentDestination)
    {
        var folderInfo = new DirectoryInfo(folderPath);
        
        // Register the main folder
        var folderMetadata = new ContentMetadata
        {
            Id = Guid.NewGuid().ToString(),
            Name = folderInfo.Name,
            Type = "folder",
            ProjectId = parentDestination.ProjectId,
            ParentId = parentDestination.Id,
            Path = $"{parentDestination.Path}/{folderInfo.Name}",
            CreatedAt = DateTime.UtcNow
        };

        await _permissionService.RegisterContentAsync(folderMetadata, _userContext.UserId);

        // Register all files in the folder
        foreach (var file in folderInfo.GetFiles())
        {
            var fileMetadata = new ContentMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = file.Name,
                Type = "file",
                ProjectId = parentDestination.ProjectId,
                ParentId = folderMetadata.Id,
                Path = $"{folderMetadata.Path}/{file.Name}",
                FileExtension = file.Extension,
                FileSize = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            await _permissionService.RegisterContentAsync(fileMetadata, _userContext.UserId);
        }

        // Recursively register subfolders
        foreach (var subFolder in folderInfo.GetDirectories())
        {
            var subDestination = new Destination
            {
                Id = folderMetadata.Id,
                Name = folderMetadata.Name,
                Type = "folder",
                ProjectId = parentDestination.ProjectId,
                Path = folderMetadata.Path,
                ParentId = parentDestination.Id
            };

            await RegisterFolderStructureAsync(subFolder.FullName, subDestination);
        }
    }

    #endregion
}

/// <summary>
/// User context for tracking current authenticated user
/// </summary>
public sealed class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public DateTime AuthenticatedAt { get; set; } = DateTime.UtcNow;
}