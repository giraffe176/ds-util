using DatasiteUploader.Models;
using DatasiteUploader.Models.Permissions;
using DatasiteUploader.Models.CsvTemplates;

namespace DatasiteUploader.Services;

/// <summary>
/// Enterprise dataroom management interface
/// Orchestrates permissions, content, and user management for complete dataroom setup
/// </summary>
public interface IDataroomManagementService
{
    #region Dataroom Setup & Configuration
    
    /// <summary>
    /// Create a complete dataroom from scratch with folder structure and permissions
    /// </summary>
    Task<(bool Success, DataroomSetupResult Result, List<string> Errors)> CreateDataroomAsync(
        DataroomSetupRequest setupRequest);
    
    /// <summary>
    /// Setup dataroom from CSV templates (enterprise bulk setup)
    /// </summary>
    Task<(bool Success, DataroomSetupResult Result, List<string> Errors)> SetupDataroomFromCsvAsync(
        string projectId, ProjectSetupCsvFiles csvFiles, string createdByUserId);
    
    /// <summary>
    /// Clone dataroom structure and permissions from existing project
    /// </summary>
    Task<(bool Success, DataroomSetupResult Result, List<string> Errors)> CloneDataroomAsync(
        string sourceProjectId, string targetProjectId, DataroomCloneOptions options, string clonedByUserId);
    
    /// <summary>
    /// Generate standard dataroom templates based on industry/use case
    /// </summary>
    Task<DataroomTemplate> GenerateDataroomTemplateAsync(DataroomType dataroomType);
    
    #endregion
    
    #region Content Management & Organization
    
    /// <summary>
    /// Create folder structure with automatic permission inheritance
    /// </summary>
    Task<(bool Success, List<string> CreatedContentIds, List<string> Errors)> CreateFolderStructureAsync(
        string projectId, List<ContentStructureItem> structure, string createdByUserId);
    
    /// <summary>
    /// Bulk upload content with automatic categorization and permission assignment
    /// </summary>
    Task<(bool Success, BulkUploadResult Result, List<string> Errors)> BulkUploadContentAsync(
        string projectId, BulkUploadRequest uploadRequest);
    
    /// <summary>
    /// Organize existing content into new structure
    /// </summary>
    Task<(bool Success, int ItemsReorganized, List<string> Errors)> ReorganizeContentAsync(
        string projectId, List<ContentReorganizationRule> rules, string reorganizedByUserId);
    
    /// <summary>
    /// Publish/unpublish content in bulk with role-based visibility control
    /// </summary>
    Task<(bool Success, int ItemsPublished, List<string> Errors)> BulkPublishContentAsync(
        string projectId, List<string> contentIds, bool publish, string publishedByUserId);
    
    #endregion
    
    #region User & Role Management
    
    /// <summary>
    /// Setup complete user ecosystem for dataroom
    /// </summary>
    Task<(bool Success, UserSetupResult Result, List<string> Errors)> SetupDataroomUsersAsync(
        string projectId, List<DataroomUserSetup> userSetups, string setupByUserId);
    
    /// <summary>
    /// Invite users with automatic role assignment and welcome materials
    /// </summary>
    Task<(bool Success, int InvitationsSent, List<string> Errors)> InviteDataroomUsersAsync(
        string projectId, List<UserInvitationRequest> invitations, string invitedByUserId);
    
    /// <summary>
    /// Manage user lifecycle (onboarding, role changes, offboarding)
    /// </summary>
    Task<(bool Success, string UserId, List<string> Errors)> ManageUserLifecycleAsync(
        string projectId, string userEmail, UserLifecycleAction action, object actionData, string managedByUserId);
    
    /// <summary>
    /// Generate user access reports and analytics
    /// </summary>
    Task<UserAccessReport> GenerateUserAccessReportAsync(string projectId, DateTime? fromDate = null, DateTime? toDate = null);
    
    #endregion
    
    #region Permission Management & Governance
    
    /// <summary>
    /// Apply permission templates to content structure
    /// </summary>
    Task<(bool Success, int PermissionsApplied, List<string> Errors)> ApplyPermissionTemplateAsync(
        string projectId, PermissionTemplate template, List<string> targetContentIds, string appliedByUserId);
    
    /// <summary>
    /// Perform permission audit and compliance check
    /// </summary>
    Task<PermissionAuditReport> AuditDataroomPermissionsAsync(string projectId);
    
    /// <summary>
    /// Fix permission issues and inconsistencies
    /// </summary>
    Task<(bool Success, int IssuesFixed, List<string> RemainingIssues)> FixPermissionIssuesAsync(
        string projectId, List<string> issueTypes, string fixedByUserId);
    
    /// <summary>
    /// Generate permission matrices and reports for compliance
    /// </summary>
    Task<PermissionMatrix> GeneratePermissionMatrixAsync(string projectId, PermissionMatrixOptions options);
    
    #endregion
    
    #region Analytics & Monitoring
    
    /// <summary>
    /// Get dataroom usage analytics
    /// </summary>
    Task<DataroomAnalytics> GetDataroomAnalyticsAsync(string projectId, DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Monitor dataroom activity and generate alerts
    /// </summary>
    Task<List<DataroomAlert>> MonitorDataroomActivityAsync(string projectId);
    
    /// <summary>
    /// Generate executive dashboard data
    /// </summary>
    Task<DataroomDashboard> GenerateExecutiveDashboardAsync(string projectId);
    
    #endregion
    
    #region Template & Configuration Management
    
    /// <summary>
    /// Save dataroom configuration as reusable template
    /// </summary>
    Task<(bool Success, string TemplateId, List<string> Errors)> SaveDataroomTemplateAsync(
        string projectId, string templateName, string templateDescription, string savedByUserId);
    
    /// <summary>
    /// Apply saved template to new dataroom
    /// </summary>
    Task<(bool Success, DataroomSetupResult Result, List<string> Errors)> ApplyDataroomTemplateAsync(
        string templateId, string targetProjectId, string appliedByUserId);
    
    /// <summary>
    /// Export dataroom configuration for backup/migration
    /// </summary>
    Task<DataroomExportPackage> ExportDataroomConfigurationAsync(string projectId, DataroomExportOptions options);
    
    /// <summary>
    /// Import dataroom configuration from backup/migration package
    /// </summary>
    Task<(bool Success, DataroomSetupResult Result, List<string> Errors)> ImportDataroomConfigurationAsync(
        DataroomExportPackage package, string targetProjectId, string importedByUserId);
    
    #endregion
    
    #region Workflow & Automation
    
    /// <summary>
    /// Setup automated workflows for common dataroom tasks
    /// </summary>
    Task<(bool Success, string WorkflowId, List<string> Errors)> SetupDataroomWorkflowAsync(
        string projectId, DataroomWorkflow workflow, string setupByUserId);
    
    /// <summary>
    /// Execute workflow steps based on triggers
    /// </summary>
    Task<(bool Success, WorkflowExecutionResult Result)> ExecuteWorkflowAsync(
        string workflowId, WorkflowTrigger trigger, object triggerData);
    
    /// <summary>
    /// Schedule periodic dataroom maintenance tasks
    /// </summary>
    Task<(bool Success, List<string> ScheduledTasks)> ScheduleMaintenanceTasksAsync(
        string projectId, List<MaintenanceTask> tasks, string scheduledByUserId);
    
    #endregion
}

#region Supporting Models

/// <summary>
/// Request for setting up a new dataroom
/// </summary>
public sealed class DataroomSetupRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public string DataroomName { get; set; } = string.Empty;
    public DataroomType Type { get; set; } = DataroomType.DueDiligence;
    public List<ContentStructureItem> ContentStructure { get; set; } = new();
    public List<DataroomRole> Roles { get; set; } = new();
    public List<DataroomUserSetup> Users { get; set; } = new();
    public DataroomSettings Settings { get; set; } = new();
    public string CreatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Result of dataroom setup operation
/// </summary>
public sealed class DataroomSetupResult
{
    public string DataroomId { get; set; } = string.Empty;
    public int ContentItemsCreated { get; set; }
    public int RolesCreated { get; set; }
    public int UsersInvited { get; set; }
    public int PermissionsSet { get; set; }
    public TimeSpan SetupDuration { get; set; }
    public DateTime SetupCompletedAt { get; set; } = DateTime.UtcNow;
    public string SetupSessionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Types of datarooms with different default configurations
/// </summary>
public enum DataroomType
{
    DueDiligence,
    BoardMaterials,
    AuditReview,
    LegalDiscovery,
    RegulatoryFiling,
    IPO,
    MA,
    PrivateEquity,
    RealEstate,
    ProjectFinancing,
    Custom
}

/// <summary>
/// Content structure item for dataroom setup
/// </summary>
public sealed class ContentStructureItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "fileroom", "folder"
    public string? ParentPath { get; set; }
    public List<ContentStructureItem> Children { get; set; } = new();
    public Dictionary<string, ContentPermissionLevel> RolePermissions { get; set; } = new();
    public ContentMetadata Metadata { get; set; } = new();
    public int SortOrder { get; set; }
}

/// <summary>
/// User setup configuration for dataroom
/// </summary>
public sealed class DataroomUserSetup
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Organization { get; set; }
    public List<string> RoleNames { get; set; } = new();
    public bool SendInvitation { get; set; } = true;
    public string? WelcomeMessage { get; set; }
    public DateTime? AccessExpiry { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

/// <summary>
/// Dataroom settings and configuration
/// </summary>
public sealed class DataroomSettings
{
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnableDownloadTracking { get; set; } = true;
    public bool EnableWatermarking { get; set; } = false;
    public bool EnableDocumentExpiry { get; set; } = false;
    public bool EnableMobileAccess { get; set; } = true;
    public bool EnableBulkDownload { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 480; // 8 hours
    public List<string> AllowedFileTypes { get; set; } = new();
    public long MaxFileSize { get; set; } = 1073741824; // 1GB
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Options for cloning datarooms
/// </summary>
public sealed class DataroomCloneOptions
{
    public bool CopyContent { get; set; } = false;
    public bool CopyPermissions { get; set; } = true;
    public bool CopyRoles { get; set; } = true;
    public bool CopyUsers { get; set; } = false;
    public bool CopySettings { get; set; } = true;
    public List<string> ExcludeContentPaths { get; set; } = new();
    public List<string> ExcludeRoles { get; set; } = new();
    public Dictionary<string, string> RoleMapping { get; set; } = new();
}

/// <summary>
/// Predefined dataroom template
/// </summary>
public sealed class DataroomTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DataroomType Type { get; set; }
    public List<ContentStructureItem> ContentStructure { get; set; } = new();
    public List<DataroomRole> DefaultRoles { get; set; } = new();
    public DataroomSettings DefaultSettings { get; set; } = new();
    public string Industry { get; set; } = string.Empty;
    public string UseCase { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}

public enum UserLifecycleAction
{
    Onboard,
    ChangeRole,
    ExtendAccess,
    SuspendAccess,
    RestoreAccess,
    Offboard
}

public sealed class UserAccessReport
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public List<UserAccessSummary> UserSummaries { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class UserAccessSummary
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime LastAccess { get; set; }
    public int DocumentsAccessed { get; set; }
    public int DownloadsPerformed { get; set; }
}

#endregion