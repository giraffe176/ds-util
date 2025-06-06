# Datasite File Uploader

> **⚠️ IMPORTANT DISCLAIMER: This is an UNOFFICIAL, third-party tool. Datasite does NOT support, endorse, or maintain this software. Use at your own risk.**

> **Enterprise-Grade File Upload Solution for Datasite Projects**

A production-ready C# console application for uploading files and folder structures to Datasite projects with OAuth2 authentication, real-time progress tracking, and comprehensive error handling.

## ⚠️ **DISCLAIMER**

**THIS IS NOT AN OFFICIAL DATASITE PRODUCT**
- This tool is developed independently and is NOT supported by Datasite
- Datasite provides NO warranties, support, or guarantees for this software
- Use of this tool is at your own risk and responsibility
- For official Datasite solutions, contact Datasite directly

## 🌟 Features

### 🔒 **Enterprise Security**
- **OAuth2 Authentication** with authorization code flow and refresh token support
- **Secure credential management** with environment variable integration
- **File type validation** with configurable allow/block lists
- **File size limits** with configurable maximum upload sizes
- **Input validation** and sanitization throughout

### 🚀 **High Performance**
- **Concurrent uploads** with configurable limits (default: 5 simultaneous)
- **Retry policies** with exponential backoff for network resilience
- **Circuit breaker** patterns for API protection
- **Memory-efficient streaming** for large file uploads
- **Chunked uploads** with progress tracking (8MB chunks)

### 📊 **Rich User Experience**
- **Real-time progress bars** with transfer speed and ETA
- **Interactive project/destination selection** by index number
- **Comprehensive upload preview** before execution
- **Colored console output** with Unicode symbols
- **Professional error messages** and user guidance

### 🏗️ **Production Architecture**
- **Dependency injection** with Microsoft.Extensions
- **Structured logging** with configurable levels
- **Configuration management** via JSON and environment variables
- **Graceful shutdown** handling with Ctrl+C support
- **Health checks** and monitoring capabilities
- **Single-file deployment** ready

## 📋 Prerequisites

- **.NET 8.0 Runtime** (included in single-file build)
- **Windows 10/11** or **Windows Server 2019+**
- **Valid Datasite OAuth2 credentials** (contact your Datasite administrator)
- **Network access** to Datasite APIs (HTTPS outbound on port 443)

## 🚀 Quick Start

### Option 1: Download Pre-built Executable
1. Download `DatasiteUploader.exe` from releases
2. Double-click to run or execute from command line
3. Follow the interactive prompts

### Option 2: Build from Source
```bash
# Clone the repository
git clone <repository-url>
cd ds-test

# Restore dependencies and build
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Executable will be in: bin/Release/net8.0/win-x64/publish/DatasiteUploader.exe
```

## ⚙️ Configuration

### Environment Variables (Recommended)
Set these once for automatic credential loading:

```cmd
# Set system environment variables (requires administrator)
setx DATASITE_CLIENT_ID "your-client-id" /M
setx DATASITE_CLIENT_SECRET "your-client-secret" /M
setx DATASITE_REDIRECT_URI "http://localhost:8080/callback" /M
```

### Configuration File
Modify `appsettings.json` for advanced settings:

```json
{
  "Datasite": {
    "BaseUrl": "https://api.americas.datasite.com",
    "TokenUrl": "https://token.datasite.com/oauth2/token",
    "AuthBaseUrl": "https://token.datasite.com/oauth2/authorize", 
    "RefreshTokenUrl": "https://token.datasite.com/oauth2/refresh_token",
    "ApiVersion": "2024-04-01",
    "DefaultRedirectUri": "http://localhost:8080/callback",
    "MaxFileSize": 1073741824,
    "MaxRetryAttempts": 3,
    "RetryDelayMs": 1000,
    "RequestTimeoutMs": 300000,
    "ChunkSize": 8388608
  },
  "Environment": {
    "ValidateFileTypes": true,
    "AllowedExtensions": [
      ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
      ".txt", ".csv", ".zip", ".rar", ".7z", ".tar", ".gz",
      ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
      ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav"
    ],
    "BlockedExtensions": [".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".vbs", ".js"]
  }
}
```

## 📖 Usage Guide

### Interactive Mode (Default)
Simply run the executable and follow the guided prompts:

1. **Authentication**: Enter OAuth2 credentials or use saved environment variables
2. **Project Selection**: Choose from available projects by number
3. **Destination Selection**: Browse filerooms and folders, select by index
4. **Upload Selection**: Choose single file or entire folder structure
5. **Preview & Confirm**: Review upload details before proceeding
6. **Monitor Progress**: Watch real-time progress with transfer statistics

### Command Line Options
```cmd
# Basic usage
DatasiteUploader.exe

# With custom configuration file
DatasiteUploader.exe --environment Production

# With logging level override  
DatasiteUploader.exe --Logging:LogLevel:Default=Debug

# Display help information
DatasiteUploader.exe --help
```

### OAuth2 Setup Process

#### Getting OAuth2 Credentials
Contact your Datasite administrator to obtain:
- **Client ID**: Your application identifier
- **Client Secret**: Your application secret key
- **Redirect URI**: Callback URL (typically `http://localhost:8080/callback`)

#### First-Time Authentication Flow
1. Application opens browser to Datasite login page
2. User logs in with Datasite credentials
3. User authorizes the application
4. Browser redirects with authorization code
5. User copies code from URL and pastes into application
6. Application exchanges code for access token
7. Refresh token saved for future sessions

## 📁 Upload Scenarios

### Single File Upload
- Select any file type (subject to validation rules)
- Real-time progress with transfer speed
- Automatic retry on network issues
- File integrity verification

### Folder Structure Upload
```
Local Folder:
📁 Documents/
  📄 contract.pdf
  📄 summary.docx
  📁 Financials/
    📄 budget.xlsx
    📄 forecast.pdf
    📁 Archive/
      📄 old-data.csv
```

Becomes:
```
Datasite Project:
📁 [Selected Destination]/
  📁 Documents/
    📄 contract.pdf
    📄 summary.docx
    📁 Financials/
      📄 budget.xlsx
      📄 forecast.pdf
      📁 Archive/
        📄 old-data.csv
```

## 🔧 Advanced Features

### File Type Validation
```json
{
  "Environment": {
    "ValidateFileTypes": true,
    "AllowedExtensions": [
      ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
      ".txt", ".csv", ".zip", ".rar", ".7z", ".tar", ".gz",
      ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
      ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav"
    ],
    "BlockedExtensions": [
      ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".vbs", ".js"
    ]
  }
}
```

### Concurrent Upload Management
- **Automatic throttling**: Maximum 5 concurrent uploads
- **Memory management**: Efficient handling of large files
- **Error isolation**: Failed uploads don't affect others
- **Progress aggregation**: Combined statistics across all uploads

### Network Resilience
- **Retry policies**: 3 attempts with exponential backoff
- **Circuit breaker**: Prevents cascading failures
- **Timeout handling**: Configurable request timeouts
- **Connection pooling**: Efficient HTTP client management

## 📊 Monitoring & Logging

### Real-Time Statistics
- **Transfer speed**: MB/s with moving average
- **Progress tracking**: Files completed/total with percentage
- **ETA calculation**: Estimated time to completion
- **Error tracking**: Failed uploads with detailed messages

### Logging Levels
- **Critical**: Application-breaking errors
- **Error**: Upload failures and API errors
- **Warning**: Retries and degraded performance
- **Information**: Normal operation flow
- **Debug**: Detailed technical information

### Exit Codes
```
0  - Success
1  - Authentication Failed
2  - Health Check Failed
3  - Validation Failed
4  - Upload Failed
5  - User Cancelled
-1 - Critical Error
```

## 🛠️ Troubleshooting

### Common Issues

#### Authentication Problems
```
❌ Authentication failed: invalid_grant
```
**Solution**: Authorization code expired. Restart OAuth2 flow.

#### Network Connectivity
```
❌ Upload failed: A task was canceled
```
**Solution**: Check network connection and firewall settings.

#### File Access Issues
```
❌ File not found: C:\path\to\file.pdf
```
**Solution**: Verify file path and read permissions.

#### Large File Uploads
```
⚠️ Large data transfer (2.1 GB)
```
**Solution**: Ensure stable network connection for duration of upload.

### Performance Optimization

#### For Large Uploads
- Close unnecessary applications to free memory
- Use wired network connection when possible
- Monitor available disk space
- Consider uploading during off-peak hours

#### For Many Small Files
- Group files into folders for better organization
- Use folder upload rather than individual file uploads
- Monitor concurrent upload limits

## 🔐 Security Considerations

### Credential Security
- **Environment variables**: Stored at system level
- **In-memory only**: No credentials saved to disk during execution
- **Secure input**: Password masking for sensitive data
- **Token refresh**: Automatic refresh without re-authentication

### Network Security
- **HTTPS only**: All API communication encrypted
- **Certificate validation**: Full SSL/TLS verification
- **No credential logging**: Sensitive data excluded from logs
- **Secure defaults**: Conservative timeout and retry settings

### File Security
- **Type validation**: Prevent execution of dangerous files
- **Size limits**: Prevent resource exhaustion
- **Path validation**: Prevent directory traversal attacks
- **Virus scanning**: Recommend integration with enterprise AV

## 📚 API Documentation

### Supported Datasite APIs
- **Authentication**: OAuth2 token exchange and refresh
- **Projects**: List accessible projects
- **Filerooms**: Browse project filerooms
- **Metadata**: Navigate folder structures
- **Upload**: Multipart file uploads with progress

### Rate Limiting
- **Automatic throttling**: Respects API rate limits
- **Circuit breaker**: Prevents overwhelming servers
- **Retry logic**: Handles temporary service unavailability
- **Health checks**: Validates API availability

## 🏢 Enterprise Deployment

### System Requirements
- **Windows 10 1809+** or **Windows Server 2019+**
- **2 GB RAM** minimum (4 GB recommended for large uploads)
- **100 MB disk space** for application
- **Network connectivity** to datasite.com domains (HTTPS/443)

### Deployment Options
1. **Single executable**: Self-contained with no dependencies
2. **MSI installer**: For enterprise software deployment
3. **Group Policy**: Deploy via Active Directory
4. **SCCM/Intune**: Microsoft endpoint management

### Enterprise Configuration
```json
{
  "Datasite": {
    "BaseUrl": "https://api.americas.datasite.com",
    "RequestTimeoutMs": 600000,
    "MaxRetryAttempts": 5
  },
  "Environment": {
    "SaveCredentials": false,
    "ValidateFileTypes": true,
    "AllowedExtensions": [
      ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
      ".txt", ".csv", ".zip", ".rar", ".7z", ".tar", ".gz",
      ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
      ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## 📞 Support & Troubleshooting

### Getting Help
**⚠️ IMPORTANT: Datasite does NOT provide support for this unofficial tool**

1. **Check this documentation** for common solutions
2. **Review application logs** for specific error details  
3. **Verify prerequisites** (network, credentials, permissions)
4. **Contact your Datasite administrator** ONLY for official API access issues (NOT for this tool)

### OAuth2 Credential Issues
- **Client ID/Secret**: Contact your Datasite administrator
- **Redirect URI**: Must exactly match the registered callback URL
- **Authorization Code**: Expires within minutes - regenerate if needed
- **Token Refresh**: Clear saved tokens and re-authenticate if issues persist

### Performance Issues
- **Large files**: Ensure stable network connection and sufficient disk space
- **Many files**: Consider grouping into folders for better organization
- **Slow uploads**: Check network bandwidth and close other applications
- **Memory usage**: Monitor system resources during large operations

## 🔄 Version History

### v1.0.0 - Initial Release
- ✅ OAuth2 authentication with refresh token support
- ✅ Interactive project and destination selection
- ✅ Single file and folder structure uploads
- ✅ Real-time progress tracking with ETA
- ✅ Comprehensive error handling and retry logic
- ✅ Enterprise-grade logging and configuration
- ✅ Single-file deployment ready

## 📄 License & Legal

**⚠️ UNOFFICIAL THIRD-PARTY SOFTWARE**

This is an independent, unofficial tool that is NOT affiliated with, endorsed by, or supported by Datasite. 

- **NO WARRANTY**: This software is provided "AS IS" without any warranties
- **NO SUPPORT**: Datasite provides no support, maintenance, or guarantees  
- **USE AT YOUR OWN RISK**: Users assume all responsibility for any issues
- **NOT OFFICIAL**: This is not an official Datasite product or solution

---

**🚀 Ready to upload? Download the latest release and get started in minutes!**