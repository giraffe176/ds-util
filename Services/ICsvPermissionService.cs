using DatasiteUploader.Models.Permissions;

namespace DatasiteUploader.Services;

/// <summary>
/// Service for importing/exporting permissions via CSV templates
/// Provides enterprise-ready bulk permission management
/// </summary>
public interface ICsvPermissionService
{
    #region CSV Template Management
    
    /// <summary>
    /// Generate CSV template for permission setup
    /// </summary>
    Task<string> GeneratePermissionTemplateAsync(string projectId, bool includeExistingPermissions = false);
    
    /// <summary>
    /// Generate role template CSV for creating multiple roles
    /// </summary>
    Task<string> GenerateRoleTemplateAsync(string projectId);
    
    /// <summary>
    /// Generate content structure template CSV for folder/fileroom setup
    /// </summary>
    Task<string> GenerateContentStructureTemplateAsync(string projectId);
    
    /// <summary>
    /// Generate user assignment template CSV
    /// </summary>
    Task<string> GenerateUserAssignmentTemplateAsync(string projectId);
    
    #endregion
    
    #region CSV Import Operations
    
    /// <summary>
    /// Import roles from CSV template
    /// </summary>
    Task<(bool Success, List<string> CreatedRoleIds, List<string> Errors)> ImportRolesFromCsvAsync(
        string csvContent, string projectId, string importedByUserId);
    
    /// <summary>
    /// Import content structure from CSV template
    /// </summary>
    Task<(bool Success, List<string> CreatedContentIds, List<string> Errors)> ImportContentStructureFromCsvAsync(
        string csvContent, string projectId, string importedByUserId);
    
    /// <summary>
    /// Import permissions from CSV template
    /// </summary>
    Task<(bool Success, int ProcessedPermissions, List<string> Errors)> ImportPermissionsFromCsvAsync(
        string csvContent, string projectId, string importedByUserId);
    
    /// <summary>
    /// Import user role assignments from CSV template
    /// </summary>
    Task<(bool Success, int ProcessedAssignments, List<string> Errors)> ImportUserAssignmentsFromCsvAsync(
        string csvContent, string projectId, string importedByUserId);
    
    /// <summary>
    /// Complete project setup from multiple CSV files
    /// </summary>
    Task<(bool Success, ProjectSetupResult Result, List<string> Errors)> ImportCompleteProjectSetupAsync(
        ProjectSetupCsvFiles csvFiles, string projectId, string importedByUserId);
    
    #endregion
    
    #region CSV Export Operations
    
    /// <summary>
    /// Export current project permissions to CSV
    /// </summary>
    Task<string> ExportProjectPermissionsToCsvAsync(string projectId);
    
    /// <summary>
    /// Export project roles to CSV
    /// </summary>
    Task<string> ExportProjectRolesToCsvAsync(string projectId);
    
    /// <summary>
    /// Export content structure to CSV
    /// </summary>
    Task<string> ExportContentStructureToCsvAsync(string projectId);
    
    /// <summary>
    /// Export user assignments to CSV
    /// </summary>
    Task<string> ExportUserAssignmentsToCsvAsync(string projectId);
    
    /// <summary>
    /// Export complete project configuration to multiple CSV files
    /// </summary>
    Task<ProjectExportResult> ExportCompleteProjectConfigurationAsync(string projectId);
    
    #endregion
    
    #region Validation and Preview
    
    /// <summary>
    /// Validate CSV content before import
    /// </summary>
    Task<(bool IsValid, List<string> Errors, List<string> Warnings)> ValidateCsvContentAsync(
        string csvContent, CsvTemplateType templateType, string projectId);
    
    /// <summary>
    /// Preview changes that would be made by importing CSV
    /// </summary>
    Task<CsvImportPreview> PreviewCsvImportAsync(
        string csvContent, CsvTemplateType templateType, string projectId);
    
    /// <summary>
    /// Validate complete project setup before import
    /// </summary>
    Task<(bool IsValid, List<string> Errors, List<string> Warnings)> ValidateCompleteProjectSetupAsync(
        ProjectSetupCsvFiles csvFiles, string projectId);
    
    #endregion
    
    #region Batch Operations
    
    /// <summary>
    /// Process multiple CSV operations in correct order
    /// </summary>
    Task<(bool Success, BatchProcessResult Result)> ProcessBatchCsvOperationsAsync(
        List<CsvOperation> operations, string projectId, string processedByUserId);
    
    /// <summary>
    /// Rollback CSV import operations
    /// </summary>
    Task<bool> RollbackCsvImportAsync(string importSessionId, string rolledBackByUserId);
    
    #endregion
}

/// <summary>
/// Types of CSV templates supported
/// </summary>
public enum CsvTemplateType
{
    Roles,
    ContentStructure,
    Permissions,
    UserAssignments,
    CompleteSetup
}

/// <summary>
/// CSV files for complete project setup
/// </summary>
public sealed class ProjectSetupCsvFiles
{
    public string? RolesCsv { get; set; }
    public string? ContentStructureCsv { get; set; }
    public string? PermissionsCsv { get; set; }
    public string? UserAssignmentsCsv { get; set; }
    public bool CreateMissingUsers { get; set; } = false;
    public bool OverwriteExisting { get; set; } = false;
}

/// <summary>
/// Result of project setup import
/// </summary>
public sealed class ProjectSetupResult
{
    public int RolesCreated { get; set; }
    public int ContentItemsCreated { get; set; }
    public int PermissionsSet { get; set; }
    public int UsersAssigned { get; set; }
    public TimeSpan Duration { get; set; }
    public string ImportSessionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Result of project export
/// </summary>
public sealed class ProjectExportResult
{
    public string RolesCsv { get; set; } = string.Empty;
    public string ContentStructureCsv { get; set; } = string.Empty;
    public string PermissionsCsv { get; set; } = string.Empty;
    public string UserAssignmentsCsv { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string ExportedByUserId { get; set; } = string.Empty;
}

/// <summary>
/// Preview of changes that would be made by CSV import
/// </summary>
public sealed class CsvImportPreview
{
    public int ItemsToCreate { get; set; }
    public int ItemsToUpdate { get; set; }
    public int ItemsToSkip { get; set; }
    public List<string> NewRoles { get; set; } = new();
    public List<string> NewContentItems { get; set; } = new();
    public List<string> PermissionChanges { get; set; } = new();
    public List<string> UserAssignmentChanges { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Single CSV operation for batch processing
/// </summary>
public sealed class CsvOperation
{
    public CsvTemplateType Type { get; set; }
    public string CsvContent { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsCritical { get; set; } = true; // Stop processing if this fails
}

/// <summary>
/// Result of batch CSV processing
/// </summary>
public sealed class BatchProcessResult
{
    public int OperationsCompleted { get; set; }
    public int OperationsFailed { get; set; }
    public List<string> CompletedOperations { get; set; } = new();
    public List<string> FailedOperations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public string BatchSessionId { get; set; } = Guid.NewGuid().ToString();
}