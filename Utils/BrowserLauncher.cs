using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DatasiteUploader.Utils;

/// <summary>
/// Safe browser launcher with multiple fallback methods for enterprise environments
/// </summary>
public static class BrowserLauncher
{
    public static bool TryOpenUrl(string url, ILogger? logger = null)
    {
        try
        {
            // Method 1: Try Process.Start with UseShellExecute
            if (TryMethod1(url, logger)) return true;
            
            // Method 2: Try cmd start command
            if (TryMethod2(url, logger)) return true;
            
            // Method 3: Try Windows API directly
            if (TryMethod3(url, logger)) return true;
            
            // Method 4: Try explorer.exe
            if (TryMethod4(url, logger)) return true;
            
            logger?.LogWarning("All browser launch methods failed for URL: {Url}", url);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Exception during browser launch for URL: {Url}", url);
            return false;
        }
    }

    private static bool TryMethod1(string url, ILogger? logger)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
                Verb = "open"
            };
            
            using var process = Process.Start(processInfo);
            logger?.LogDebug("Browser launched successfully using Method 1 (direct URL)");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Method 1 failed: {Error}", ex.Message);
            return false;
        }
    }

    private static bool TryMethod2(string url, ILogger? logger)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start \"\" \"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(processInfo);
            process?.WaitForExit(5000); // Wait max 5 seconds
            
            logger?.LogDebug("Browser launched successfully using Method 2 (cmd start)");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Method 2 failed: {Error}", ex.Message);
            return false;
        }
    }

    private static bool TryMethod3(string url, ILogger? logger)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use Windows API to find default browser
                var result = ShellExecute(IntPtr.Zero, "open", url, null, null, 1);
                if (result.ToInt32() > 32)
                {
                    logger?.LogDebug("Browser launched successfully using Method 3 (ShellExecute)");
                    return true;
                }
            }
            
            logger?.LogDebug("Method 3 not applicable or failed");
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Method 3 failed: {Error}", ex.Message);
            return false;
        }
    }

    private static bool TryMethod4(string url, ILogger? logger)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = url,
                UseShellExecute = true
            };
            
            using var process = Process.Start(processInfo);
            logger?.LogDebug("Browser launched successfully using Method 4 (explorer)");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Method 4 failed: {Error}", ex.Message);
            return false;
        }
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string? lpParameters,
        string? lpDirectory,
        int nShowCmd);
}