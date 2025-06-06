using DatasiteUploader.Models.Permissions;

namespace DatasiteUploader.Services;

/// <summary>
/// Service for managing roles, permissions, and access control
/// Following Datasite's permission model with inheritance and feature access
/// </summary>
public interface IPermissionService
{
    #region Role Management
    
    /// <summary>
    /// Create a new role in the project
    /// </summary>
    Task<DataroomRole> CreateRoleAsync(string projectId, string roleName, string? description, 
        FeaturePermissions featurePermissions, string createdByUserId);
    
    /// <summary>
    /// Get all roles for a project
    /// </summary>
    Task<List<DataroomRole>> GetProjectRolesAsync(string projectId);
    
    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    Task<DataroomRole?> GetRoleAsync(string roleId);
    
    /// <summary>
    /// Update role permissions and settings
    /// </summary>
    Task<bool> UpdateRoleAsync(string roleId, string? newName, string? newDescription, 
        FeaturePermissions? newFeaturePermissions, string updatedByUserId);
    
    /// <summary>
    /// Delete a role (moves users to default role)
    /// </summary>
    Task<bool> DeleteRoleAsync(string roleId, string defaultRoleId, string deletedByUserId);
    
    /// <summary>
    /// Assign user to role
    /// </summary>
    Task<bool> AssignUserToRoleAsync(string userId, string roleId, string assignedByUserId);
    
    /// <summary>
    /// Remove user from role
    /// </summary>
    Task<bool> RemoveUserFromRoleAsync(string userId, string roleId, string removedByUserId);
    
    /// <summary>
    /// Get all roles for a specific user
    /// </summary>
    Task<List<DataroomRole>> GetUserRolesAsync(string userId, string projectId);
    
    #endregion
    
    #region Content Permissions
    
    /// <summary>
    /// Set content permission for a role (with inheritance to children)
    /// </summary>
    Task<bool> SetContentPermissionAsync(string projectId, string roleId, string contentId, 
        ContentPermissionLevel permissionLevel, bool applyToChildren, string setByUserId);
    
    /// <summary>
    /// Set permissions for multiple content items (bulk operation)
    /// </summary>
    Task<bool> SetBulkContentPermissionsAsync(BulkPermissionOperation operation);
    
    /// <summary>
    /// Get effective permission for a user on specific content
    /// </summary>
    Task<PermissionEvaluationResult> EvaluateContentPermissionAsync(string userId, string contentId);
    
    /// <summary>
    /// Get content permissions for a specific role
    /// </summary>
    Task<List<ContentPermission>> GetRoleContentPermissionsAsync(string roleId);
    
    /// <summary>
    /// Get all permissions for a specific content item
    /// </summary>
    Task<List<ContentPermission>> GetContentPermissionsAsync(string contentId);
    
    /// <summary>
    /// Remove custom permissions and revert to inheritance
    /// </summary>
    Task<bool> RevertToInheritedPermissionsAsync(string contentId, string roleId, string revertedByUserId);
    
    #endregion
    
    #region Permission Evaluation
    
    /// <summary>
    /// Check if user has specific content permission level
    /// </summary>
    Task<bool> HasContentPermissionAsync(string userId, string contentId, ContentPermissionLevel requiredLevel);
    
    /// <summary>
    /// Check if user has specific feature permission
    /// </summary>
    Task<bool> HasFeaturePermissionAsync(string userId, FeaturePermissions requiredFeature);
    
    /// <summary>
    /// Get all content a user can access with minimum permission level
    /// </summary>
    Task<List<string>> GetAccessibleContentAsync(string userId, string projectId, ContentPermissionLevel minLevel);
    
    /// <summary>
    /// Check if user can perform specific action on content
    /// </summary>
    Task<bool> CanPerformActionAsync(string userId, string contentId, string action);
    
    #endregion
    
    #region Content Metadata & Publishing
    
    /// <summary>
    /// Register new content in the permission system
    /// </summary>
    Task<bool> RegisterContentAsync(ContentMetadata contentMetadata, string createdByUserId);
    
    /// <summary>
    /// Update content metadata (publishing status, etc.)
    /// </summary>
    Task<bool> UpdateContentMetadataAsync(string contentId, PublishingStatus? publishingStatus, 
        bool? nativeFileDownload, bool? documentDisclaimer, string updatedByUserId);
    
    /// <summary>
    /// Publish content (makes it visible to roles with appropriate permissions)
    /// </summary>
    Task<bool> PublishContentAsync(string contentId, string publishedByUserId);
    
    /// <summary>
    /// Unpublish content (hides from all non-admin roles)
    /// </summary>
    Task<bool> UnpublishContentAsync(string contentId, string unpublishedByUserId);
    
    /// <summary>
    /// Bulk publish/unpublish operations
    /// </summary>
    Task<bool> BulkPublishContentAsync(List<string> contentIds, bool publish, string operatedByUserId);
    
    /// <summary>
    /// Get content metadata with computed permission summary
    /// </summary>
    Task<ContentMetadata?> GetContentMetadataAsync(string contentId);
    
    #endregion
    
    #region Inheritance & Hierarchy
    
    /// <summary>
    /// Calculate inherited permissions for content based on parent hierarchy
    /// </summary>
    Task<Dictionary<string, ContentPermissionLevel>> CalculateInheritedPermissionsAsync(string contentId);
    
    /// <summary>
    /// Update inheritance when content hierarchy changes
    /// </summary>
    Task<bool> UpdateContentHierarchyAsync(string contentId, string? newParentId, string updatedByUserId);
    
    /// <summary>
    /// Get content hierarchy for permission inheritance calculation
    /// </summary>
    Task<List<ContentMetadata>> GetContentHierarchyAsync(string contentId);
    
    #endregion
    
    #region View As Feature
    
    /// <summary>
    /// Get content structure as viewed by a specific role (for "View As" feature)
    /// </summary>
    Task<List<ContentMetadata>> GetContentAsRoleAsync(string projectId, string roleId, bool publishedOnly = true);
    
    /// <summary>
    /// Validate if user can use "View As" for the specified role
    /// </summary>
    Task<bool> CanViewAsRoleAsync(string userId, string roleId);
    
    #endregion
    
    #region Audit & Compliance
    
    /// <summary>
    /// Log permission-related action for audit trail
    /// </summary>
    Task LogPermissionActionAsync(PermissionAuditLog auditLog);
    
    /// <summary>
    /// Get audit trail for specific content or user
    /// </summary>
    Task<List<PermissionAuditLog>> GetAuditTrailAsync(string? userId = null, string? contentId = null, 
        string? projectId = null, DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Generate permission report for compliance
    /// </summary>
    Task<string> GeneratePermissionReportAsync(string projectId, string generatedByUserId);
    
    #endregion
    
    #region Validation & Constraints
    
    /// <summary>
    /// Validate permission operation before execution
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidatePermissionOperationAsync(
        string userId, string action, object operationData);
    
    /// <summary>
    /// Check for permission conflicts and issues
    /// </summary>
    Task<List<string>> CheckPermissionIntegrityAsync(string projectId);
    
    /// <summary>
    /// Ensure minimum access requirements (e.g., admin always has access)
    /// </summary>
    Task<bool> EnsureMinimumAccessAsync(string projectId);
    
    #endregion
}