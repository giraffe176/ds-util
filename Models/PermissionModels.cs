using System.Text.Json.Serialization;

namespace DatasiteUploader.Models.Permissions;

/// <summary>
/// Content permission levels - each level includes all levels below it
/// </summary>
public enum ContentPermissionLevel
{
    /// <summary>
    /// Role cannot see this content
    /// </summary>
    Hidden = 0,
    
    /// <summary>
    /// Role can view content onscreen but cannot print or download
    /// </summary>
    View = 1,
    
    /// <summary>
    /// Role can print content (includes View)
    /// </summary>
    Print = 2,
    
    /// <summary>
    /// Role can download content (includes View + Print)
    /// </summary>
    Download = 3,
    
    /// <summary>
    /// Role can perform admin actions: create folders, upload files, etc. (includes all above)
    /// </summary>
    Manage = 4
}

/// <summary>
/// Feature permissions that control access to specific tools and capabilities
/// </summary>
[Flags]
public enum FeaturePermissions : long
{
    None = 0,
    
    // General Features
    Publishing = 1L << 0,
    DownloadMultipleFiles = 1L << 1,
    InviteUser = 1L << 2,
    Analytics = 1L << 3,
    Redaction = 1L << 4,
    
    // Pipeline Features
    UserManagement = 1L << 5,
    Dashboards = 1L << 6,
    Trackers = 1L << 7,
    Inbox = 1L << 8,
    Contacts = 1L << 9,
    Activities = 1L << 10,
    Reminders = 1L << 11,
    TearSheets = 1L << 12,
    
    // Acquire Features
    CommentingOnFiles = 1L << 13,
    Findings = 1L << 14,
    SellerContentAccess = 1L << 15,
    AcquireTrackers = 1L << 16,
    AcquireDashboard = 1L << 17,
    
    // Administrative Features
    CreateRoles = 1L << 18,
    ManagePermissions = 1L << 19,
    ViewAsRole = 1L << 20,
    BulkOperations = 1L << 21
}

/// <summary>
/// Content publishing status
/// </summary>
public enum PublishingStatus
{
    Draft,
    Published,
    PartiallyPublished // For folders with mixed published/draft content
}

/// <summary>
/// Role definition with permissions
/// </summary>
public sealed class DataroomRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public FeaturePermissions FeaturePermissions { get; set; } = FeaturePermissions.None;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; } = false; // For built-in roles like Admin
    
    /// <summary>
    /// Users assigned to this role
    /// </summary>
    public List<string> AssignedUserIds { get; set; } = new();
}

/// <summary>
/// Content permission assignment for a specific role and content item
/// </summary>
public sealed class ContentPermission
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; // "fileroom", "folder", "file"
    public ContentPermissionLevel PermissionLevel { get; set; } = ContentPermissionLevel.Hidden;
    public bool IsInherited { get; set; } = true; // If permission comes from parent
    public string? ParentContentId { get; set; } // For inheritance tracking
    public DateTime SetAt { get; set; } = DateTime.UtcNow;
    public string SetByUserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom permissions that override inheritance
    /// </summary>
    public bool HasCustomPermissions { get; set; } = false;
}

/// <summary>
/// Extended content metadata for permission management
/// </summary>
public sealed class ContentMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "fileroom", "folder", "file"
    public string ProjectId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Path { get; set; } = string.Empty;
    public PublishingStatus PublishingStatus { get; set; } = PublishingStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public bool NativeFileDownload { get; set; } = true;
    public bool DocumentDisclaimer { get; set; } = false;
    
    /// <summary>
    /// Computed permission summary for efficient display
    /// </summary>
    public Dictionary<string, ContentPermissionLevel> RolePermissions { get; set; } = new();
    
    /// <summary>
    /// Indicates if this content has custom permissions that differ from inherited
    /// </summary>
    public bool HasCustomPermissions { get; set; } = false;
}

/// <summary>
/// Permission evaluation result for a user's access to content
/// </summary>
public sealed class PermissionEvaluationResult
{
    public bool HasAccess { get; set; }
    public ContentPermissionLevel PermissionLevel { get; set; } = ContentPermissionLevel.Hidden;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsInherited { get; set; }
    public string? InheritedFromContentId { get; set; }
    public string? DenialReason { get; set; }
    
    // Specific permission checks
    public bool CanView => PermissionLevel >= ContentPermissionLevel.View;
    public bool CanPrint => PermissionLevel >= ContentPermissionLevel.Print;
    public bool CanDownload => PermissionLevel >= ContentPermissionLevel.Download;
    public bool CanManage => PermissionLevel >= ContentPermissionLevel.Manage;
}

/// <summary>
/// Bulk permission operation for setting permissions on multiple items
/// </summary>
public sealed class BulkPermissionOperation
{
    public string ProjectId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public List<string> ContentIds { get; set; } = new();
    public ContentPermissionLevel PermissionLevel { get; set; }
    public bool ApplyToChildren { get; set; } = true;
    public string OperationByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Permission inheritance rule for content hierarchy
/// </summary>
public sealed class PermissionInheritanceRule
{
    public string ContentId { get; set; } = string.Empty;
    public string ParentContentId { get; set; } = string.Empty;
    public bool InheritPermissions { get; set; } = true;
    public List<string> ExceptionRoleIds { get; set; } = new(); // Roles that don't inherit
}

/// <summary>
/// Permission audit log entry
/// </summary>
public sealed class PermissionAuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "SET_PERMISSION", "CREATE_ROLE", "ASSIGN_USER", etc.
    public string TargetType { get; set; } = string.Empty; // "CONTENT", "ROLE", "USER"
    public string TargetId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

/// <summary>
/// System-defined roles with default permissions
/// </summary>
public static class SystemRoles
{
    public static readonly DataroomRole ProjectAdmin = new()
    {
        Id = "system-admin",
        Name = "Project Administrator",
        Description = "Full access to all project features and content",
        IsSystemRole = true,
        FeaturePermissions = FeaturePermissions.Publishing | 
                           FeaturePermissions.DownloadMultipleFiles |
                           FeaturePermissions.InviteUser |
                           FeaturePermissions.Analytics |
                           FeaturePermissions.UserManagement |
                           FeaturePermissions.CreateRoles |
                           FeaturePermissions.ManagePermissions |
                           FeaturePermissions.ViewAsRole |
                           FeaturePermissions.BulkOperations
    };
    
    public static readonly DataroomRole Reviewer = new()
    {
        Id = "system-reviewer",
        Name = "Reviewer",
        Description = "Can view and download assigned content",
        IsSystemRole = true,
        FeaturePermissions = FeaturePermissions.None
    };
    
    public static readonly DataroomRole Contributor = new()
    {
        Id = "system-contributor",
        Name = "Contributor",
        Description = "Can upload content and view assigned areas",
        IsSystemRole = true,
        FeaturePermissions = FeaturePermissions.None
    };
}