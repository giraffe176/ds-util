using DatasiteUploader.Models;

namespace DatasiteUploader.Services;

/// <summary>
/// Interface for Datasite API operations
/// </summary>
public interface IDatasiteApiService : IDisposable
{
    /// <summary>
    /// Authenticates using OAuth2 authorization code flow
    /// </summary>
    /// <param name="credentials">OAuth2 credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication result with tokens and user info</returns>
    Task<(bool Success, string? AccessToken, string? RefreshToken, UserInfo? UserInfo, string? ErrorMessage)> AuthenticateAsync(
        Credentials credentials, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the access token using a refresh token
    /// </summary>
    /// <param name="refreshToken">Refresh token</param>
    /// <param name="clientId">OAuth2 client ID</param>
    /// <param name="clientSecret">OAuth2 client secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access token and refresh token</returns>
    Task<(bool Success, string? AccessToken, string? RefreshToken, string? ErrorMessage)> RefreshTokenAsync(
        string refreshToken, 
        string clientId, 
        string clientSecret, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the access token for API requests
    /// </summary>
    /// <param name="accessToken">Bearer access token</param>
    void SetAccessToken(string accessToken);

    /// <summary>
    /// Gets all projects accessible to the authenticated user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects</returns>
    Task<(bool Success, List<Project> Projects, string? ErrorMessage)> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all filerooms in a project
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of filerooms</returns>
    Task<(bool Success, List<Fileroom> Filerooms, string? ErrorMessage)> GetFileroomsAsync(
        string projectId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata children (files and folders) of a parent item
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="parentId">Parent metadata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of child metadata items</returns>
    Task<(bool Success, List<MetadataItem> Children, string? ErrorMessage)> GetMetadataChildrenAsync(
        string projectId, 
        string parentId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder in the specified parent location
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="parentId">Parent metadata ID</param>
    /// <param name="folderName">Name of the new folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created folder metadata</returns>
    Task<(bool Success, MetadataItem? Folder, string? ErrorMessage)> CreateFolderAsync(
        string projectId, 
        string parentId, 
        string folderName, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a single file to the specified destination
    /// </summary>
    /// <param name="filePath">Local file path</param>
    /// <param name="projectId">Project ID</param>
    /// <param name="destinationId">Destination metadata ID</param>
    /// <param name="fileName">Optional custom file name</param>
    /// <param name="progress">Progress reporting callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with created file metadata</returns>
    Task<(bool Success, MetadataItem? File, string? ErrorMessage)> UploadFileAsync(
        string filePath, 
        string projectId, 
        string destinationId, 
        string? fileName = null,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file type is allowed for upload
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <returns>True if file type is allowed</returns>
    bool IsFileTypeAllowed(string filePath);

    /// <summary>
    /// Validates if a file size is within limits
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <returns>True if file size is within limits</returns>
    bool IsFileSizeAllowed(string filePath);

    /// <summary>
    /// Gets the current API health status
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<(bool IsHealthy, string? ErrorMessage)> CheckHealthAsync(CancellationToken cancellationToken = default);
}