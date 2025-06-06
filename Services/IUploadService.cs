using DatasiteUploader.Models;

namespace DatasiteUploader.Services;

/// <summary>
/// Interface for high-level upload operations with comprehensive progress tracking
/// </summary>
public interface IUploadService
{
    /// <summary>
    /// Uploads a single file to the specified destination
    /// </summary>
    /// <param name="filePath">Local file path</param>
    /// <param name="destination">Upload destination</param>
    /// <param name="progress">Progress reporting callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result</returns>
    Task<(bool Success, string? ErrorMessage)> UploadFileAsync(
        string filePath, 
        Destination destination,
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an entire folder structure recursively
    /// </summary>
    /// <param name="folderPath">Local folder path</param>
    /// <param name="destination">Upload destination</param>
    /// <param name="progress">Progress reporting callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result</returns>
    Task<(bool Success, string? ErrorMessage)> UploadFolderAsync(
        string folderPath, 
        Destination destination,
        IProgress<UploadStatistics>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a path can be uploaded (file exists, folder exists, permissions, etc.)
    /// </summary>
    /// <param name="path">Path to validate</param>
    /// <returns>Validation result with detailed error message if invalid</returns>
    (bool IsValid, string? ErrorMessage) ValidateUploadPath(string path);

    /// <summary>
    /// Gets upload statistics for a given path (file count, total size, etc.)
    /// </summary>
    /// <param name="path">Path to analyze</param>
    /// <returns>Upload statistics preview</returns>
    Task<UploadStatistics> GetUploadPreviewAsync(string path);

    /// <summary>
    /// Cancels all ongoing uploads
    /// </summary>
    void CancelAllUploads();
}

/// <summary>
/// Interface for user interaction and console operations
/// </summary>
public interface IUserInterface
{
    /// <summary>
    /// Displays the application banner
    /// </summary>
    void ShowBanner();

    /// <summary>
    /// Prompts user for OAuth2 credentials with environment variable fallback
    /// </summary>
    /// <returns>User credentials or null if cancelled</returns>
    Task<Credentials?> GetCredentialsAsync();

    /// <summary>
    /// Displays projects and prompts user to select one
    /// </summary>
    /// <param name="projects">Available projects</param>
    /// <returns>Selected project or null if cancelled</returns>
    Project? SelectProject(List<Project> projects);

    /// <summary>
    /// Displays destinations and prompts user to select one
    /// </summary>
    /// <param name="destinations">Available destinations</param>
    /// <returns>Selected destination or null if cancelled</returns>
    Destination? SelectDestination(List<Destination> destinations);

    /// <summary>
    /// Displays filerooms and prompts user to select one for hierarchical navigation
    /// </summary>
    /// <param name="filerooms">Available filerooms</param>
    /// <returns>Selected fileroom or null if cancelled</returns>
    Destination? SelectFileroom(List<Destination> filerooms);

    /// <summary>
    /// Displays current location and folders with navigation options
    /// </summary>
    /// <param name="currentLocation">Current destination</param>
    /// <param name="folders">Available folders in current location</param>
    /// <returns>Navigation choice: selected folder, upload here, go back, or cancel</returns>
    (NavigationAction Action, Destination? Destination) NavigateFolder(Destination currentLocation, List<Destination> folders);

    /// <summary>
    /// Prompts user to select upload path (file or folder)
    /// </summary>
    /// <returns>Selected path or null if cancelled</returns>
    string? GetUploadPath();

    /// <summary>
    /// Displays upload progress in real-time
    /// </summary>
    /// <param name="statistics">Upload statistics</param>
    void DisplayProgress(UploadStatistics statistics);

    /// <summary>
    /// Displays upload completion summary
    /// </summary>
    /// <param name="statistics">Final upload statistics</param>
    /// <param name="success">Whether upload completed successfully</param>
    void DisplaySummary(UploadStatistics statistics, bool success);

    /// <summary>
    /// Displays an error message
    /// </summary>
    /// <param name="message">Error message</param>
    void DisplayError(string message);

    /// <summary>
    /// Displays a success message
    /// </summary>
    /// <param name="message">Success message</param>
    void DisplaySuccess(string message);

    /// <summary>
    /// Displays an informational message
    /// </summary>
    /// <param name="message">Information message</param>
    void DisplayInfo(string message);

    /// <summary>
    /// Displays a warning message
    /// </summary>
    /// <param name="message">Warning message</param>
    void DisplayWarning(string message);

    /// <summary>
    /// Prompts user for confirmation
    /// </summary>
    /// <param name="message">Confirmation message</param>
    /// <returns>True if user confirmed</returns>
    bool Confirm(string message);

    /// <summary>
    /// Waits for user to press any key
    /// </summary>
    /// <param name="message">Optional message to display</param>
    void WaitForUser(string? message = null);
}

/// <summary>
/// Interface for destination browsing and management with hierarchical navigation
/// </summary>
public interface IDestinationService
{
    /// <summary>
    /// Gets filerooms for initial navigation (fast initialization)
    /// </summary>
    /// <param name="project">Target project</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of filerooms</returns>
    Task<(bool Success, List<Destination> Filerooms, string? ErrorMessage)> GetFileroomsAsync(
        Project project, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets folders within a specific parent (fileroom or folder) for hierarchical navigation
    /// </summary>
    /// <param name="parentDestination">Parent destination to browse</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of folders within the parent</returns>
    Task<(bool Success, List<Destination> Folders, string? ErrorMessage)> GetFoldersAsync(
        Destination parentDestination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available upload destinations for a project (legacy method)
    /// </summary>
    /// <param name="project">Target project</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available destinations</returns>
    [Obsolete("Use GetFileroomsAsync and GetFoldersAsync for hierarchical navigation")]
    Task<(bool Success, List<Destination> Destinations, string? ErrorMessage)> GetDestinationsAsync(
        Project project, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder destination if it doesn't exist
    /// </summary>
    /// <param name="parentDestination">Parent destination</param>
    /// <param name="folderName">Name of new folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created destination</returns>
    Task<(bool Success, Destination? Destination, string? ErrorMessage)> CreateDestinationAsync(
        Destination parentDestination,
        string folderName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a destination is accessible and writable
    /// </summary>
    /// <param name="destination">Destination to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<(bool IsValid, string? ErrorMessage)> ValidateDestinationAsync(
        Destination destination,
        CancellationToken cancellationToken = default);
}