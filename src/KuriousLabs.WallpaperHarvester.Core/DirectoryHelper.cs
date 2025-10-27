using Microsoft.Extensions.Logging;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed class DirectoryHelper
{
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public static string GetDefaultWallpaperDirectory()
    {
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        // Fallback if Pictures folder doesn't exist
        if (string.IsNullOrEmpty(picturesPath))
        {
            var homeFolder = OperatingSystem.IsWindows()
                ? Environment.SpecialFolder.UserProfile
                : Environment.SpecialFolder.Personal;
            picturesPath = Path.Combine(Environment.GetFolderPath(homeFolder), "Pictures");
        }

        return Path.Combine(picturesPath, "Wallpapers");
    }

    public static void ValidateDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        // Check path length based on OS
        var maxPathLength = OperatingSystem.IsWindows() ? 260 : 4096;
        if (path.Length > maxPathLength)
        {
            throw new ArgumentException($"Path exceeds maximum length of {maxPathLength} characters for this OS");
        }

        // Validate path characters
        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c)))
        {
            throw new ArgumentException("Path contains invalid characters");
        }

        // Windows-specific reserved names check
        if (OperatingSystem.IsWindows())
        {
            var pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var part in pathParts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var nameWithoutExtension = Path.GetFileNameWithoutExtension(part).ToUpperInvariant();
                if (WindowsReservedNames.Contains(nameWithoutExtension))
                {
                    throw new ArgumentException($"Path contains reserved Windows name: {part}");
                }
            }
        }
    }

    public static void EnsureDirectoryExists(string path, ILogger? logger = null)
    {
        try
        {
            ValidateDirectoryPath(path);

            // Create directory if it doesn't exist
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                logger?.LogInformation("Created directory: {Path}", path);
            }

            // Verify write permissions
            var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid()}");
            try
            {
                File.WriteAllText(testFile, "test");
            }
            finally
            {
                // Ensure cleanup even if deletion fails
                try
                {
                    if (File.Exists(testFile))
                    {
                        File.Delete(testFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            var help = GetPlatformSpecificHelp(ex);
            var message = $"Insufficient permissions to access directory: {path}. " +
                         $"Please ensure the directory is writable or specify a different location.";

            if (!string.IsNullOrEmpty(help))
            {
                message += $" {help}";
            }

            throw new InvalidOperationException(message, ex);
        }
        catch (IOException ex) when (OperatingSystem.IsLinux())
        {
            // Linux-specific: Check if it's a permission issue
            var help = GetPlatformSpecificHelp(ex);
            var message = $"Cannot access directory: {path}.";

            if (!string.IsNullOrEmpty(help))
            {
                message += $" {help}";
            }
            else
            {
                message += $" On Linux, ensure you have proper permissions (try: chmod 755 {path})";
            }

            throw new InvalidOperationException(message, ex);
        }
        catch (ArgumentException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            var help = GetPlatformSpecificHelp(ex);
            var message = $"Cannot create or access directory: {path}.";

            if (!string.IsNullOrEmpty(help))
            {
                message += $" {help}";
            }

            throw new InvalidOperationException(message, ex);
        }
    }

    private static string GetPlatformSpecificHelp(Exception ex)
    {
        if (OperatingSystem.IsWindows() && ex is PathTooLongException)
        {
            return "Enable long path support in Windows: https://learn.microsoft.com/windows/win32/fileio/maximum-file-path-limitation";
        }

        if (OperatingSystem.IsLinux() && ex is UnauthorizedAccessException)
        {
            return "Check file permissions with 'ls -la' and use 'chmod' to fix access rights.";
        }

        if (OperatingSystem.IsMacOS() && ex is UnauthorizedAccessException)
        {
            return "Check System Preferences > Security & Privacy > Full Disk Access settings.";
        }

        return string.Empty;
    }
}
