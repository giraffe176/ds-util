using DatasiteUploader.Models.Permissions;
using DatasiteUploader.Models.CsvTemplates;

namespace DatasiteUploader.Models;

/// <summary>
/// Request for bulk content upload
/// </summary>
public sealed class BulkUploadRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public List<UploadItem> Items { get; set; } = new();
    public bool PreserveStructure { get; set; } = true;
    public bool AutoPublish { get; set; } = false;
    public Dictionary<string, ContentPermissionLevel> DefaultPermissions { get; set; } = new();
    public string UploadedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Individual item for bulk upload
/// </summary>
public sealed class UploadItem
{
    public string LocalPath { get; set; } = string.Empty;
    public string? TargetPath { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public int SortOrder { get; set; }
}

/// <summary>
/// Result of bulk upload operation
/// </summary>
public sealed class BulkUploadResult
{
    public int TotalItems { get; set; }
    public int SuccessfulUploads { get; set; }
    public int FailedUploads { get; set; }
    public List<string> UploadedContentIds { get; set; } = new();
    public List<string> FailedItems { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public long TotalBytesUploaded { get; set; }
}

/// <summary>
/// Rule for content reorganization
/// </summary>
public sealed class ContentReorganizationRule
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public ReorganizationType Type { get; set; } = ReorganizationType.Move;
    public bool ApplyToChildren { get; set; } = true;
    public string? Reason { get; set; }
}

public enum ReorganizationType
{
    Move,
    Copy,
    Rename,
    Delete
}

/// <summary>
/// Result of user setup operation
/// </summary>
public sealed class UserSetupResult
{
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int InvitationsSent { get; set; }
    public int RoleAssignments { get; set; }
    public List<string> CreatedUserIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request for user invitation
/// </summary>
public sealed class UserInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Organization { get; set; }
    public List<string> RoleIds { get; set; } = new();
    public string? WelcomeMessage { get; set; }
    public DateTime? AccessExpiry { get; set; }
    public bool SendImmediately { get; set; } = true;
}

/// <summary>
/// Permission template for applying standard permissions
/// </summary>
public sealed class PermissionTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TemplatePermissionRule> Rules { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Individual permission rule in a template
/// </summary>
public sealed class TemplatePermissionRule
{
    public string RoleId { get; set; } = string.Empty;
    public string ContentPathPattern { get; set; } = string.Empty; // Supports wildcards
    public ContentPermissionLevel PermissionLevel { get; set; }
    public bool ApplyToChildren { get; set; } = true;
    public string? Condition { get; set; } // Optional condition for applying rule
}

/// <summary>
/// Permission audit report
/// </summary>
public sealed class PermissionAuditReport
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratedByUserId { get; set; } = string.Empty;
    public PermissionAuditSummary Summary { get; set; } = new();
    public List<PermissionAuditIssue> Issues { get; set; } = new();
    public List<PermissionAuditRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Summary of permission audit
/// </summary>
public sealed class PermissionAuditSummary
{
    public int TotalRoles { get; set; }
    public int TotalUsers { get; set; }
    public int TotalContentItems { get; set; }
    public int TotalPermissions { get; set; }
    public int OrphanedPermissions { get; set; }
    public int OverprivilegedUsers { get; set; }
    public int UnusedRoles { get; set; }
    public int ExpiredAccess { get; set; }
}

/// <summary>
/// Individual audit issue
/// </summary>
public sealed class PermissionAuditIssue
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public AuditIssueType Type { get; set; }
    public AuditIssueSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? TargetType { get; set; }
    public string? RecommendedAction { get; set; }
}

public enum AuditIssueType
{
    OrphanedPermission,
    OverprivilegedAccess,
    UnusedRole,
    ExpiredAccess,
    InconsistentPermissions,
    SecurityRisk,
    ComplianceViolation
}

public enum AuditIssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Audit recommendation
/// </summary>
public sealed class PermissionAuditRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? AutofixAvailable { get; set; }
}

/// <summary>
/// Permission matrix for visualization
/// </summary>
public sealed class PermissionMatrix
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<PermissionMatrixRow> Rows { get; set; } = new();
    public List<PermissionMatrixColumn> Columns { get; set; } = new();
    public PermissionMatrixOptions Options { get; set; } = new();
}

/// <summary>
/// Row in permission matrix (content item)
/// </summary>
public sealed class PermissionMatrixRow
{
    public string ContentId { get; set; } = string.Empty;
    public string ContentName { get; set; } = string.Empty;
    public string ContentPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Dictionary<string, ContentPermissionLevel> RolePermissions { get; set; } = new();
}

/// <summary>
/// Column in permission matrix (role)
/// </summary>
public sealed class PermissionMatrixColumn
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

/// <summary>
/// Options for generating permission matrix
/// </summary>
public sealed class PermissionMatrixOptions
{
    public bool IncludeInheritedPermissions { get; set; } = true;
    public bool IncludeSystemRoles { get; set; } = false;
    public bool IncludeUnpublishedContent { get; set; } = false;
    public List<string> FilterByRoleIds { get; set; } = new();
    public List<string> FilterByContentTypes { get; set; } = new();
    public ContentPermissionLevel MinimumPermissionLevel { get; set; } = ContentPermissionLevel.View;
}

/// <summary>
/// Dataroom analytics data
/// </summary>
public sealed class DataroomAnalytics
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DataroomUsageStats Usage { get; set; } = new();
    public List<DataroomUserActivity> UserActivity { get; set; } = new();
    public List<DataroomContentStats> ContentStats { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Usage statistics
/// </summary>
public sealed class DataroomUsageStats
{
    public int TotalLogins { get; set; }
    public int UniqueUsers { get; set; }
    public int DocumentViews { get; set; }
    public int Downloads { get; set; }
    public int Uploads { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public int PeakConcurrentUsers { get; set; }
}

/// <summary>
/// User activity data
/// </summary>
public sealed class DataroomUserActivity
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime LastLogin { get; set; }
    public int LoginCount { get; set; }
    public int DocumentsViewed { get; set; }
    public int FilesDownloaded { get; set; }
    public TimeSpan TotalTimeSpent { get; set; }
}

/// <summary>
/// Content statistics
/// </summary>
public sealed class DataroomContentStats
{
    public string ContentId { get; set; } = string.Empty;
    public string ContentName { get; set; } = string.Empty;
    public string ContentPath { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
    public int UniqueViewers { get; set; }
    public DateTime LastAccessed { get; set; }
    public List<string> TopUsers { get; set; } = new();
}

/// <summary>
/// Dataroom alert
/// </summary>
public sealed class DataroomAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; } = false;
    public string? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum AlertType
{
    SecurityBreach,
    UnusualActivity,
    AccessExpiration,
    PermissionChange,
    SystemIssue,
    ComplianceViolation
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Executive dashboard data
/// </summary>
public sealed class DataroomDashboard
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DataroomOverview Overview { get; set; } = new();
    public List<DataroomMetric> KeyMetrics { get; set; } = new();
    public List<DataroomAlert> RecentAlerts { get; set; } = new();
    public DataroomTrend ActivityTrend { get; set; } = new();
}

/// <summary>
/// Dataroom overview
/// </summary>
public sealed class DataroomOverview
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalDocuments { get; set; }
    public int PublishedDocuments { get; set; }
    public long TotalStorageUsed { get; set; }
    public DateTime LastActivity { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
}

/// <summary>
/// Key metric for dashboard
/// </summary>
public sealed class DataroomMetric
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double? Change { get; set; }
    public string? ChangeDirection { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Activity trend data
/// </summary>
public sealed class DataroomTrend
{
    public List<DateTime> Dates { get; set; } = new();
    public List<int> Logins { get; set; } = new();
    public List<int> DocumentViews { get; set; } = new();
    public List<int> Downloads { get; set; } = new();
}

/// <summary>
/// Export package for dataroom configuration
/// </summary>
public sealed class DataroomExportPackage
{
    public string Version { get; set; } = "1.0";
    public string SourceProjectId { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string ExportedByUserId { get; set; } = string.Empty;
    public DataroomExportData Data { get; set; } = new();
    public DataroomExportOptions Options { get; set; } = new();
}

/// <summary>
/// Exported dataroom data
/// </summary>
public sealed class DataroomExportData
{
    public List<DataroomRole> Roles { get; set; } = new();
    public List<ContentMetadata> Content { get; set; } = new();
    public List<ContentPermission> Permissions { get; set; } = new();
    public List<UserAssignmentCsvRow> UserAssignments { get; set; } = new();
    public DataroomSettings Settings { get; set; } = new();
}

/// <summary>
/// Options for dataroom export
/// </summary>
public sealed class DataroomExportOptions
{
    public bool IncludeContent { get; set; } = false;
    public bool IncludeUsers { get; set; } = true;
    public bool IncludePermissions { get; set; } = true;
    public bool IncludeSettings { get; set; } = true;
    public bool IncludeAuditLogs { get; set; } = false;
    public List<string> ExcludeRoles { get; set; } = new();
    public List<string> ExcludeContentTypes { get; set; } = new();
}

/// <summary>
/// Workflow definition for automation
/// </summary>
public sealed class DataroomWorkflow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public List<WorkflowTrigger> Triggers { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Individual step in workflow
/// </summary>
public sealed class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int Order { get; set; }
    public string? Condition { get; set; }
}

/// <summary>
/// Workflow trigger
/// </summary>
public sealed class WorkflowTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public WorkflowTriggerType Type { get; set; }
    public string? Schedule { get; set; } // Cron expression for scheduled triggers
    public string? EventType { get; set; } // Event type for event-based triggers
    public Dictionary<string, object> Conditions { get; set; } = new();
}

public enum WorkflowTriggerType
{
    Manual,
    Scheduled,
    Event,
    Webhook
}

/// <summary>
/// Result of workflow execution
/// </summary>
public sealed class WorkflowExecutionResult
{
    public string WorkflowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Running;
    public List<WorkflowStepResult> StepResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public enum WorkflowExecutionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Result of individual workflow step
/// </summary>
public sealed class WorkflowStepResult
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> OutputData { get; set; } = new();
}

/// <summary>
/// Maintenance task definition
/// </summary>
public sealed class MaintenanceTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MaintenanceTaskType Type { get; set; }
    public string Schedule { get; set; } = string.Empty; // Cron expression
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
}

public enum MaintenanceTaskType
{
    CleanupExpiredAccess,
    ArchiveOldContent,
    GenerateReports,
    BackupConfiguration,
    OptimizePermissions,
    ValidateIntegrity
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