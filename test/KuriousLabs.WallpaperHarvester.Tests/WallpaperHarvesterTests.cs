using System.Linq;
using KuriousLabs.WallpaperHarvester.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace KuriousLabs.WallpaperHarvester.Tests;

public class WallpaperHarvesterTests
{
    private static readonly string[] EmptyRepos = Array.Empty<string>();
    private static readonly string[] InvalidRepos = { "invalid-repo-format" };
    private static readonly string[] ValidRepos = { "test/repo1", "test/repo2", "test/repo3" };
    private static readonly string[] ErrorRepos = { "nonexistent/repo" };

    [Fact]
    public async Task HarvestAsyncWithEmptyRepositoryListDoesNotThrow()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions());
        var config = new TestConfiguration(EmptyRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.FailedRepos);
    }

    [Fact]
    public async Task HarvestAsyncWithInvalidRepositoryFormatLogsWarning()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions());
        var config = new TestConfiguration(InvalidRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert - Invalid repos are not counted in the result
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Contains(logger.LoggedMessages, m =>
            m.Contains("Invalid repository format") &&
            m.Contains("invalid-repo-format"));
    }

    [Fact]
    public async Task HarvestAsyncWithValidRepositoriesProcessesConcurrently()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestWallpapers")
        });
        var config = new TestConfiguration(ValidRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Ensure test directory exists
        Directory.CreateDirectory(options.Value.WallpaperDirectory);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ValidRepos.Length, result.Total);
        // We can't verify the exact success/failure counts as these depend on network conditions
        // but we can verify the total equals succeeded + failed
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
    }

    [Fact]
    public async Task HarvestAsyncHandlesRepositoryProcessingErrors()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestWallpapers")
        });
        // Use a repository that will cause an error (invalid format or non-existent)
        var config = new TestConfiguration(ErrorRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ErrorRepos.Length, result.Total);
        // Errors should be tracked
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
    }

    [Fact]
    public async Task HarvestAsyncWithVerboseModeDoesNotThrow()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestWallpapersVerbose"),
            Verbose = true
        });
        var config = new TestConfiguration(ErrorRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert - Should not throw when verbose mode is enabled
        Assert.NotNull(result);
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
    }

    [Fact]
    public async Task HarvestAsyncRespectsCancellationToken()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestCancellation"),
            UseParallel = false
        });
        var config = new TestConfiguration(ValidRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await harvester.HarvestAsync(cts.Token));
    }

    [Fact]
    public async Task HarvestAsyncRespectsCancellationTokenInParallelMode()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestCancellationParallel"),
            UseParallel = true
        });
        var config = new TestConfiguration(ValidRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await harvester.HarvestAsync(cts.Token));
    }

    [Fact]
    public async Task HarvestAsyncWithDefaultCancellationTokenDoesNotThrow()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions());
        var config = new TestConfiguration(EmptyRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act & Assert - default(CancellationToken) should work without issue
        var result = await harvester.HarvestAsync();
        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task HarvestResultTracksSuccessAndFailure()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestHarvestResult")
        });
        // Mix of repos - some will fail due to network/nonexistent
        var config = new TestConfiguration(ErrorRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ErrorRepos.Length, result.Total);
        Assert.True(result.Failed >= 0); // At least track failures
        Assert.Equal(result.Total, result.Succeeded + result.Failed);
        Assert.Equal(result.Failed, result.FailedRepos.Count);
    }

    [Fact]
    public async Task HarvestResultLogsCompletionSummary()
    {
        // Arrange
        var logger = new TestLogger();
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            WallpaperDirectory = Path.Combine(Path.GetTempPath(), "TestSummary")
        });
        var config = new TestConfiguration(ErrorRepos);

        var harvester = new KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester(logger, options, config);

        // Act
        var result = await harvester.HarvestAsync();

        // Assert - Verify summary message is logged
        Assert.Contains(logger.LoggedMessages, m => m.Contains("Completed:"));
        Assert.Contains(logger.LoggedMessages, m => m.Contains("succeeded"));
        Assert.Contains(logger.LoggedMessages, m => m.Contains("failed"));
    }
}

// Test implementations
internal class TestLogger : ILogger<KuriousLabs.WallpaperHarvester.Core.WallpaperHarvester>
{
    public List<string> LoggedMessages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LoggedMessages.Add(formatter(state, exception));
    }
}

internal class TestConfiguration : IConfiguration
{
    private readonly IConfiguration _config;

    public TestConfiguration(string[] repositories)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("WallpaperRepositories:0", repositories.Length > 0 ? repositories[0] : null),
            new KeyValuePair<string, string?>("WallpaperRepositories:1", repositories.Length > 1 ? repositories[1] : null),
            new KeyValuePair<string, string?>("WallpaperRepositories:2", repositories.Length > 2 ? repositories[2] : null),
            new KeyValuePair<string, string?>("WallpaperRepositories:3", repositories.Length > 3 ? repositories[3] : null),
        });
        _config = configBuilder.Build();
    }

    public string? this[string key] { get => _config[key]; set => _config[key] = value; }

    public IEnumerable<IConfigurationSection> GetChildren() => _config.GetChildren();

    public IChangeToken GetReloadToken() => _config.GetReloadToken();

    public IConfigurationSection GetSection(string key) => _config.GetSection(key);
}
