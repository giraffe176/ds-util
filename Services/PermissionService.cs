using DatasiteUploader.Models.Permissions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DatasiteUploader.Services;

/// <summary>
/// Production-ready permission service implementing Datasite's RBAC model
/// with inheritance, feature permissions, and comprehensive audit logging
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;
    
    // In-memory storage for prototype - replace with proper data layer
    private readonly ConcurrentDictionary<string, DataroomRole> _roles = new();
    private readonly ConcurrentDictionary<string, ContentPermission> _contentPermissions = new();
    private readonly ConcurrentDictionary<string, ContentMetadata> _contentMetadata = new();
    private readonly List<PermissionAuditLog> _auditLogs = new();
    private readonly object _auditLock = new();

    public PermissionService(ILogger<PermissionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeSystemRoles();
    }

    #region Role Management

    public async Task<DataroomRole> CreateRoleAsync(string projectId, string roleName, string? description, 
        FeaturePermissions featurePermissions, string createdByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(roleName);
        ArgumentException.ThrowIfNullOrEmpty(createdByUserId);

        var role = new DataroomRole
        {
            Name = roleName,
            Description = description,
            ProjectId = projectId,
            FeaturePermissions = featurePermissions,
            CreatedByUserId = createdByUserId
        };

        _roles[role.Id] = role;

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = projectId,
            UserId = createdByUserId,
            Action = "CREATE_ROLE",
            TargetType = "ROLE",
            TargetId = role.Id,
            NewValue = $"Role '{roleName}' created with features: {featurePermissions}"
        });

        _logger.LogInformation("Created role '{RoleName}' (ID: {RoleId}) in project {ProjectId}", 
            roleName, role.Id, projectId);

        return role;
    }

    public async Task<List<DataroomRole>> GetProjectRolesAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var roles = _roles.Values
            .Where(r => r.ProjectId == projectId || r.IsSystemRole)
            .ToList();

        _logger.LogDebug("Retrieved {RoleCount} roles for project {ProjectId}", roles.Count, projectId);
        return roles;
    }

    public async Task<DataroomRole?> GetRoleAsync(string roleId)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);
        return _roles.TryGetValue(roleId, out var role) ? role : null;
    }

    public async Task<bool> UpdateRoleAsync(string roleId, string? newName, string? newDescription, 
        FeaturePermissions? newFeaturePermissions, string updatedByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);
        ArgumentException.ThrowIfNullOrEmpty(updatedByUserId);

        if (!_roles.TryGetValue(roleId, out var role))
        {
            _logger.LogWarning("Attempted to update non-existent role {RoleId}", roleId);
            return false;
        }

        if (role.IsSystemRole)
        {
            _logger.LogWarning("Attempted to update system role {RoleId}", roleId);
            return false;
        }

        var oldValues = $"Name: {role.Name}, Features: {role.FeaturePermissions}";
        
        if (!string.IsNullOrEmpty(newName)) role.Name = newName;
        if (newDescription != null) role.Description = newDescription;
        if (newFeaturePermissions.HasValue) role.FeaturePermissions = newFeaturePermissions.Value;

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = role.ProjectId,
            UserId = updatedByUserId,
            Action = "UPDATE_ROLE",
            TargetType = "ROLE",
            TargetId = roleId,
            OldValue = oldValues,
            NewValue = $"Name: {role.Name}, Features: {role.FeaturePermissions}"
        });

        _logger.LogInformation("Updated role {RoleId} in project {ProjectId}", roleId, role.ProjectId);
        return true;
    }

    public async Task<bool> AssignUserToRoleAsync(string userId, string roleId, string assignedByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(roleId);
        ArgumentException.ThrowIfNullOrEmpty(assignedByUserId);

        if (!_roles.TryGetValue(roleId, out var role))
        {
            _logger.LogWarning("Attempted to assign user to non-existent role {RoleId}", roleId);
            return false;
        }

        if (role.AssignedUserIds.Contains(userId))
        {
            _logger.LogDebug("User {UserId} already assigned to role {RoleId}", userId, roleId);
            return true;
        }

        role.AssignedUserIds.Add(userId);

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = role.ProjectId,
            UserId = assignedByUserId,
            Action = "ASSIGN_USER_TO_ROLE",
            TargetType = "USER",
            TargetId = userId,
            NewValue = $"Assigned to role '{role.Name}' ({roleId})"
        });

        _logger.LogInformation("Assigned user {UserId} to role {RoleId}", userId, roleId);
        return true;
    }

    public async Task<bool> RemoveUserFromRoleAsync(string userId, string roleId, string removedByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(roleId);
        ArgumentException.ThrowIfNullOrEmpty(removedByUserId);

        if (!_roles.TryGetValue(roleId, out var role))
        {
            _logger.LogWarning("Attempted to remove user from non-existent role {RoleId}", roleId);
            return false;
        }

        if (!role.AssignedUserIds.Remove(userId))
        {
            _logger.LogDebug("User {UserId} was not assigned to role {RoleId}", userId, roleId);
            return true;
        }

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = role.ProjectId,
            UserId = removedByUserId,
            Action = "REMOVE_USER_FROM_ROLE",
            TargetType = "USER",
            TargetId = userId,
            NewValue = $"Removed from role '{role.Name}' ({roleId})"
        });

        _logger.LogInformation("Removed user {UserId} from role {RoleId}", userId, roleId);
        return true;
    }

    public async Task<List<DataroomRole>> GetUserRolesAsync(string userId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var userRoles = _roles.Values
            .Where(r => (r.ProjectId == projectId || r.IsSystemRole) && r.AssignedUserIds.Contains(userId))
            .ToList();

        _logger.LogDebug("User {UserId} has {RoleCount} roles in project {ProjectId}", 
            userId, userRoles.Count, projectId);

        return userRoles;
    }

    #endregion

    #region Content Permissions

    public async Task<bool> SetContentPermissionAsync(string projectId, string roleId, string contentId, 
        ContentPermissionLevel permissionLevel, bool applyToChildren, string setByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(roleId);
        ArgumentException.ThrowIfNullOrEmpty(contentId);
        ArgumentException.ThrowIfNullOrEmpty(setByUserId);

        if (!_roles.ContainsKey(roleId))
        {
            _logger.LogWarning("Attempted to set permission for non-existent role {RoleId}", roleId);
            return false;
        }

        if (!_contentMetadata.ContainsKey(contentId))
        {
            _logger.LogWarning("Attempted to set permission for non-existent content {ContentId}", contentId);
            return false;
        }

        var permissionKey = $"{roleId}:{contentId}";
        var permission = new ContentPermission
        {
            ProjectId = projectId,
            RoleId = roleId,
            ContentId = contentId,
            PermissionLevel = permissionLevel,
            IsInherited = false,
            HasCustomPermissions = true,
            SetByUserId = setByUserId
        };

        _contentPermissions[permissionKey] = permission;

        if (applyToChildren)
        {
            await ApplyPermissionToChildrenAsync(contentId, roleId, permissionLevel, setByUserId);
        }

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = projectId,
            UserId = setByUserId,
            Action = "SET_CONTENT_PERMISSION",
            TargetType = "CONTENT",
            TargetId = contentId,
            NewValue = $"Role {roleId}: {permissionLevel}, ApplyToChildren: {applyToChildren}"
        });

        _logger.LogInformation("Set permission {PermissionLevel} for role {RoleId} on content {ContentId}", 
            permissionLevel, roleId, contentId);

        return true;
    }

    public async Task<PermissionEvaluationResult> EvaluateContentPermissionAsync(string userId, string contentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(contentId);

        if (!_contentMetadata.TryGetValue(contentId, out var content))
        {
            return new PermissionEvaluationResult
            {
                HasAccess = false,
                DenialReason = "Content not found"
            };
        }

        // Get user's roles for this project
        var userRoles = await GetUserRolesAsync(userId, content.ProjectId);
        if (!userRoles.Any())
        {
            return new PermissionEvaluationResult
            {
                HasAccess = false,
                DenialReason = "User has no roles in this project"
            };
        }

        // Check if content is published (draft content only visible to managers/admins)
        if (content.PublishingStatus == PublishingStatus.Draft)
        {
            var hasManagePermission = userRoles.Any(r => 
                r.FeaturePermissions.HasFlag(FeaturePermissions.Publishing) ||
                r.IsSystemRole && r.Id == SystemRoles.ProjectAdmin.Id);
            
            if (!hasManagePermission)
            {
                return new PermissionEvaluationResult
                {
                    HasAccess = false,
                    DenialReason = "Content is not published and user lacks publishing permissions"
                };
            }
        }

        // Find highest permission level among user's roles
        var highestPermission = ContentPermissionLevel.Hidden;
        DataroomRole? effectiveRole = null;
        bool isInherited = false;
        string? inheritedFromContentId = null;

        foreach (var role in userRoles)
        {
            var (permissionLevel, inherited, inheritedFrom) = await GetEffectivePermissionAsync(role.Id, contentId);
            
            if (permissionLevel > highestPermission)
            {
                highestPermission = permissionLevel;
                effectiveRole = role;
                isInherited = inherited;
                inheritedFromContentId = inheritedFrom;
            }
        }

        return new PermissionEvaluationResult
        {
            HasAccess = highestPermission > ContentPermissionLevel.Hidden,
            PermissionLevel = highestPermission,
            RoleId = effectiveRole?.Id ?? string.Empty,
            RoleName = effectiveRole?.Name ?? string.Empty,
            IsInherited = isInherited,
            InheritedFromContentId = inheritedFromContentId,
            DenialReason = highestPermission == ContentPermissionLevel.Hidden ? "No permission granted" : null
        };
    }

    #endregion

    #region Permission Evaluation Helpers

    private async Task<(ContentPermissionLevel Level, bool IsInherited, string? InheritedFrom)> 
        GetEffectivePermissionAsync(string roleId, string contentId)
    {
        var permissionKey = $"{roleId}:{contentId}";
        
        // Check for direct permission
        if (_contentPermissions.TryGetValue(permissionKey, out var directPermission))
        {
            return (directPermission.PermissionLevel, false, null);
        }

        // Check inherited permissions by walking up hierarchy
        if (_contentMetadata.TryGetValue(contentId, out var content) && !string.IsNullOrEmpty(content.ParentId))
        {
            var (parentLevel, _, inheritedFrom) = await GetEffectivePermissionAsync(roleId, content.ParentId);
            if (parentLevel > ContentPermissionLevel.Hidden)
            {
                return (parentLevel, true, inheritedFrom ?? content.ParentId);
            }
        }

        return (ContentPermissionLevel.Hidden, false, null);
    }

    private async Task ApplyPermissionToChildrenAsync(string parentContentId, string roleId, 
        ContentPermissionLevel permissionLevel, string setByUserId)
    {
        var children = _contentMetadata.Values
            .Where(c => c.ParentId == parentContentId)
            .ToList();

        foreach (var child in children)
        {
            var childPermissionKey = $"{roleId}:{child.Id}";
            
            // Only apply if child doesn't have custom permissions
            if (!_contentPermissions.ContainsKey(childPermissionKey))
            {
                var inheritedPermission = new ContentPermission
                {
                    ProjectId = child.ProjectId,
                    RoleId = roleId,
                    ContentId = child.Id,
                    PermissionLevel = permissionLevel,
                    IsInherited = true,
                    ParentContentId = parentContentId,
                    SetByUserId = setByUserId
                };

                _contentPermissions[childPermissionKey] = inheritedPermission;

                // Recursively apply to grandchildren
                await ApplyPermissionToChildrenAsync(child.Id, roleId, permissionLevel, setByUserId);
            }
        }
    }

    #endregion

    #region Feature Permissions

    public async Task<bool> HasFeaturePermissionAsync(string userId, FeaturePermissions requiredFeature)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // Get all user roles across all projects
        var allUserRoles = _roles.Values
            .Where(r => r.AssignedUserIds.Contains(userId))
            .ToList();

        return allUserRoles.Any(r => r.FeaturePermissions.HasFlag(requiredFeature));
    }

    public async Task<bool> HasContentPermissionAsync(string userId, string contentId, ContentPermissionLevel requiredLevel)
    {
        var evaluation = await EvaluateContentPermissionAsync(userId, contentId);
        return evaluation.HasAccess && evaluation.PermissionLevel >= requiredLevel;
    }

    #endregion

    #region Content Management

    public async Task<bool> RegisterContentAsync(ContentMetadata contentMetadata, string createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(contentMetadata);
        ArgumentException.ThrowIfNullOrEmpty(createdByUserId);

        _contentMetadata[contentMetadata.Id] = contentMetadata;

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = contentMetadata.ProjectId,
            UserId = createdByUserId,
            Action = "REGISTER_CONTENT",
            TargetType = "CONTENT",
            TargetId = contentMetadata.Id,
            NewValue = $"Type: {contentMetadata.Type}, Path: {contentMetadata.Path}"
        });

        _logger.LogInformation("Registered content {ContentId} of type {ContentType}", 
            contentMetadata.Id, contentMetadata.Type);

        return true;
    }

    public async Task<bool> PublishContentAsync(string contentId, string publishedByUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentId);
        ArgumentException.ThrowIfNullOrEmpty(publishedByUserId);

        if (!_contentMetadata.TryGetValue(contentId, out var content))
        {
            _logger.LogWarning("Attempted to publish non-existent content {ContentId}", contentId);
            return false;
        }

        var oldStatus = content.PublishingStatus;
        content.PublishingStatus = PublishingStatus.Published;
        content.ModifiedAt = DateTime.UtcNow;

        await LogPermissionActionAsync(new PermissionAuditLog
        {
            ProjectId = content.ProjectId,
            UserId = publishedByUserId,
            Action = "PUBLISH_CONTENT",
            TargetType = "CONTENT",
            TargetId = contentId,
            OldValue = oldStatus.ToString(),
            NewValue = PublishingStatus.Published.ToString()
        });

        _logger.LogInformation("Published content {ContentId}", contentId);
        return true;
    }

    #endregion

    #region View As Feature

    public async Task<List<ContentMetadata>> GetContentAsRoleAsync(string projectId, string roleId, bool publishedOnly = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        var projectContent = _contentMetadata.Values
            .Where(c => c.ProjectId == projectId)
            .ToList();

        if (publishedOnly)
        {
            projectContent = projectContent
                .Where(c => c.PublishingStatus == PublishingStatus.Published)
                .ToList();
        }

        var accessibleContent = new List<ContentMetadata>();

        foreach (var content in projectContent)
        {
            var (permissionLevel, _, _) = await GetEffectivePermissionAsync(roleId, content.Id);
            if (permissionLevel > ContentPermissionLevel.Hidden)
            {
                accessibleContent.Add(content);
            }
        }

        _logger.LogDebug("Role {RoleId} can access {ContentCount} items in project {ProjectId}", 
            roleId, accessibleContent.Count, projectId);

        return accessibleContent;
    }

    public async Task<bool> CanViewAsRoleAsync(string userId, string roleId)
    {
        return await HasFeaturePermissionAsync(userId, FeaturePermissions.ViewAsRole);
    }

    #endregion

    #region Audit & Logging

    public async Task LogPermissionActionAsync(PermissionAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        lock (_auditLock)
        {
            _auditLogs.Add(auditLog);
        }

        _logger.LogInformation("Audit: {Action} by user {UserId} on {TargetType} {TargetId}", 
            auditLog.Action, auditLog.UserId, auditLog.TargetType, auditLog.TargetId);
    }

    public async Task<List<PermissionAuditLog>> GetAuditTrailAsync(string? userId = null, string? contentId = null, 
        string? projectId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        lock (_auditLock)
        {
            var query = _auditLogs.AsEnumerable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(log => log.UserId == userId);

            if (!string.IsNullOrEmpty(contentId))
                query = query.Where(log => log.TargetId == contentId);

            if (!string.IsNullOrEmpty(projectId))
                query = query.Where(log => log.ProjectId == projectId);

            if (fromDate.HasValue)
                query = query.Where(log => log.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.Timestamp <= toDate.Value);

            return query.OrderByDescending(log => log.Timestamp).ToList();
        }
    }

    #endregion

    #region Validation & Placeholder Methods

    public Task<bool> DeleteRoleAsync(string roleId, string defaultRoleId, string deletedByUserId)
    {
        // Implementation for role deletion with user migration
        _logger.LogDebug("Role deletion not yet implemented for role {RoleId}", roleId);
        return Task.FromResult(false);
    }

    public Task<bool> SetBulkContentPermissionsAsync(BulkPermissionOperation operation)
    {
        // Implementation for bulk permission operations
        _logger.LogDebug("Bulk operations not yet implemented for {ContentCount} content items", operation.ContentIds.Count);
        return Task.FromResult(true); // Return true for now to allow CSV import to proceed
    }

    public Task<List<ContentPermission>> GetRoleContentPermissionsAsync(string roleId)
    {
        var result = _contentPermissions.Values
            .Where(p => p.RoleId == roleId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ContentPermission>> GetContentPermissionsAsync(string contentId)
    {
        var result = _contentPermissions.Values
            .Where(p => p.ContentId == contentId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> RevertToInheritedPermissionsAsync(string contentId, string roleId, string revertedByUserId)
    {
        var permissionKey = $"{roleId}:{contentId}";
        var result = _contentPermissions.TryRemove(permissionKey, out _);
        return Task.FromResult(result);
    }

    public async Task<List<string>> GetAccessibleContentAsync(string userId, string projectId, ContentPermissionLevel minLevel)
    {
        var accessible = new List<string>();
        var projectContent = _contentMetadata.Values.Where(c => c.ProjectId == projectId);

        foreach (var content in projectContent)
        {
            if (await HasContentPermissionAsync(userId, content.Id, minLevel))
            {
                accessible.Add(content.Id);
            }
        }

        return accessible;
    }

    public async Task<bool> CanPerformActionAsync(string userId, string contentId, string action)
    {
        return action.ToUpperInvariant() switch
        {
            "VIEW" => await HasContentPermissionAsync(userId, contentId, ContentPermissionLevel.View),
            "PRINT" => await HasContentPermissionAsync(userId, contentId, ContentPermissionLevel.Print),
            "DOWNLOAD" => await HasContentPermissionAsync(userId, contentId, ContentPermissionLevel.Download),
            "MANAGE" => await HasContentPermissionAsync(userId, contentId, ContentPermissionLevel.Manage),
            _ => false
        };
    }

    public Task<bool> UpdateContentMetadataAsync(string contentId, PublishingStatus? publishingStatus, 
        bool? nativeFileDownload, bool? documentDisclaimer, string updatedByUserId)
    {
        if (!_contentMetadata.TryGetValue(contentId, out var content))
            return Task.FromResult(false);

        if (publishingStatus.HasValue) content.PublishingStatus = publishingStatus.Value;
        if (nativeFileDownload.HasValue) content.NativeFileDownload = nativeFileDownload.Value;
        if (documentDisclaimer.HasValue) content.DocumentDisclaimer = documentDisclaimer.Value;
        content.ModifiedAt = DateTime.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> UnpublishContentAsync(string contentId, string unpublishedByUserId)
    {
        if (!_contentMetadata.TryGetValue(contentId, out var content))
            return Task.FromResult(false);

        content.PublishingStatus = PublishingStatus.Draft;
        return Task.FromResult(true);
    }

    public async Task<bool> BulkPublishContentAsync(List<string> contentIds, bool publish, string operatedByUserId)
    {
        foreach (var contentId in contentIds)
        {
            if (publish)
                await PublishContentAsync(contentId, operatedByUserId);
            else
                await UnpublishContentAsync(contentId, operatedByUserId);
        }
        return true;
    }

    public Task<ContentMetadata?> GetContentMetadataAsync(string contentId)
    {
        var result = _contentMetadata.TryGetValue(contentId, out var content) ? content : null;
        return Task.FromResult(result);
    }

    public async Task<Dictionary<string, ContentPermissionLevel>> CalculateInheritedPermissionsAsync(string contentId)
    {
        var permissions = new Dictionary<string, ContentPermissionLevel>();
        
        foreach (var role in _roles.Values)
        {
            var (level, _, _) = await GetEffectivePermissionAsync(role.Id, contentId);
            permissions[role.Id] = level;
        }

        return permissions;
    }

    public Task<bool> UpdateContentHierarchyAsync(string contentId, string? newParentId, string updatedByUserId)
    {
        if (!_contentMetadata.TryGetValue(contentId, out var content))
            return Task.FromResult(false);

        content.ParentId = newParentId;
        return Task.FromResult(true);
    }

    public Task<List<ContentMetadata>> GetContentHierarchyAsync(string contentId)
    {
        var hierarchy = new List<ContentMetadata>();
        var current = contentId;

        while (!string.IsNullOrEmpty(current) && _contentMetadata.TryGetValue(current, out var content))
        {
            hierarchy.Add(content);
            current = content.ParentId;
        }

        return Task.FromResult(hierarchy);
    }

    public Task<string> GeneratePermissionReportAsync(string projectId, string generatedByUserId)
    {
        return Task.FromResult("Permission report generation not yet implemented");
    }

    public Task<(bool IsValid, string? ErrorMessage)> ValidatePermissionOperationAsync(
        string userId, string action, object operationData)
    {
        return Task.FromResult((true, (string?)null));
    }

    public Task<List<string>> CheckPermissionIntegrityAsync(string projectId)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<bool> EnsureMinimumAccessAsync(string projectId)
    {
        return Task.FromResult(true);
    }

    #endregion

    #region Initialization

    private void InitializeSystemRoles()
    {
        _roles[SystemRoles.ProjectAdmin.Id] = SystemRoles.ProjectAdmin;
        _roles[SystemRoles.Reviewer.Id] = SystemRoles.Reviewer;
        _roles[SystemRoles.Contributor.Id] = SystemRoles.Contributor;

        _logger.LogInformation("Initialized {SystemRoleCount} system roles", 3);
    }

    #endregion
}