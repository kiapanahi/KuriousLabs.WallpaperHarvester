using KuriousLabs.WallpaperHarvester.Core;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.WallpaperHarvester.Tests;

public sealed class DirectoryHelperTests
{
    [Fact]
    public void GetDefaultWallpaperDirectory_ReturnsValidPath()
    {
        // Act
        var path = DirectoryHelper.GetDefaultWallpaperDirectory();

        // Assert
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.EndsWith("Wallpapers", path);
    }

    [Fact]
    public void ValidateDirectoryPath_WithValidPath_DoesNotThrow()
    {
        // Arrange
        var validPath = Path.Combine(Path.GetTempPath(), "ValidPath");

        // Act & Assert
        var exception = Record.Exception(() => DirectoryHelper.ValidateDirectoryPath(validPath));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateDirectoryPath_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(null!));
        Assert.Contains("Path cannot be null or whitespace", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(string.Empty));
        Assert.Contains("Path cannot be null or whitespace", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath("   "));
        Assert.Contains("Path cannot be null or whitespace", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithExcessivelyLongPath_ThrowsArgumentException()
    {
        // Arrange
        var maxLength = OperatingSystem.IsWindows() ? 260 : 4096;
        var longPath = new string('a', maxLength + 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(longPath));
        Assert.Contains("exceeds maximum length", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithInvalidCharacters_ThrowsArgumentException()
    {
        // Arrange - use a character that's invalid on all platforms
        var invalidPath = "C:\0invalid";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(invalidPath));
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithReservedWindowsName_ThrowsArgumentExceptionOnWindows()
    {
        // Only run on Windows
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var reservedPath = Path.Combine(Path.GetTempPath(), "CON", "test");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(reservedPath));
        Assert.Contains("reserved Windows name", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithReservedWindowsNameWithExtension_ThrowsArgumentExceptionOnWindows()
    {
        // Only run on Windows
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var reservedPath = Path.Combine(Path.GetTempPath(), "NUL.txt");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.ValidateDirectoryPath(reservedPath));
        Assert.Contains("reserved Windows name", exception.Message);
    }

    [Fact]
    public void EnsureDirectoryExists_WithValidPath_CreatesDirectory()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"TestDir_{Guid.NewGuid()}");

        try
        {
            // Act
            DirectoryHelper.EnsureDirectoryExists(testDir);

            // Assert
            Assert.True(Directory.Exists(testDir));
        }
        finally
        {
            // Cleanup
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithExistingDirectory_DoesNotThrow()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"ExistingDir_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Act
            var exception = Record.Exception(() => DirectoryHelper.EnsureDirectoryExists(testDir));

            // Assert
            Assert.Null(exception);
            Assert.True(Directory.Exists(testDir));
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithReadOnlyDirectory_ThrowsInvalidOperationException()
    {
        // This test is platform-specific and may not work on Windows
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var readOnlyDir = Path.Combine(Path.GetTempPath(), $"ReadOnly_{Guid.NewGuid()}");
        Directory.CreateDirectory(readOnlyDir);

        try
        {
            // Make directory read-only on Unix-like systems
            File.SetUnixFileMode(readOnlyDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => DirectoryHelper.EnsureDirectoryExists(readOnlyDir));
            Assert.Contains("permissions", exception.Message);
        }
        finally
        {
            // Cleanup: restore write permissions and delete
            try
            {
                File.SetUnixFileMode(readOnlyDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.Delete(readOnlyDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithInvalidPath_ThrowsArgumentException()
    {
        // Arrange
        var invalidPath = new string('a', 5000); // Too long for any platform

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => DirectoryHelper.EnsureDirectoryExists(invalidPath));
        Assert.Contains("exceeds maximum length", exception.Message);
    }

    [Fact]
    public void EnsureDirectoryExists_WithLogger_LogsDirectoryCreation()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"LoggerTest_{Guid.NewGuid()}");
        var logger = new TestLogger();

        try
        {
            // Act
            DirectoryHelper.EnsureDirectoryExists(testDir, logger);

            // Assert
            Assert.True(Directory.Exists(testDir));
            Assert.Contains(logger.LogMessages, msg => msg.Contains("Created directory"));
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithNestedPath_CreatesAllDirectories()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), $"Base_{Guid.NewGuid()}");
        var nestedDir = Path.Combine(baseDir, "level1", "level2", "level3");

        try
        {
            // Act
            DirectoryHelper.EnsureDirectoryExists(nestedDir);

            // Assert
            Assert.True(Directory.Exists(nestedDir));
            Assert.True(Directory.Exists(Path.Combine(baseDir, "level1")));
            Assert.True(Directory.Exists(Path.Combine(baseDir, "level1", "level2")));
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(baseDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private sealed class TestLogger : ILogger
    {
        public List<string> LogMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogMessages.Add(formatter(state, exception));
        }
    }
}
