using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DatasiteUploader.Models;
using Polly;
using Polly.Extensions.Http;

namespace DatasiteUploader.Services;

/// <summary>
/// Production-ready Datasite API service with comprehensive error handling and retry policies
/// </summary>
public sealed class DatasiteApiService : IDatasiteApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DatasiteApiService> _logger;
    private readonly DatasiteConfig _config;
    private readonly EnvironmentConfig _envConfig;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private bool _disposed;

    public DatasiteApiService(
        HttpClient httpClient, 
        ILogger<DatasiteApiService> logger, 
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _config = configuration.GetSection("Datasite").Get<DatasiteConfig>() ?? new DatasiteConfig();
        _envConfig = configuration.GetSection("Environment").Get<EnvironmentConfig>() ?? new EnvironmentConfig();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure HTTP client
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.RequestTimeoutMs);
        _httpClient.DefaultRequestHeaders.Add("x-datasite-api-version", _config.ApiVersion);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DatasiteUploader/1.0.0");

        // Configure retry policy with exponential backoff
        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: _config.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(_config.RetryDelayMs * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry attempt {RetryCount} in {Delay}ms", 
                        retryCount, timespan.TotalMilliseconds);
                });

        _logger.LogInformation("DatasiteApiService initialized with base URL: {BaseUrl}", _config.BaseUrl);
    }

    public async Task<(bool Success, string? AccessToken, string? RefreshToken, UserInfo? UserInfo, string? ErrorMessage)> AuthenticateAsync(
        Credentials credentials, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting OAuth2 authentication for client: {ClientId}", credentials.ClientId);

            var requestBody = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "authorization_code"),
                new("client_id", credentials.ClientId),
                new("client_secret", credentials.ClientSecret),
                new("code", credentials.AuthCode),
                new("redirect_uri", credentials.RedirectUri)
            };

            var content = new FormUrlEncodedContent(requestBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var request = new HttpRequestMessage(HttpMethod.Post, _config.TokenUrl)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
                return httpResponse;
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Authentication failed with status {response.StatusCode}: {errorContent}";
                _logger.LogError("OAuth2 authentication failed: {Error}", errorMessage);
                return (false, null, null, null, errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, _jsonOptions);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                const string errorMessage = "Invalid token response received from server";
                _logger.LogError("OAuth2 authentication failed: {Error}", errorMessage);
                return (false, null, null, null, errorMessage);
            }

            var userInfo = tokenResponse.ToUserInfo();
            _logger.LogInformation("Successfully authenticated user: {Email} from organization: {Organization}", 
                userInfo.Email, userInfo.Organization);

            // Save refresh token to environment if configured
            if (_envConfig.SaveCredentials && !string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                try
                {
                    Environment.SetEnvironmentVariable("DATASITE_REFRESH_TOKEN", tokenResponse.RefreshToken, EnvironmentVariableTarget.Machine);
                    _logger.LogInformation("Refresh token saved to environment variables");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save refresh token to environment variables");
                }
            }

            return (true, tokenResponse.AccessToken, tokenResponse.RefreshToken, userInfo, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Authentication error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during OAuth2 authentication");
            return (false, null, null, null, errorMessage);
        }
    }

    public async Task<(bool Success, string? AccessToken, string? RefreshToken, string? ErrorMessage)> RefreshTokenAsync(
        string refreshToken, 
        string clientId, 
        string clientSecret, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Refreshing access token for client: {ClientId}", clientId);

            var requestBody = new List<KeyValuePair<string, string>>
            {
                new("refresh_token", refreshToken),
                new("client_id", clientId),
                new("client_secret", clientSecret)
            };

            var content = new FormUrlEncodedContent(requestBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var request = new HttpRequestMessage(HttpMethod.Post, _config.RefreshTokenUrl)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
                return httpResponse;
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Token refresh failed with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Token refresh failed: {Error}", errorMessage);
                return (false, null, null, errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, _jsonOptions);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                const string errorMessage = "Invalid token refresh response received from server";
                _logger.LogError("Token refresh failed: {Error}", errorMessage);
                return (false, null, null, errorMessage);
            }

            _logger.LogInformation("Successfully refreshed access token");
            return (true, tokenResponse.AccessToken, tokenResponse.RefreshToken, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Token refresh error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during token refresh");
            return (false, null, null, errorMessage);
        }
    }

    public void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _logger.LogDebug("Access token set for API requests");
    }

    public async Task<(bool Success, List<Project> Projects, string? ErrorMessage)> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching user projects");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync($"{_config.BaseUrl}/projects", cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Failed to fetch projects with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Failed to fetch projects: {Error}", errorMessage);
                return (false, new List<Project>(), errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<Project>>(responseContent, _jsonOptions);

            var projects = apiResponse?.Data ?? new List<Project>();
            _logger.LogInformation("Successfully fetched {ProjectCount} projects", projects.Count);

            return (true, projects, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error fetching projects: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while fetching projects");
            return (false, new List<Project>(), errorMessage);
        }
    }

    public async Task<(bool Success, List<Fileroom> Filerooms, string? ErrorMessage)> GetFileroomsAsync(
        string projectId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(projectId);
            
            _logger.LogInformation("Fetching filerooms for project: {ProjectId}", projectId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync($"{_config.BaseUrl}/projects/{projectId}/filerooms", cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Failed to fetch filerooms with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Failed to fetch filerooms for project {ProjectId}: {Error}", projectId, errorMessage);
                return (false, new List<Fileroom>(), errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<Fileroom>>(responseContent, _jsonOptions);

            var filerooms = apiResponse?.Data ?? new List<Fileroom>();
            _logger.LogInformation("Successfully fetched {FileroomCount} filerooms for project {ProjectId}", 
                filerooms.Count, projectId);

            return (true, filerooms, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error fetching filerooms: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while fetching filerooms for project {ProjectId}", projectId);
            return (false, new List<Fileroom>(), errorMessage);
        }
    }

    public async Task<(bool Success, List<MetadataItem> Children, string? ErrorMessage)> GetMetadataChildrenAsync(
        string projectId, 
        string parentId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(projectId);
            ArgumentException.ThrowIfNullOrEmpty(parentId);
            
            _logger.LogDebug("Fetching metadata children for project: {ProjectId}, parent: {ParentId}", projectId, parentId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync($"{_config.BaseUrl}/projects/{projectId}/metadata/{parentId}/children", cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Failed to fetch metadata children with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Failed to fetch metadata children: {Error}", errorMessage);
                return (false, new List<MetadataItem>(), errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MetadataItem>>(responseContent, _jsonOptions);

            var children = apiResponse?.Data ?? new List<MetadataItem>();
            _logger.LogDebug("Successfully fetched {ChildCount} metadata children", children.Count);

            return (true, children, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error fetching metadata children: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while fetching metadata children");
            return (false, new List<MetadataItem>(), errorMessage);
        }
    }

    public async Task<(bool Success, MetadataItem? Folder, string? ErrorMessage)> CreateFolderAsync(
        string projectId, 
        string parentId, 
        string folderName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(projectId);
            ArgumentException.ThrowIfNullOrEmpty(parentId);
            ArgumentException.ThrowIfNullOrEmpty(folderName);
            
            _logger.LogInformation("Creating folder '{FolderName}' in project: {ProjectId}, parent: {ParentId}", 
                folderName, projectId, parentId);

            var requestData = new
            {
                data = new
                {
                    name = folderName,
                    type = "FOLDER",
                    publishingStatus = "UNPUBLISHED",
                    permissions = "INHERIT"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestData, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.PostAsync($"{_config.BaseUrl}/projects/{projectId}/metadata/{parentId}/children", content, cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Failed to create folder with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Failed to create folder '{FolderName}': {Error}", folderName, errorMessage);
                return (false, null, errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiSingleResponse<MetadataItem>>(responseContent, _jsonOptions);

            var folder = apiResponse?.Data;
            if (folder == null)
            {
                const string errorMessage = "Invalid folder creation response received from server";
                _logger.LogError("Failed to create folder '{FolderName}': {Error}", folderName, errorMessage);
                return (false, null, errorMessage);
            }

            _logger.LogInformation("Successfully created folder '{FolderName}' with ID: {FolderId}", folderName, folder.Id);
            return (true, folder, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error creating folder: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while creating folder '{FolderName}'", folderName);
            return (false, null, errorMessage);
        }
    }

    public async Task<(bool Success, MetadataItem? File, string? ErrorMessage)> UploadFileAsync(
        string filePath, 
        string projectId, 
        string destinationId, 
        string? fileName = null,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentException.ThrowIfNullOrEmpty(projectId);
            ArgumentException.ThrowIfNullOrEmpty(destinationId);

            if (!File.Exists(filePath))
            {
                var errorMessage = $"File not found: {filePath}";
                _logger.LogError("Upload failed: {Error}", errorMessage);
                return (false, null, errorMessage);
            }

            if (!IsFileTypeAllowed(filePath))
            {
                var errorMessage = $"File type not allowed: {Path.GetExtension(filePath)}";
                _logger.LogError("Upload failed: {Error}", errorMessage);
                return (false, null, errorMessage);
            }

            if (!IsFileSizeAllowed(filePath))
            {
                var fileSize = new FileInfo(filePath).Length;
                var errorMessage = $"File size {fileSize:N0} bytes exceeds maximum allowed size of {_config.MaxFileSize:N0} bytes";
                _logger.LogError("Upload failed: {Error}", errorMessage);
                return (false, null, errorMessage);
            }

            var fileInfo = new FileInfo(filePath);
            var uploadFileName = fileName ?? fileInfo.Name;
            
            _logger.LogInformation("Starting upload of file '{FileName}' ({FileSize:N0} bytes) to project: {ProjectId}, destination: {DestinationId}", 
                uploadFileName, fileInfo.Length, projectId, destinationId);

            var uploadProgress = new UploadProgress
            {
                FileName = uploadFileName,
                TotalBytes = fileInfo.Length,
                Status = "Uploading"
            };

            // Create multipart form data
            using var multipartContent = new MultipartFormDataContent($"Upload-{Guid.NewGuid()}");

            // Add metadata
            var metadata = new
            {
                filename = uploadFileName,
                publishingStatus = "UNPUBLISHED",
                permissions = "INHERIT"
            };

            var metadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);
            multipartContent.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"), "data");

            // Add file content with progress tracking
            var fileContent = new ProgressStreamContent(filePath, _config.ChunkSize, progress, uploadProgress, cancellationToken);
            multipartContent.Add(fileContent, "file", uploadFileName);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.PostAsync($"{_config.BaseUrl}/projects/{projectId}/metadata/{destinationId}/children", multipartContent, cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Failed to upload file with status {response.StatusCode}: {errorContent}";
                _logger.LogError("Failed to upload file '{FileName}': {Error}", uploadFileName, errorMessage);
                
                uploadProgress.Status = "Failed";
                uploadProgress.ErrorMessage = errorMessage;
                progress?.Report(uploadProgress);
                
                return (false, null, errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiSingleResponse<MetadataItem>>(responseContent, _jsonOptions);

            var uploadedFile = apiResponse?.Data;
            if (uploadedFile == null)
            {
                const string errorMessage = "Invalid file upload response received from server";
                _logger.LogError("Failed to upload file '{FileName}': {Error}", uploadFileName, errorMessage);
                
                uploadProgress.Status = "Failed";
                uploadProgress.ErrorMessage = errorMessage;
                progress?.Report(uploadProgress);
                
                return (false, null, errorMessage);
            }

            uploadProgress.Status = "Completed";
            uploadProgress.BytesTransferred = uploadProgress.TotalBytes;
            progress?.Report(uploadProgress);

            _logger.LogInformation("Successfully uploaded file '{FileName}' with ID: {FileId}", uploadFileName, uploadedFile.Id);
            return (true, uploadedFile, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error uploading file: {ex.Message}";
            _logger.LogError(ex, "Unexpected error while uploading file '{FileName}'", fileName ?? filePath);
            return (false, null, errorMessage);
        }
    }

    public bool IsFileTypeAllowed(string filePath)
    {
        if (!_envConfig.ValidateFileTypes)
            return true;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Check blocked extensions first
        if (_envConfig.BlockedExtensions.Any(blocked => string.Equals(blocked, extension, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("File type '{Extension}' is blocked", extension);
            return false;
        }

        // If allowed extensions are specified, file must be in the list
        if (_envConfig.AllowedExtensions.Any())
        {
            var isAllowed = _envConfig.AllowedExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed)
            {
                _logger.LogWarning("File type '{Extension}' is not in allowed list", extension);
            }
            return isAllowed;
        }

        return true;
    }

    public bool IsFileSizeAllowed(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var isAllowed = fileInfo.Length <= _config.MaxFileSize;
        
        if (!isAllowed)
        {
            _logger.LogWarning("File size {FileSize:N0} bytes exceeds maximum allowed size of {MaxSize:N0} bytes", 
                fileInfo.Length, _config.MaxFileSize);
        }
        
        return isAllowed;
    }

    public async Task<(bool IsHealthy, string? ErrorMessage)> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing API health check");

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/health", cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;
            
            if (!isHealthy)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Health check failed with status {response.StatusCode}: {errorContent}";
                _logger.LogWarning("API health check failed: {Error}", errorMessage);
                return (false, errorMessage);
            }

            _logger.LogDebug("API health check passed");
            return (true, null);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Health check error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during health check");
            return (false, errorMessage);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient?.Dispose();
        _disposed = true;
        
        _logger.LogDebug("DatasiteApiService disposed");
    }
}

/// <summary>
/// HTTP content that reports upload progress
/// </summary>
internal sealed class ProgressStreamContent : HttpContent
{
    private readonly string _filePath;
    private readonly int _chunkSize;
    private readonly IProgress<UploadProgress>? _progress;
    private readonly UploadProgress _uploadProgress;
    private readonly CancellationToken _cancellationToken;

    public ProgressStreamContent(
        string filePath, 
        int chunkSize, 
        IProgress<UploadProgress>? progress, 
        UploadProgress uploadProgress,
        CancellationToken cancellationToken)
    {
        _filePath = filePath;
        _chunkSize = chunkSize;
        _progress = progress;
        _uploadProgress = uploadProgress;
        _cancellationToken = cancellationToken;
        
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        Headers.ContentLength = new FileInfo(_filePath).Length;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _chunkSize, useAsync: true);
        var buffer = new byte[_chunkSize];
        long totalBytesRead = 0;

        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken)) > 0)
        {
            await stream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
            totalBytesRead += bytesRead;

            _uploadProgress.BytesTransferred = totalBytesRead;
            _progress?.Report(_uploadProgress);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = new FileInfo(_filePath).Length;
        return true;
    }
}