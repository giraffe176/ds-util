using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DatasiteUploader.Models;
using DatasiteUploader.Services;
using System.Reflection;

namespace DatasiteUploader;

/// <summary>
/// Production-ready Datasite File Uploader with enterprise-grade architecture
/// </summary>
internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Immediate console output for debugging
        Console.WriteLine("Starting Datasite Uploader...");
        Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"Executable Location: {AppContext.BaseDirectory}");
        
        // Set up global exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Console.WriteLine("Creating host builder...");
            var hostBuilder = CreateHostBuilder(args);
            
            Console.WriteLine("Building host...");
            var host = hostBuilder.Build();
            
            Console.WriteLine("Getting application service...");
            // Run the application
            var app = host.Services.GetRequiredService<DatasiteUploaderApplication>();
            
            Console.WriteLine("Starting application...");
            var exitCode = await app.RunAsync();
            
            return exitCode;
        }
        catch (Exception ex)
        {
            // Write error directly to console for debugging
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"💥 Critical Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.ResetColor();
            
            // Log critical startup errors
            try
            {
                var logger = CreateFallbackLogger();
                logger.LogCritical(ex, "Critical error during application startup");
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"Additional error creating logger: {logEx.Message}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            
            return -1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        Console.WriteLine("Setting up default host builder...");
        
        try
        {
            var builder = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    Console.WriteLine("Configuring app configuration...");
                    Console.WriteLine($"Base path: {Directory.GetCurrentDirectory()}");
                    // Load configuration from multiple sources
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddJsonFile("config/appsettings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables("DATASITE_")
                          .AddCommandLine(args);
                    Console.WriteLine("App configuration complete.");
                })
                .ConfigureLogging((context, logging) =>
                {
                    Console.WriteLine("Configuring logging...");
                    logging.AddConsole()
                           .SetMinimumLevel(LogLevel.Warning); // Only show warnings and errors
                    Console.WriteLine("Logging configuration complete.");
                })
                .ConfigureServices((context, services) =>
                {
                    Console.WriteLine("Configuring services...");
                    // Register configuration sections
                    services.Configure<DatasiteConfig>(context.Configuration.GetSection("Datasite"));
                    services.Configure<EnvironmentConfig>(context.Configuration.GetSection("Environment"));
                    
                    Console.WriteLine("Adding HTTP client...");
                    // Register HTTP client
                    services.AddHttpClient<IDatasiteApiService, DatasiteApiService>(client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", 
                            $"DatasiteUploader/{Assembly.GetExecutingAssembly().GetName().Version}");
                    });
                    
                    Console.WriteLine("Registering application services...");
                    // Register application services
                    services.AddSingleton<IDatasiteApiService, DatasiteApiService>();
                    services.AddSingleton<IUploadService, UploadService>();
                    services.AddSingleton<IDestinationService, DestinationService>();
                    services.AddSingleton<IUserInterface, ConsoleUserInterface>();
                    services.AddSingleton<DatasiteUploaderApplication>();
                    
                    Console.WriteLine("Services configuration complete.");
                });
                
            Console.WriteLine("Host builder created successfully.");
            return builder;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating host builder: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }


    private static ILogger CreateFallbackLogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Error));
        return loggerFactory.CreateLogger<Program>();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = CreateFallbackLogger();
        logger.LogCritical((Exception)e.ExceptionObject, "Unhandled exception occurred");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var logger = CreateFallbackLogger();
        logger.LogCritical(e.Exception, "Unobserved task exception occurred");
        e.SetObserved();
    }
}

/// <summary>
/// Main application orchestrator with comprehensive error handling and user experience
/// </summary>
public sealed class DatasiteUploaderApplication
{
    private readonly ILogger<DatasiteUploaderApplication> _logger;
    private readonly IDatasiteApiService _apiService;
    private readonly IUploadService _uploadService;
    private readonly IDestinationService _destinationService;
    private readonly IUserInterface _ui;
    private readonly CancellationTokenSource _appCancellation;

    public DatasiteUploaderApplication(
        ILogger<DatasiteUploaderApplication> logger,
        IDatasiteApiService apiService,
        IUploadService uploadService,
        IDestinationService destinationService,
        IUserInterface ui)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        _destinationService = destinationService ?? throw new ArgumentNullException(nameof(destinationService));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _appCancellation = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _logger.LogInformation("Cancellation requested by user");
            _ui.DisplayWarning("Cancellation requested. Finishing current operations...");
            _appCancellation.Cancel();
        };
    }

    public async Task<int> RunAsync()
    {
        try
        {
            _logger.LogInformation("Starting Datasite File Uploader v{Version}", 
                Assembly.GetExecutingAssembly().GetName().Version);

            _ui.ShowBanner();

            // Skip health check for now since it requires authentication
            // TODO: Implement health check after authentication if needed

            // Authentication phase
            var authResult = await AuthenticateUserAsync();
            if (!authResult.Success)
            {
                return ExitCodes.AuthenticationFailed;
            }

            _ui.DisplaySuccess($"✅ Welcome {authResult.UserInfo!.DisplayName} from {authResult.UserInfo.Organization}");

            // Project selection phase
            var project = await SelectProjectAsync();
            if (project == null)
            {
                return ExitCodes.UserCancelled;
            }

            _ui.DisplaySuccess($"📁 Selected project: {project.Name}");

            // Destination selection phase
            var destination = await SelectDestinationAsync(project);
            if (destination == null)
            {
                return ExitCodes.UserCancelled;
            }

            _ui.DisplaySuccess($"📂 Selected destination: {destination.Path}");

            // Upload path selection phase
            var uploadPath = _ui.GetUploadPath();
            if (string.IsNullOrEmpty(uploadPath))
            {
                _ui.DisplayWarning("No upload path selected");
                return ExitCodes.UserCancelled;
            }

            // Validation phase
            var validation = _uploadService.ValidateUploadPath(uploadPath);
            if (!validation.IsValid)
            {
                _ui.DisplayError($"Upload validation failed: {validation.ErrorMessage}");
                return ExitCodes.ValidationFailed;
            }

            // Upload preview and confirmation
            if (!await ShowUploadPreviewAndConfirm(uploadPath, destination))
            {
                return ExitCodes.UserCancelled;
            }

            // Upload execution phase
            var uploadResult = await PerformUploadAsync(uploadPath, destination);
            
            if (!uploadResult)
            {
                return ExitCodes.UploadFailed;
            }

            // Post-upload action loop
            var currentDestination = destination;
            while (true)
            {
                var postAction = _ui.GetPostUploadAction(currentDestination);
                
                switch (postAction)
                {
                    case PostUploadAction.UploadMoreSameLocation:
                        if (!await HandleUploadMoreSameLocationAsync(currentDestination))
                        {
                            return ExitCodes.UploadFailed;
                        }
                        break;
                        
                    case PostUploadAction.UploadNewLocation:
                        var newDestination = await HandleUploadNewLocationAsync(project);
                        if (newDestination != null)
                        {
                            currentDestination = newDestination; // Update for next iteration
                        }
                        // If newDestination is null, just continue with the current destination
                        break;
                        
                    case PostUploadAction.Exit:
                        _ui.DisplaySuccess("Thank you for using Datasite Uploader!");
                        return ExitCodes.Success;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Application cancelled by user");
            _ui.DisplayWarning("Operation cancelled by user");
            return ExitCodes.UserCancelled;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in main application flow");
            _ui.DisplayError($"💥 Critical error: {ex.Message}");
            return ExitCodes.CriticalError;
        }
        finally
        {
            _appCancellation.Dispose();
        }
    }

    private async Task<bool> PerformHealthCheckAsync()
    {
        try
        {
            _ui.DisplayInfo("🔍 Performing system health check...");
            
            var (isHealthy, errorMessage) = await _apiService.CheckHealthAsync(_appCancellation.Token);
            
            if (isHealthy)
            {
                _ui.DisplaySuccess("✅ System health check passed");
                return true;
            }
            else
            {
                _ui.DisplayError($"❌ System health check failed: {errorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            _ui.DisplayWarning("⚠️ Health check inconclusive, proceeding anyway");
            return true; // Don't block startup for health check failures
        }
    }

    private async Task<(bool Success, UserInfo? UserInfo)> AuthenticateUserAsync()
    {
        try
        {
            // Try refresh token first
            var refreshToken = Environment.GetEnvironmentVariable("DATASITE_REFRESH_TOKEN");
            var clientId = Environment.GetEnvironmentVariable("DATASITE_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("DATASITE_CLIENT_SECRET");

            if (!string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                _ui.DisplayInfo("🔄 Attempting to refresh existing session...");
                
                var refreshResult = await _apiService.RefreshTokenAsync(
                    refreshToken, clientId, clientSecret, _appCancellation.Token);

                if (refreshResult.Success)
                {
                    _apiService.SetAccessToken(refreshResult.AccessToken!);
                    _ui.DisplaySuccess("✅ Session refreshed successfully");
                    
                    // We don't get user info from refresh, but we can still proceed
                    return (true, new UserInfo { Email = "Authenticated User" });
                }
                else
                {
                    _ui.DisplayWarning($"⚠️ Session refresh failed: {refreshResult.ErrorMessage}");
                }
            }

            // Full OAuth2 flow
            _ui.DisplayInfo("🔐 Starting OAuth2 authentication...");
            
            var credentials = await _ui.GetCredentialsAsync();
            if (credentials == null)
            {
                _ui.DisplayError("Authentication cancelled");
                return (false, null);
            }

            var authResult = await _apiService.AuthenticateAsync(credentials, _appCancellation.Token);
            
            if (authResult.Success)
            {
                _apiService.SetAccessToken(authResult.AccessToken!);
                _logger.LogInformation("Successfully authenticated user: {Email}", authResult.UserInfo!.Email);
                return (true, authResult.UserInfo);
            }
            else
            {
                _ui.DisplayError($"❌ Authentication failed: {authResult.ErrorMessage}");
                return (false, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication process failed");
            _ui.DisplayError($"Authentication error: {ex.Message}");
            return (false, null);
        }
    }

    private async Task<Project?> SelectProjectAsync()
    {
        try
        {
            _ui.DisplayInfo("📁 Fetching your projects...");
            
            var (success, projects, errorMessage) = await _apiService.GetProjectsAsync(_appCancellation.Token);
            
            if (!success)
            {
                _ui.DisplayError($"Failed to fetch projects: {errorMessage}");
                return null;
            }

            if (!projects.Any())
            {
                _ui.DisplayError("No projects found. Please contact your administrator.");
                return null;
            }

            return _ui.SelectProject(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Project selection failed");
            _ui.DisplayError($"Project selection error: {ex.Message}");
            return null;
        }
    }

    private async Task<Destination?> SelectDestinationAsync(Project project)
    {
        try
        {
            _ui.DisplayInfo($"🏠 Loading filerooms for {project.Name}...");
            
            // Get filerooms first (fast initialization)
            var (fileroomsSuccess, filerooms, fileroomsError) = await _destinationService.GetFileroomsAsync(
                project, _appCancellation.Token);
            
            if (!fileroomsSuccess)
            {
                _ui.DisplayError($"Failed to fetch filerooms: {fileroomsError}");
                return null;
            }

            if (!filerooms.Any())
            {
                _ui.DisplayError("No filerooms found in this project.");
                return null;
            }

            // User selects a fileroom
            var selectedFileroom = _ui.SelectFileroom(filerooms);
            if (selectedFileroom == null)
            {
                return null;
            }

            _ui.DisplaySuccess($"📁 Entered fileroom: {selectedFileroom.Name}");

            // Navigate hierarchically through folders
            return await NavigateFoldersAsync(selectedFileroom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Destination selection failed");
            _ui.DisplayError($"Destination selection error: {ex.Message}");
            return null;
        }
    }

    private async Task<Destination?> NavigateFoldersAsync(Destination currentLocation)
    {
        var navigationStack = new Stack<Destination>();
        var current = currentLocation;

        while (true)
        {
            try
            {
                // Get folders in current location
                _ui.DisplayInfo($"📂 Loading folders in {current.Name}...");
                var (foldersSuccess, folders, foldersError) = await _destinationService.GetFoldersAsync(
                    current, _appCancellation.Token);

                if (!foldersSuccess)
                {
                    _ui.DisplayError($"Failed to load folders: {foldersError}");
                    return null;
                }

                // Show navigation options
                var (action, destination) = _ui.NavigateFolder(current, folders);

                switch (action)
                {
                    case NavigationAction.UploadHere:
                        return destination;

                    case NavigationAction.EnterFolder:
                        if (destination != null)
                        {
                            navigationStack.Push(current);
                            current = destination;
                            _ui.DisplaySuccess($"📂 Entered folder: {destination.Name}");
                        }
                        break;

                    case NavigationAction.GoBack:
                        if (navigationStack.Any())
                        {
                            current = navigationStack.Pop();
                            _ui.DisplayInfo($"⬅️ Returned to: {current.Name}");
                        }
                        else
                        {
                            _ui.DisplayInfo("⬅️ Returned to fileroom selection");
                            return await SelectDestinationAsync(await GetCurrentProject());
                        }
                        break;

                    case NavigationAction.CreateFolder:
                        var newFolder = await CreateFolderInteractiveAsync(current);
                        if (newFolder != null)
                        {
                            _ui.DisplaySuccess($"✅ Created folder: {newFolder.Name}");
                        }
                        break;

                    case NavigationAction.Cancel:
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation error");
                _ui.DisplayError($"Navigation error: {ex.Message}");
                return null;
            }
        }
    }

    private async Task<Destination?> CreateFolderInteractiveAsync(Destination parentDestination)
    {
        try
        {
            Console.WriteLine();
            _ui.DisplayInfo("➕ Create New Folder");
            var folderName = PromptForInput("Enter folder name: ");
            
            if (string.IsNullOrWhiteSpace(folderName))
            {
                _ui.DisplayWarning("Folder name cannot be empty");
                return null;
            }

            _ui.DisplayInfo($"Creating folder '{folderName}'...");
            var (success, newFolder, errorMessage) = await _destinationService.CreateDestinationAsync(
                parentDestination, folderName, _appCancellation.Token);

            if (!success)
            {
                _ui.DisplayError($"Failed to create folder: {errorMessage}");
                return null;
            }

            return newFolder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder creation failed");
            _ui.DisplayError($"Folder creation error: {ex.Message}");
            return null;
        }
    }

    private async Task<Project> GetCurrentProject()
    {
        // This would ideally store the current project in a field, 
        // but for now we'll just return the first available project
        var (success, projects, _) = await _apiService.GetProjectsAsync(_appCancellation.Token);
        return success && projects.Any() ? projects.First() : throw new InvalidOperationException("No projects available");
    }

    private static string PromptForInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private async Task<bool> ShowUploadPreviewAndConfirm(string uploadPath, Destination destination)
    {
        try
        {
            _ui.DisplayInfo("📊 Analyzing upload...");
            
            var preview = await _uploadService.GetUploadPreviewAsync(uploadPath);
            
            Console.WriteLine();
            _ui.DisplayInfo("📋 Upload Summary:");
            Console.WriteLine($"   • Source: {uploadPath}");
            Console.WriteLine($"   • Destination: {destination.Path}");
            Console.WriteLine($"   • Files: {preview.TotalFiles:N0}");
            Console.WriteLine($"   • Total Size: {FormatBytes(preview.TotalBytes)}");
            
            if (preview.TotalFiles > 100)
            {
                _ui.DisplayWarning($"⚠️ Large upload detected ({preview.TotalFiles:N0} files)");
            }
            
            if (preview.TotalBytes > 1_000_000_000) // 1GB
            {
                _ui.DisplayWarning($"⚠️ Large data transfer ({FormatBytes(preview.TotalBytes)})");
            }

            Console.WriteLine();
            return _ui.Confirm("Proceed with upload?");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload preview failed");
            _ui.DisplayError($"Preview error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PerformUploadAsync(string uploadPath, Destination destination)
    {
        var startTime = DateTime.Now;
        var progressReported = false;

        try
        {
            _ui.DisplayInfo("🚀 Starting upload...");
            Console.WriteLine();

            var progress = new Progress<UploadStatistics>(stats =>
            {
                _ui.DisplayProgress(stats);
                progressReported = true;
            });

            (bool success, string? errorMessage) uploadResult;

            if (Directory.Exists(uploadPath))
            {
                _logger.LogInformation("Starting folder upload: {FolderPath}", uploadPath);
                uploadResult = await _uploadService.UploadFolderAsync(
                    uploadPath, destination, progress, _appCancellation.Token);
            }
            else
            {
                _logger.LogInformation("Starting file upload: {FilePath}", uploadPath);
                uploadResult = await _uploadService.UploadFileAsync(
                    uploadPath, destination, progress, _appCancellation.Token);
            }

            var duration = DateTime.Now - startTime;
            
            // Final progress display
            if (progressReported)
            {
                Console.WriteLine();
            }

            // Display comprehensive results summary
            await DisplayUploadSummaryAsync(uploadPath, destination, uploadResult.success, uploadResult.errorMessage, duration);

            return uploadResult.success;
        }
        catch (OperationCanceledException)
        {
            _ui.DisplayWarning("Upload cancelled by user");
            _uploadService.CancelAllUploads();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload execution failed");
            _ui.DisplayError($"Upload error: {ex.Message}");
            return false;
        }
    }

    private async Task DisplayUploadSummaryAsync(string uploadPath, Destination destination, bool success, string? errorMessage, TimeSpan duration)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("═".PadRight(80, '═'));
            
            if (success)
            {
                _ui.DisplaySuccess("🎉 UPLOAD COMPLETED SUCCESSFULLY!");
            }
            else
            {
                _ui.DisplayError("❌ UPLOAD FAILED");
            }
            
            Console.WriteLine("═".PadRight(80, '═'));
            Console.WriteLine();

            // Get final statistics
            var preview = await _uploadService.GetUploadPreviewAsync(uploadPath);
            var isFolder = Directory.Exists(uploadPath);
            
            Console.WriteLine("📊 UPLOAD SUMMARY:");
            Console.WriteLine($"   • Source: {(isFolder ? "📁" : "📄")} {uploadPath}");
            Console.WriteLine($"   • Destination: 📂 {destination.Path}");
            Console.WriteLine($"   • Type: {(isFolder ? "Folder with subfolders" : "Single file")}");
            Console.WriteLine($"   • Files: {preview.TotalFiles:N0}");
            Console.WriteLine($"   • Total Size: {FormatBytes(preview.TotalBytes)}");
            Console.WriteLine($"   • Duration: {duration:hh\\:mm\\:ss}");
            
            if (success)
            {
                var avgSpeed = duration.TotalSeconds > 0 ? preview.TotalBytes / 1024.0 / 1024.0 / duration.TotalSeconds : 0;
                Console.WriteLine($"   • Average Speed: {avgSpeed:F1} MB/s");
                Console.WriteLine();
                _ui.DisplaySuccess("✅ All files uploaded successfully!");
                
                if (isFolder)
                {
                    Console.WriteLine();
                    _ui.DisplayInfo("📝 NOTE: Folder structure preserved with all subfolders and files");
                }
            }
            else
            {
                Console.WriteLine();
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    _ui.DisplayError($"💥 Error Details: {errorMessage}");
                }
                else
                {
                    _ui.DisplayError("💥 Upload failed with unknown error");
                }
                
                Console.WriteLine();
                _ui.DisplayWarning("⚠️  Some or all files may not have been uploaded");
                _ui.DisplayInfo("💡 Check the log output above for specific error details");
            }

            Console.WriteLine();
            Console.WriteLine("═".PadRight(80, '═'));
            _logger.LogInformation("Upload summary displayed - Success: {Success}, Duration: {Duration}", success, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying upload summary");
            _ui.DisplayError($"Error creating summary: {ex.Message}");
        }
    }

    private async Task<bool> HandleUploadMoreSameLocationAsync(Destination destination)
    {
        try
        {
            _ui.DisplayInfo($"📤 Uploading more files to: {destination.Path}");
            
            // Get new upload path
            var uploadPath = _ui.GetUploadPath();
            if (string.IsNullOrEmpty(uploadPath))
            {
                _ui.DisplayWarning("No upload path selected");
                return true; // Return to post-upload menu
            }

            // Validation phase
            var validation = _uploadService.ValidateUploadPath(uploadPath);
            if (!validation.IsValid)
            {
                _ui.DisplayError($"Upload validation failed: {validation.ErrorMessage}");
                return true; // Return to post-upload menu
            }

            // Upload preview and confirmation
            if (!await ShowUploadPreviewAndConfirm(uploadPath, destination))
            {
                return true; // Return to post-upload menu
            }

            // Upload execution
            return await PerformUploadAsync(uploadPath, destination);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling upload to same location");
            _ui.DisplayError($"Error: {ex.Message}");
            return true; // Return to post-upload menu
        }
    }

    private async Task<Destination?> HandleUploadNewLocationAsync(Project project)
    {
        try
        {
            _ui.DisplayInfo("📂 Selecting new upload destination...");
            
            // Destination selection phase
            var destination = await SelectDestinationAsync(project);
            if (destination == null)
            {
                _ui.DisplayWarning("No destination selected");
                return null; // Return to post-upload menu without destination change
            }

            _ui.DisplaySuccess($"📂 Selected destination: {destination.Path}");

            // Get upload path
            var uploadPath = _ui.GetUploadPath();
            if (string.IsNullOrEmpty(uploadPath))
            {
                _ui.DisplayWarning("No upload path selected");
                return null; // Return to post-upload menu without destination change
            }

            // Validation phase
            var validation = _uploadService.ValidateUploadPath(uploadPath);
            if (!validation.IsValid)
            {
                _ui.DisplayError($"Upload validation failed: {validation.ErrorMessage}");
                return null; // Return to post-upload menu without destination change
            }

            // Upload preview and confirmation
            if (!await ShowUploadPreviewAndConfirm(uploadPath, destination))
            {
                return null; // Return to post-upload menu without destination change
            }

            // Upload execution
            var uploadResult = await PerformUploadAsync(uploadPath, destination);
            return uploadResult ? destination : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling upload to new location");
            _ui.DisplayError($"Error: {ex.Message}");
            return null; // Return to post-upload menu without destination change
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }
}

/// <summary>
/// Hosted service for graceful application shutdown
/// </summary>
public sealed class GracefulShutdownService : BackgroundService
{
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public GracefulShutdownService(
        ILogger<GracefulShutdownService> logger, 
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(-1, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown initiated");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Application exit codes for proper error handling
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int CriticalError = -1;
    public const int AuthenticationFailed = 1;
    public const int HealthCheckFailed = 2;
    public const int ValidationFailed = 3;
    public const int UploadFailed = 4;
    public const int UserCancelled = 5;
}