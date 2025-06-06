using System.ComponentModel.DataAnnotations;
using DatasiteUploader.Models.Permissions;

namespace DatasiteUploader.Models.CsvTemplates;

/// <summary>
/// CSV row model for role definition
/// </summary>
public sealed class RoleCsvRow
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string FeaturePermissions { get; set; } = string.Empty; // Comma-separated list
    
    public string? ParentRole { get; set; } // For role inheritance
    
    public bool IsActive { get; set; } = true;
    
    // Additional metadata
    public string? Department { get; set; }
    public string? CostCenter { get; set; }
    public int? MaxUsers { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

/// <summary>
/// CSV row model for content structure definition
/// </summary>
public sealed class ContentStructureCsvRow
{
    [Required]
    public string ContentName { get; set; } = string.Empty;
    
    [Required]
    public string ContentType { get; set; } = string.Empty; // "fileroom", "folder", "file"
    
    public string? ParentPath { get; set; } // Full path to parent
    
    public string? LocalFilePath { get; set; } // For file uploads
    
    public string? PublishingStatus { get; set; } = "Draft"; // "Draft", "Published"
    
    public bool NativeFileDownload { get; set; } = true;
    
    public bool DocumentDisclaimer { get; set; } = false;
    
    public string? Description { get; set; }
    
    public string? Tags { get; set; } // Comma-separated
    
    public int? SortOrder { get; set; }
    
    // Index tracking (optional - for Datasite compatibility)
    public int? IndexNumber { get; set; }
}

/// <summary>
/// CSV row model for permission assignment
/// </summary>
public sealed class PermissionCsvRow
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
    
    [Required]
    public string ContentPath { get; set; } = string.Empty; // Full path to content
    
    [Required]
    public string Permission { get; set; } = string.Empty; // "Hidden", "View", "Print", "Download", "Manage"
    
    public bool ApplyToChildren { get; set; } = true;
    
    public bool IsInherited { get; set; } = false;
    
    public string? InheritedFrom { get; set; } // Path of parent content
    
    public string? Reason { get; set; } // Business reason for permission
    
    public DateTime? EffectiveDate { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    // Index tracking (for Datasite export compatibility)
    public int? IndexNumber { get; set; }
    public string? IndexType { get; set; }
}

/// <summary>
/// CSV row model for user role assignment
/// </summary>
public sealed class UserAssignmentCsvRow
{
    [Required]
    public string UserEmail { get; set; } = string.Empty;
    
    public string? UserName { get; set; }
    
    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }
    
    public string? Organization { get; set; }
    
    [Required]
    public string RoleName { get; set; } = string.Empty;
    
    public string? Department { get; set; }
    
    public string? Title { get; set; }
    
    public DateTime? AssignmentDate { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    public bool SendInvitation { get; set; } = true;
    
    public string? InvitationMessage { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Complete project setup CSV row (all-in-one format)
/// </summary>
public sealed class CompleteSetupCsvRow
{
    // Content Definition
    [Required]
    public string ContentPath { get; set; } = string.Empty;
    
    public string? ContentType { get; set; } = "folder";
    
    public string? LocalFilePath { get; set; }
    
    public string? PublishingStatus { get; set; } = "Draft";
    
    // Role Definition
    public string? RoleName { get; set; }
    
    public string? RoleDescription { get; set; }
    
    public string? FeaturePermissions { get; set; }
    
    // Permission Assignment
    public string? Permission { get; set; } = "Hidden";
    
    public bool ApplyToChildren { get; set; } = true;
    
    // User Assignment
    public string? UserEmail { get; set; }
    
    public string? UserName { get; set; }
    
    public bool SendInvitation { get; set; } = false;
    
    // Metadata
    public int? SortOrder { get; set; }
    
    public string? Tags { get; set; }
    
    public string? Description { get; set; }
    
    // Index tracking
    public int? IndexNumber { get; set; }
    
    public string? IndexType { get; set; }
}

/// <summary>
/// CSV validation result for a single row
/// </summary>
public sealed class CsvRowValidationResult
{
    public int RowNumber { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public object? ParsedRow { get; set; }
}

/// <summary>
/// Complete CSV validation result
/// </summary>
public sealed class CsvValidationResult
{
    public bool IsValid { get; set; } = true;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public List<CsvRowValidationResult> RowResults { get; set; } = new();
    public List<string> GlobalErrors { get; set; } = new();
    public List<string> GlobalWarnings { get; set; } = new();
    public Dictionary<string, int> ColumnStats { get; set; } = new();
}

/// <summary>
/// CSV import statistics
/// </summary>
public sealed class CsvImportStatistics
{
    public int TotalRowsProcessed { get; set; }
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public int SkippedRows { get; set; }
    public int DuplicatesFound { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public DateTime ImportStartTime { get; set; }
    public DateTime ImportEndTime { get; set; }
    public string ImportedByUserId { get; set; } = string.Empty;
    public string ImportSessionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Template generation options
/// </summary>
public sealed class CsvTemplateOptions
{
    public bool IncludeExamples { get; set; } = true;
    public bool IncludeInstructions { get; set; } = true;
    public bool IncludeValidationRules { get; set; } = true;
    public bool IncludeExistingData { get; set; } = false;
    public string? FilterByRole { get; set; }
    public string? FilterByContentType { get; set; }
    public List<string> IncludeColumns { get; set; } = new();
    public List<string> ExcludeColumns { get; set; } = new();
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public string Delimiter { get; set; } = ",";
    public bool UseQuotes { get; set; } = true;
}

/// <summary>
/// Datasite export compatibility mapping
/// </summary>
public sealed class DatasiteExportMapping
{
    public Dictionary<string, string> PermissionMapping { get; set; } = new()
    {
        { "View/Print/Download", "Download" },
        { "View/Print", "Print" },
        { "View", "View" },
        { "Hidden", "Hidden" },
        { "Manage", "Manage" }
    };
    
    public Dictionary<string, string> ContentTypeMapping { get; set; } = new()
    {
        { "FILEROOM", "fileroom" },
        { "FOLDER", "folder" },
        { "FILE", "file" }
    };
    
    public Dictionary<string, FeaturePermissions> FeatureMapping { get; set; } = new()
    {
        { "Publishing", FeaturePermissions.Publishing },
        { "Download Multiple Files", FeaturePermissions.DownloadMultipleFiles },
        { "Inviting a User", FeaturePermissions.InviteUser },
        { "Analytics", FeaturePermissions.Analytics },
        { "Redaction", FeaturePermissions.Redaction },
        { "Users", FeaturePermissions.UserManagement },
        { "Dashboards", FeaturePermissions.Dashboards },
        { "Trackers", FeaturePermissions.Trackers }
    };
}

/// <summary>
/// CSV processing options
/// </summary>
public sealed class CsvProcessingOptions
{
    public bool SkipDuplicates { get; set; } = true;
    public bool ContinueOnError { get; set; } = true;
    public bool ValidateReferences { get; set; } = true;
    public bool CreateMissingParents { get; set; } = false;
    public bool OverwriteExisting { get; set; } = false;
    public int BatchSize { get; set; } = 100;
    public int MaxErrors { get; set; } = 50;
    public bool DryRun { get; set; } = false;
    public bool LogAllOperations { get; set; } = true;
    public string? ImportReason { get; set; }
}