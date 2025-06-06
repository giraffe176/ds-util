using System.Text.Json.Serialization;

namespace DatasiteUploader.Models;

/// <summary>
/// OAuth2 credentials for Datasite authentication
/// </summary>
public sealed record Credentials(
    string ClientId,
    string ClientSecret,
    string RedirectUri,
    string AuthCode);

/// <summary>
/// User information returned from OAuth2 authentication
/// </summary>
public sealed class UserInfo
{
    [JsonPropertyName("mail")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastname")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("organization_name")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("application_name")]
    public string ApplicationName { get; set; } = string.Empty;

    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// OAuth2 token response
/// </summary>
public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("mail")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastname")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("organization_name")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("application_name")]
    public string ApplicationName { get; set; } = string.Empty;

    public UserInfo ToUserInfo()
    {
        return new UserInfo
        {
            Email = Email,
            FirstName = FirstName,
            LastName = LastName,
            Organization = Organization,
            OrganizationId = OrganizationId,
            Subject = Subject,
            ApplicationName = ApplicationName
        };
    }
}

/// <summary>
/// Datasite project information
/// </summary>
public sealed class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    public string DisplayText => $"{Name} (ID: {Id}, State: {State})";
}

/// <summary>
/// API response wrapper for collections
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();

    [JsonPropertyName("pagination")]
    public PaginationInfo? Pagination { get; set; }
}

/// <summary>
/// API response wrapper for single items
/// </summary>
public sealed class ApiSingleResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

/// <summary>
/// Pagination information
/// </summary>
public sealed class PaginationInfo
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

/// <summary>
/// Upload destination information with hierarchical navigation support
/// </summary>
public sealed class Destination
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public bool CanUpload { get; set; } = true;

    public string DisplayText => $"[{Type.ToUpper()}] {Path}";
    public bool IsFileroom => string.Equals(Type, "Fileroom", StringComparison.OrdinalIgnoreCase);
    public bool IsFolder => string.Equals(Type, "Folder", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Fileroom information
/// </summary>
public sealed class Fileroom
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>
/// Metadata item (file or folder)
/// </summary>
public sealed class MetadataItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("publishingStatus")]
    public string? PublishingStatus { get; set; }

    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [JsonPropertyName("modified")]
    public DateTime? Modified { get; set; }

    public bool IsFolder => string.Equals(Type, "FOLDER", StringComparison.OrdinalIgnoreCase);
    public bool IsFile => string.Equals(Type, "FILE", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Upload progress information
/// </summary>
public sealed class UploadProgress
{
    public string FileName { get; set; } = string.Empty;
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public bool IsCompleted => BytesTransferred >= TotalBytes && string.IsNullOrEmpty(ErrorMessage);
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// Upload statistics
/// </summary>
public sealed class UploadStatistics
{
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public int FailedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int PendingFiles => TotalFiles - CompletedFiles - FailedFiles;
    public double OverallProgress => TotalFiles > 0 ? (double)CompletedFiles / TotalFiles * 100 : 0;
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
    public double TransferRateMBps => Duration.TotalSeconds > 0 ? TransferredBytes / 1024.0 / 1024.0 / Duration.TotalSeconds : 0;
}

/// <summary>
/// Application configuration
/// </summary>
public sealed class DatasiteConfig
{
    public string BaseUrl { get; set; } = "https://api.americas.datasite.com";
    public string TokenUrl { get; set; } = "https://token.datasite.com/oauth2/token";
    public string AuthBaseUrl { get; set; } = "https://token.datasite.com/oauth2/authorize";
    public string RefreshTokenUrl { get; set; } = "https://token.datasite.com/oauth2/refresh_token";
    public string ApiVersion { get; set; } = "2024-04-01";
    public string DefaultRedirectUri { get; set; } = "http://localhost:8080/callback";
    public long MaxFileSize { get; set; } = 1073741824; // 1GB
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int RequestTimeoutMs { get; set; } = 300000; // 5 minutes
    public int ChunkSize { get; set; } = 8388608; // 8MB
}

/// <summary>
/// Environment configuration
/// </summary>
public sealed class EnvironmentConfig
{
    public bool SaveCredentials { get; set; } = true;
    public bool ValidateFileTypes { get; set; } = true;
    public List<string> AllowedExtensions { get; set; } = new();
    public List<string> BlockedExtensions { get; set; } = new();
}

/// <summary>
/// Navigation action for hierarchical folder browsing
/// </summary>
public enum NavigationAction
{
    /// <summary>
    /// Enter the selected folder
    /// </summary>
    EnterFolder,
    
    /// <summary>
    /// Upload to the current location
    /// </summary>
    UploadHere,
    
    /// <summary>
    /// Go back to parent folder/fileroom
    /// </summary>
    GoBack,
    
    /// <summary>
    /// Create a new folder in current location
    /// </summary>
    CreateFolder,
    
    /// <summary>
    /// Cancel navigation
    /// </summary>
    Cancel
}