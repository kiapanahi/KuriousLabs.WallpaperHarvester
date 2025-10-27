using KuriousLabs.WallpaperHarvester.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KuriousLabs.WallpaperHarvester.Tests;

public sealed class AppOptionsValidatorTests
{
    [Fact]
    public void Validate_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner1/repo1"),
            new KeyValuePair<string, string?>("WallpaperRepositories:1", "owner2/repo2"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "ValidTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "ValidTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithMissingRepositoriesSection_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "MissingReposTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "MissingReposTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("'WallpaperRepositories' is missing"));
    }

    [Fact]
    public void Validate_WithEmptyRepositoriesList_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "EmptyReposTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "EmptyReposTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("At least one repository must be configured"));
    }

    [Fact]
    public void Validate_WithInvalidRepositoryFormat_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "invalid-repo-format"),
            new KeyValuePair<string, string?>("WallpaperRepositories:1", "owner/repo"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "InvalidFormatTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "InvalidFormatTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("Invalid repository format: 'invalid-repo-format'"));
    }

    [Fact]
    public void Validate_WithRepositoryHavingEmptyOwner_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "/repo"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "EmptyOwnerTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "EmptyOwnerTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("Invalid repository format: '/repo'"));
    }

    [Fact]
    public void Validate_WithRepositoryHavingEmptyName_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner/"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "EmptyNameTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "EmptyNameTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("Invalid repository format: 'owner/'"));
    }

    [Fact]
    public void Validate_WithReadOnlyDirectory_ReturnsFailed()
    {
        // This test is platform-specific and may not work in all environments
        // Skip on Windows as setting read-only is more complex
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var readOnlyDir = Path.Combine(Path.GetTempPath(), "ReadOnlyTest_" + Guid.NewGuid());
        Directory.CreateDirectory(readOnlyDir);
        
        try
        {
            // Make directory read-only on Unix-like systems
            File.SetUnixFileMode(readOnlyDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner/repo"),
                new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", readOnlyDir)
            });
            var config = configBuilder.Build();
            var validator = new AppOptionsValidator(config);
            var options = new AppOptions
            {
                WallpaperDirectory = readOnlyDir
            };

            // Act
            var result = validator.Validate(null, options);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Failures, f => f.Contains("Cannot write to wallpaper directory"));
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
    public void Validate_WithWritableDirectory_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var newDir = Path.Combine(Path.GetTempPath(), "NewDirTest_" + Guid.NewGuid());
        
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner/repo"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", newDir)
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = newDir
        };

        try
        {
            // Act
            var result = validator.Validate(null, options);

            // Assert
            Assert.True(result.Succeeded);
            Assert.True(Directory.Exists(newDir));
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(newDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Validate_WithMultipleValidRepos_ReturnsSuccess()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner1/repo1"),
            new KeyValuePair<string, string?>("WallpaperRepositories:1", "owner2/repo2"),
            new KeyValuePair<string, string?>("WallpaperRepositories:2", "owner3/repo3"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "MultipleReposTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "MultipleReposTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithMixedValidAndInvalidRepos_ReturnsFailed()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", "owner1/repo1"),
            new KeyValuePair<string, string?>("WallpaperRepositories:1", "invalid-format"),
            new KeyValuePair<string, string?>("WallpaperRepositories:2", "owner3/repo3"),
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", Path.Combine(Path.GetTempPath(), "MixedReposTest"))
        });
        var config = configBuilder.Build();
        var validator = new AppOptionsValidator(config);
        var options = new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "MixedReposTest")
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("Invalid repository format: 'invalid-format'"));
    }
}
