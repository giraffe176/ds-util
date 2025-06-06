using Microsoft.Extensions.Logging;
using DatasiteUploader.Models;
using DatasiteUploader.Utils;
using System.Text;
using System.Diagnostics;
using System.Net;

namespace DatasiteUploader.Services;

/// <summary>
/// Production-ready console user interface with rich formatting and input validation
/// </summary>
public class ConsoleUserInterface : IUserInterface
{
    private readonly ILogger<ConsoleUserInterface> _logger;
    private readonly object _consoleLock = new();
    private UploadStatistics? _lastDisplayedStats;
    private DateTime _lastProgressUpdate = DateTime.MinValue;
    private const int ProgressUpdateIntervalMs = 500; // Update progress every 500ms

    public ConsoleUserInterface(ILogger<ConsoleUserInterface> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure console for better output
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (OperatingSystem.IsWindows())
            {
                Console.Title = "Datasite File Uploader";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure console settings");
        }
    }

    public void ShowBanner()
    {
        lock (_consoleLock)
        {
            Console.Clear();
            WriteColoredLine("╔" + new string('═', 58) + "╗", ConsoleColor.Cyan);
            WriteColoredLine("║" + "          DATASITE FILE UPLOADER v1.0.0".PadRight(58) + "║", ConsoleColor.Yellow);
            WriteColoredLine("║" + "     Enterprise-Grade File Upload Solution".PadRight(58) + "║", ConsoleColor.White);
            WriteColoredLine("╚" + new string('═', 58) + "╝", ConsoleColor.Cyan);
            Console.WriteLine();
        }
    }

    public async Task<Credentials?> GetCredentialsAsync()
    {
        try
        {
            DisplayInfo("🔐 OAuth2 Credentials Setup");
            DrawSeparator();

            // Check environment variables first
            var envClientId = Environment.GetEnvironmentVariable("DATASITE_CLIENT_ID");
            var envClientSecret = Environment.GetEnvironmentVariable("DATASITE_CLIENT_SECRET");
            var envRedirectUri = Environment.GetEnvironmentVariable("DATASITE_REDIRECT_URI");

            if (!string.IsNullOrEmpty(envClientId))
                DisplaySuccess($"✓ Client ID loaded from environment: {MaskSensitiveValue(envClientId)}");
            
            if (!string.IsNullOrEmpty(envClientSecret))
                DisplaySuccess("✓ Client Secret loaded from environment");
            
            if (!string.IsNullOrEmpty(envRedirectUri))
                DisplaySuccess($"✓ Redirect URI loaded from environment: {envRedirectUri}");

            // Get missing credentials
            var clientId = envClientId ?? PromptForInput("Client ID: ", true);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                DisplayError("Client ID is required");
                return null;
            }

            var clientSecret = envClientSecret ?? PromptForSecureInput("Client Secret: ");
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                DisplayError("Client Secret is required");
                return null;
            }

            var redirectUri = envRedirectUri ?? PromptForInputWithDefault(
                "Redirect URI", "http://localhost:8080/callback");

            // Start OAuth2 flow
            var authCode = await StartOAuth2Flow(clientId, redirectUri);
            if (string.IsNullOrWhiteSpace(authCode))
            {
                DisplayError("Authorization code is required");
                return null;
            }

            return new Credentials(clientId, clientSecret, redirectUri, authCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user credentials");
            DisplayError($"Error getting credentials: {ex.Message}");
            return null;
        }
    }

    private async Task<string> StartOAuth2Flow(string clientId, string redirectUri)
    {
        Console.WriteLine();
        DisplayInfo("🌐 OAuth2 Authorization Flow");
        DrawSeparator();

        var state = Guid.NewGuid().ToString();

        // Try to start local HTTP listener first
        if (redirectUri.StartsWith("http://localhost:"))
        {
            return await StartOAuth2FlowWithListener(clientId, redirectUri, state);
        }

        // Fallback to manual code entry
        return StartOAuth2FlowManual(clientId, redirectUri, state);
    }

    private async Task<string> StartOAuth2FlowWithListener(string clientId, string redirectUri, string state)
    {
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        var authUrl = $"https://token.datasite.com/oauth2/authorize?client_id={clientId}&redirect_uri={encodedRedirectUri}&state={state}";

        DisplayInfo("🚀 Automatic code capture mode");
        Console.WriteLine();

        // Extract port from redirect URI
        var uri = new Uri(redirectUri);
        var port = uri.Port;

        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        
        try
        {
            listener.Start();
            DisplaySuccess($"✓ Local server started on port {port}");
            
            DisplayInfo("Step 1: Opening Datasite login page...");
            var browserOpened = BrowserLauncher.TryOpenUrl(authUrl, _logger);
            
            if (browserOpened)
            {
                DisplaySuccess("✓ Browser opened successfully");
            }
            else
            {
                DisplayWarning("⚠ Could not open browser automatically");
                DisplayInfo("Please manually open this URL:");
                WriteColoredLine(authUrl, ConsoleColor.Cyan);
            }

            Console.WriteLine();
            DisplayInfo("Step 2: Complete login in your browser...");
            DisplayInfo("⏳ Waiting for authorization callback...");

            // Wait for the callback with timeout
            var contextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
            
            var completedTask = await Task.WhenAny(contextTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                DisplayWarning("⚠ Timeout waiting for authorization. Falling back to manual entry.");
                return StartOAuth2FlowManual(clientId, redirectUri, state);
            }

            var context = await contextTask;
            var request = context.Request;
            var response = context.Response;

            // Send a response to the browser
            var responseString = @"
<!DOCTYPE html>
<html>
<head><title>Datasite Uploader - Authorization Complete</title></head>
<body style='font-family: Arial; text-align: center; padding: 50px;'>
    <h1 style='color: green;'>✅ Authorization Successful!</h1>
    <p>You can now close this browser window and return to the Datasite Uploader.</p>
</body>
</html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            // Extract authorization code from query string
            var query = System.Web.HttpUtility.ParseQueryString(request.Url?.Query ?? "");
            var code = query["code"];
            var returnedState = query["state"];

            if (string.IsNullOrEmpty(code))
            {
                var error = query["error"];
                var errorDescription = query["error_description"];
                DisplayError($"❌ Authorization failed: {error} - {errorDescription}");
                return string.Empty;
            }

            if (returnedState != state)
            {
                DisplayError("❌ Security error: State parameter mismatch");
                return string.Empty;
            }

            DisplaySuccess("✅ Authorization code received automatically!");
            return code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start local HTTP listener");
            DisplayWarning($"⚠ Could not start local server: {ex.Message}");
            DisplayInfo("Falling back to manual code entry...");
            return StartOAuth2FlowManual(clientId, redirectUri, state);
        }
        finally
        {
            try { listener.Stop(); } catch { }
        }
    }

    private string StartOAuth2FlowManual(string clientId, string redirectUri, string state)
    {
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        var authUrl = $"https://token.datasite.com/oauth2/authorize?client_id={clientId}&redirect_uri={encodedRedirectUri}&state={state}";

        DisplayInfo("📋 Manual code entry mode");
        Console.WriteLine();

        DisplayInfo("Step 1: Opening Datasite login page...");
        
        try
        {
            var browserOpened = BrowserLauncher.TryOpenUrl(authUrl, _logger);
            
            if (browserOpened)
            {
                DisplaySuccess("✓ Browser opened successfully");
            }
            else
            {
                throw new InvalidOperationException("All browser launch methods failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open browser automatically");
            DisplayWarning("⚠ Could not open browser automatically");
            Console.WriteLine();
            DisplayInfo("Please manually open this URL in your browser:");
            WriteColoredLine(authUrl, ConsoleColor.Cyan);
        }

        Console.WriteLine();
        DisplayInfo("Step 2: Complete the following in your browser:");
        Console.WriteLine("  1. Log in with your Datasite credentials");
        Console.WriteLine("  2. Click 'Allow' or 'Authorize' when prompted");
        Console.WriteLine("  3. You'll be redirected to a page (may show an error)");
        Console.WriteLine("  4. Look at the URL in your browser's address bar");

        Console.WriteLine();
        DisplayInfo("Step 3: Copy the authorization code from the URL");
        Console.WriteLine($"The URL will look like:");
        WriteColoredLine($"{redirectUri}?code=XXXXXX&state={state}", ConsoleColor.Gray);
        Console.WriteLine("Copy everything after 'code=' and before '&state'");

        Console.WriteLine();
        return PromptForInput("Paste the authorization code here: ", true);
    }

    public Project? SelectProject(List<Project> projects)
    {
        if (!projects.Any())
        {
            DisplayError("No projects available");
            return null;
        }

        Console.WriteLine();
        DisplayInfo($"📁 Available Projects ({projects.Count} found)");
        DrawSeparator();

        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            var displayNumber = (i + 1).ToString().PadLeft(2);
            WriteColored($"  {displayNumber}. ", ConsoleColor.White);
            WriteColored(project.Name, ConsoleColor.Green);
            WriteColoredLine($" (ID: {project.Id}, State: {project.State})", ConsoleColor.Gray);
        }

        Console.WriteLine();
        var selection = PromptForIntegerInput($"Select project (1-{projects.Count}): ", 1, projects.Count);
        return selection.HasValue ? projects[selection.Value - 1] : null;
    }

    public Destination? SelectDestination(List<Destination> destinations)
    {
        if (!destinations.Any())
        {
            DisplayError("No destinations available");
            return null;
        }

        Console.WriteLine();
        DisplayInfo($"📂 Available Upload Destinations ({destinations.Count} found)");
        DrawSeparator();

        for (int i = 0; i < destinations.Count; i++)
        {
            var dest = destinations[i];
            var displayNumber = (i + 1).ToString().PadLeft(2);
            var typeColor = dest.Type == "Fileroom" ? ConsoleColor.Yellow : ConsoleColor.White;
            
            WriteColored($"  {displayNumber}. ", ConsoleColor.White);
            WriteColored($"[{dest.Type.ToUpper()}] ", typeColor);
            WriteColoredLine(dest.Path, ConsoleColor.Cyan);
        }

        Console.WriteLine();
        var selection = PromptForIntegerInput($"Select destination (1-{destinations.Count}): ", 1, destinations.Count);
        return selection.HasValue ? destinations[selection.Value - 1] : null;
    }

    public Destination? SelectFileroom(List<Destination> filerooms)
    {
        if (!filerooms.Any())
        {
            DisplayError("No filerooms available in this project");
            return null;
        }

        Console.WriteLine();
        DisplayInfo($"🏠 Select Fileroom ({filerooms.Count} available)");
        DrawSeparator();

        for (int i = 0; i < filerooms.Count; i++)
        {
            var fileroom = filerooms[i];
            var displayNumber = (i + 1).ToString().PadLeft(2);
            
            WriteColored($"  {displayNumber}. ", ConsoleColor.White);
            WriteColored("📁 ", ConsoleColor.Yellow);
            WriteColoredLine(fileroom.Name, ConsoleColor.Cyan);
        }

        Console.WriteLine();
        WriteColored("  0. ", ConsoleColor.Gray);
        WriteColoredLine("❌ Cancel", ConsoleColor.Red);
        Console.WriteLine();

        var selection = PromptForIntegerInput($"Select fileroom (0-{filerooms.Count}): ", 0, filerooms.Count);
        return selection.HasValue && selection.Value > 0 ? filerooms[selection.Value - 1] : null;
    }

    public (NavigationAction Action, Destination? Destination) NavigateFolder(Destination currentLocation, List<Destination> folders)
    {
        Console.WriteLine();
        DisplayInfo($"📂 Current Location: {currentLocation.Path}");
        DrawSeparator();

        // Show available actions
        var optionNumber = 1;
        
        WriteColored($"  {optionNumber++}. ", ConsoleColor.White);
        WriteColored("📤 ", ConsoleColor.Green);
        WriteColoredLine("Upload files here", ConsoleColor.Green);

        if (!string.IsNullOrEmpty(currentLocation.ParentId))
        {
            WriteColored($"  {optionNumber++}. ", ConsoleColor.White);
            WriteColored("⬅️  ", ConsoleColor.Yellow);
            WriteColoredLine("Go back", ConsoleColor.Yellow);
        }

        WriteColored($"  {optionNumber++}. ", ConsoleColor.White);
        WriteColored("➕ ", ConsoleColor.Cyan);
        WriteColoredLine("Create new folder", ConsoleColor.Cyan);

        // Show folders
        var folderStartIndex = optionNumber;
        if (folders.Any())
        {
            Console.WriteLine();
            WriteColoredLine("📁 Folders:", ConsoleColor.White);
            for (int i = 0; i < folders.Count; i++)
            {
                var folder = folders[i];
                var displayNumber = (optionNumber + i).ToString().PadLeft(2);
                
                WriteColored($"  {displayNumber}. ", ConsoleColor.White);
                WriteColored("📂 ", ConsoleColor.Blue);
                WriteColoredLine(folder.Name, ConsoleColor.Cyan);
            }
        }
        else
        {
            Console.WriteLine();
            WriteColoredLine("  (No folders in this location)", ConsoleColor.Gray);
        }

        Console.WriteLine();
        WriteColored("  0. ", ConsoleColor.Gray);
        WriteColoredLine("❌ Cancel", ConsoleColor.Red);
        Console.WriteLine();

        var maxOption = folderStartIndex + folders.Count - 1;
        var selection = PromptForIntegerInput($"Select option (0-{maxOption}): ", 0, maxOption);
        
        if (!selection.HasValue || selection.Value == 0)
        {
            return (NavigationAction.Cancel, null);
        }

        var choice = selection.Value;
        var backOptionIndex = string.IsNullOrEmpty(currentLocation.ParentId) ? -1 : 2;
        var createFolderIndex = string.IsNullOrEmpty(currentLocation.ParentId) ? 2 : 3;

        return choice switch
        {
            1 => (NavigationAction.UploadHere, currentLocation),
            var c when c == backOptionIndex => (NavigationAction.GoBack, null),
            var c when c == createFolderIndex => (NavigationAction.CreateFolder, currentLocation),
            var c when c >= folderStartIndex => (NavigationAction.EnterFolder, folders[c - folderStartIndex]),
            _ => (NavigationAction.Cancel, null)
        };
    }

    public string? GetUploadPath()
    {
        Console.WriteLine();
        DisplayInfo("📤 Upload Selection");
        DrawSeparator();
        
        Console.WriteLine("  1. Single file");
        Console.WriteLine("  2. Entire folder (including subfolders)");

        var choice = PromptForIntegerInput("Enter choice (1 or 2): ", 1, 2);
        if (!choice.HasValue) return null;

        Console.WriteLine();
        
        if (choice == 1)
        {
            return PromptForFilePath("Enter full path to file: ");
        }
        else
        {
            return PromptForFolderPath("Enter full path to folder: ");
        }
    }

    public void DisplayProgress(UploadStatistics statistics)
    {
        // Throttle progress updates to avoid excessive console output
        var now = DateTime.Now;
        if (now - _lastProgressUpdate < TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs) && 
            _lastDisplayedStats != null)
        {
            return;
        }

        lock (_consoleLock)
        {
            // Clear previous progress lines if this is an update
            if (_lastDisplayedStats != null)
            {
                ClearProgressLines(6); // Number of progress lines we display
            }

            var progressBar = CreateProgressBar(statistics.OverallProgress, 40);
            var eta = CalculateETA(statistics);
            
            WriteColored("📊 Upload Progress: ", ConsoleColor.Cyan);
            WriteColoredLine($"{statistics.OverallProgress:F1}%", ConsoleColor.Yellow);
            
            WriteColoredLine($"   {progressBar}", ConsoleColor.Green);
            
            WriteColored("📁 Files: ", ConsoleColor.White);
            WriteColored($"{statistics.CompletedFiles}", ConsoleColor.Green);
            WriteColored("/", ConsoleColor.Gray);
            WriteColored($"{statistics.TotalFiles}", ConsoleColor.White);
            if (statistics.FailedFiles > 0)
            {
                WriteColored($" (", ConsoleColor.Gray);
                WriteColored($"{statistics.FailedFiles} failed", ConsoleColor.Red);
                WriteColored(")", ConsoleColor.Gray);
            }
            Console.WriteLine();

            WriteColored("💾 Data: ", ConsoleColor.White);
            WriteColored(FormatBytes(statistics.TransferredBytes), ConsoleColor.Green);
            WriteColored("/", ConsoleColor.Gray);
            WriteColoredLine(FormatBytes(statistics.TotalBytes), ConsoleColor.White);

            WriteColored("⚡ Speed: ", ConsoleColor.White);
            WriteColored($"{statistics.TransferRateMBps:F1} MB/s", ConsoleColor.Yellow);
            if (!string.IsNullOrEmpty(eta))
            {
                WriteColored($" | ETA: {eta}", ConsoleColor.Gray);
            }
            Console.WriteLine();

            WriteColored("⏱️  Duration: ", ConsoleColor.White);
            WriteColoredLine($"{statistics.Duration:mm\\:ss}", ConsoleColor.Gray);
        }

        _lastDisplayedStats = statistics;
        _lastProgressUpdate = now;
    }

    public void DisplaySummary(UploadStatistics statistics, bool success)
    {
        Console.WriteLine();
        DrawSeparator('═');
        
        if (success && statistics.FailedFiles == 0)
        {
            DisplaySuccess("🎉 Upload Completed Successfully!");
        }
        else if (statistics.CompletedFiles > 0)
        {
            DisplayWarning($"⚠️  Upload Completed with Issues");
        }
        else
        {
            DisplayError("❌ Upload Failed");
        }

        DrawSeparator();

        // Summary statistics
        Console.WriteLine($"📊 Final Statistics:");
        Console.WriteLine($"   • Total Files: {statistics.TotalFiles:N0}");
        Console.WriteLine($"   • Completed: {statistics.CompletedFiles:N0}");
        if (statistics.FailedFiles > 0)
        {
            WriteColored($"   • Failed: {statistics.FailedFiles:N0}", ConsoleColor.Red);
            Console.WriteLine();
        }
        Console.WriteLine($"   • Total Size: {FormatBytes(statistics.TotalBytes)}");
        Console.WriteLine($"   • Transferred: {FormatBytes(statistics.TransferredBytes)}");
        Console.WriteLine($"   • Duration: {statistics.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"   • Average Speed: {statistics.TransferRateMBps:F1} MB/s");

        Console.WriteLine();
    }

    public void DisplayError(string message)
    {
        lock (_consoleLock)
        {
            WriteColored("❌ ", ConsoleColor.Red);
            WriteColoredLine(message, ConsoleColor.Red);
        }
    }

    public void DisplaySuccess(string message)
    {
        lock (_consoleLock)
        {
            WriteColored("✅ ", ConsoleColor.Green);
            WriteColoredLine(message, ConsoleColor.Green);
        }
    }

    public void DisplayInfo(string message)
    {
        lock (_consoleLock)
        {
            WriteColored("ℹ️  ", ConsoleColor.Blue);
            WriteColoredLine(message, ConsoleColor.Cyan);
        }
    }

    public void DisplayWarning(string message)
    {
        lock (_consoleLock)
        {
            WriteColored("⚠️  ", ConsoleColor.Yellow);
            WriteColoredLine(message, ConsoleColor.Yellow);
        }
    }

    public bool Confirm(string message)
    {
        WriteColored("❓ ", ConsoleColor.Yellow);
        WriteColored($"{message} ", ConsoleColor.White);
        WriteColored("(y/n): ", ConsoleColor.Gray);
        
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.KeyChar == 'y' || key.KeyChar == 'Y')
            {
                WriteColoredLine("Yes", ConsoleColor.Green);
                return true;
            }
            if (key.KeyChar == 'n' || key.KeyChar == 'N')
            {
                WriteColoredLine("No", ConsoleColor.Red);
                return false;
            }
        }
    }

    public void WaitForUser(string? message = null)
    {
        Console.WriteLine();
        WriteColored("⏸️  ", ConsoleColor.Gray);
        WriteColoredLine(message ?? "Press any key to continue...", ConsoleColor.Gray);
        Console.ReadKey(true);
    }

    public PostUploadAction GetPostUploadAction(Destination currentDestination)
    {
        Console.WriteLine();
        DisplayInfo("🎉 Upload completed successfully! What would you like to do next?");
        DrawSeparator();

        WriteColored("  1. ", ConsoleColor.White);
        WriteColored("📤 ", ConsoleColor.Green);
        WriteColoredLine($"Upload more files to the same location ({currentDestination.Path})", ConsoleColor.Green);

        WriteColored("  2. ", ConsoleColor.White);
        WriteColored("📂 ", ConsoleColor.Cyan);
        WriteColoredLine("Upload files to a different location", ConsoleColor.Cyan);

        WriteColored("  3. ", ConsoleColor.White);
        WriteColored("🚪 ", ConsoleColor.Yellow);
        WriteColoredLine("Exit application", ConsoleColor.Yellow);

        Console.WriteLine();
        var selection = PromptForIntegerInput("Enter your choice (1-3): ", 1, 3);
        
        return selection switch
        {
            1 => PostUploadAction.UploadMoreSameLocation,
            2 => PostUploadAction.UploadNewLocation,
            3 => PostUploadAction.Exit,
            _ => PostUploadAction.Exit // Default to exit if no valid selection
        };
    }

    protected void WriteColored(string text, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = originalColor;
    }

    protected void WriteColoredLine(string text, ConsoleColor color)
    {
        WriteColored(text, color);
        Console.WriteLine();
    }

    protected string PromptForInput(string prompt, bool required = false)
    {
        while (true)
        {
            WriteColored(prompt, ConsoleColor.White);
            var input = Console.ReadLine() ?? "";
            
            if (!required || !string.IsNullOrWhiteSpace(input))
                return input.Trim();
                
            DisplayError("This field is required");
        }
    }

    private string PromptForSecureInput(string prompt)
    {
        WriteColored(prompt, ConsoleColor.White);
        var password = new StringBuilder();
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.Key == ConsoleKey.Enter)
                break;
                
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }
        
        Console.WriteLine();
        return password.ToString();
    }

    private string PromptForInputWithDefault(string prompt, string defaultValue)
    {
        WriteColored($"{prompt} ", ConsoleColor.White);
        WriteColored($"[{defaultValue}]: ", ConsoleColor.Gray);
        
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    private int? PromptForIntegerInput(string prompt, int min, int max)
    {
        while (true)
        {
            var input = PromptForInput(prompt);
            
            if (string.IsNullOrWhiteSpace(input))
                return null;
                
            if (int.TryParse(input, out var value) && value >= min && value <= max)
                return value;
                
            DisplayError($"Please enter a number between {min} and {max}");
        }
    }

    private string? PromptForFilePath(string prompt)
    {
        while (true)
        {
            var path = PromptForInput(prompt);
            
            if (string.IsNullOrWhiteSpace(path))
                return null;
                
            if (File.Exists(path))
                return path;
                
            DisplayError("File not found. Please enter a valid file path.");
        }
    }

    private string? PromptForFolderPath(string prompt)
    {
        while (true)
        {
            var path = PromptForInput(prompt);
            
            if (string.IsNullOrWhiteSpace(path))
                return null;
                
            if (Directory.Exists(path))
                return path;
                
            DisplayError("Folder not found. Please enter a valid folder path.");
        }
    }

    protected void DrawSeparator(char character = '─', int length = 60)
    {
        WriteColoredLine(new string(character, length), ConsoleColor.Gray);
    }

    private string CreateProgressBar(double percentage, int width)
    {
        // Clamp percentage to 0-100 range to prevent negative values
        var clampedPercentage = Math.Max(0, Math.Min(100, percentage));
        var filled = (int)(clampedPercentage / 100.0 * width);
        var empty = Math.Max(0, width - filled);
        
        return $"[{new string('█', filled)}{new string('░', empty)}] {percentage:F1}%";
    }

    private string? CalculateETA(UploadStatistics statistics)
    {
        if (statistics.TransferRateMBps <= 0 || statistics.TotalBytes <= statistics.TransferredBytes)
            return null;
            
        var remainingBytes = statistics.TotalBytes - statistics.TransferredBytes;
        var remainingMB = remainingBytes / 1024.0 / 1024.0;
        var etaSeconds = remainingMB / statistics.TransferRateMBps;
        
        if (etaSeconds < 60)
            return $"{etaSeconds:F0}s";
        if (etaSeconds < 3600)
            return $"{etaSeconds / 60:F0}m {etaSeconds % 60:F0}s";
            
        return $"{etaSeconds / 3600:F0}h {(etaSeconds % 3600) / 60:F0}m";
    }

    private void ClearProgressLines(int lineCount)
    {
        for (int i = 0; i < lineCount; i++)
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, Console.CursorTop);
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

    private static string MaskSensitiveValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 8)
            return new string('*', value.Length);
            
        var start = value[..4];
        var end = value[^4..];
        var middle = new string('*', value.Length - 8);
        
        return $"{start}{middle}{end}";
    }
}