using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

using LibGit2Sharp;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed partial class WallpaperHarvester : IWallpaperHarvester
{
    private readonly ILogger<WallpaperHarvester> _logger;
    private readonly AppOptions _options;
    private readonly IConfiguration _config;
    private readonly ResiliencePipeline _retryPipeline;

    public WallpaperHarvester(ILogger<WallpaperHarvester> logger, IOptions<AppOptions> options, IConfiguration config)
    {
        _logger = logger;
        _options = options.Value;
        _config = config;
        
        // Create retry pipeline for transient failures
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<LibGit2SharpException>(ex => IsTransientException(ex))
                    .Handle<IOException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    LogRetry(_logger, args.AttemptNumber, args.Outcome.Exception?.Message ?? "Unknown error");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<HarvestResult> HarvestAsync(CancellationToken cancellationToken = default)
    {
        var repos = _config.GetSection("WallpaperRepositories").Get<string[]>();
        if (repos is null || repos.Length == 0)
        {
            LogNoRepos(_logger);
            return new HarvestResult(0, 0, 0, new List<string>());
        }

        var directory = _options.WallpaperDirectory;
        Directory.CreateDirectory(directory);

        var validRepos = repos.Where(repo =>
        {
            if (!AppOptionsValidator.IsValidRepositoryFormat(repo))
            {
                LogInvalidRepo(_logger, repo);
                return false;
            }
            return true;
        }).ToArray();

        var succeeded = 0;
        var failed = 0;
        var failedRepos = new ConcurrentBag<string>();

        if (_options.UseParallel)
        {
            var tasks = validRepos.Select(async repo =>
            {
                var success = await ProcessRepositoryAsync(repo, directory, cancellationToken);
                if (success)
                {
                    Interlocked.Increment(ref succeeded);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    failedRepos.Add(repo);
                }
            });
            await Task.WhenAll(tasks);
        }
        else
        {
            foreach (var repo in validRepos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var success = await ProcessRepositoryAsync(repo, directory, cancellationToken);
                if (success)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                    failedRepos.Add(repo);
                }
            }
        }

        var result = new HarvestResult(validRepos.Length, succeeded, failed, failedRepos.ToArray());
        LogHarvestSummary(_logger, succeeded, validRepos.Length, failed);
        return result;
    }

    private async Task<bool> ProcessRepositoryAsync(string repo, string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var name = repo.Split('/')[1];
        var repoDir = Path.Combine(directory, name);
        var isUpdate = Directory.Exists(repoDir);

        try
        {
            await _retryPipeline.ExecuteAsync(ct =>
            {
                if (isUpdate)
                {
                    // update existing repository
                    using var repository = new Repository(repoDir);
                    var remote = repository.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                    var fetchOptions = new FetchOptions();
                    if (_options.Verbose)
                    {
                        fetchOptions.OnTransferProgress = progress =>
                        {
                            var percent = progress.TotalObjects > 0
                                ? (100 * progress.ReceivedObjects) / progress.TotalObjects
                                : 0;
                            LogFetchProgress(_logger, repo, percent, progress.ReceivedObjects, progress.TotalObjects);
                            return true;
                        };
                    }

                    Commands.Fetch(repository, remote.Name, refSpecs, fetchOptions, "Fetching updates");

                    ct.ThrowIfCancellationRequested();

                    // Hard reset to remote HEAD to avoid detached HEAD state and merge conflicts
                    var remoteBranch = repository.Branches[$"origin/{repository.Head.FriendlyName}"];
                    if (remoteBranch is not null)
                    {
                        repository.Reset(ResetMode.Hard, remoteBranch.Tip);
                    }

                    LogUpdated(_logger, repo);
                }
                else
                {
                    // clone new repository
                    var cloneOptions = new CloneOptions();
                    if (_options.Verbose)
                    {
                        cloneOptions.FetchOptions.OnProgress = output =>
                        {
                            LogCloneProgress(_logger, repo, output.TrimEnd());
                            return true;
                        };

                        cloneOptions.FetchOptions.OnTransferProgress = progress =>
                        {
                            var percent = progress.TotalObjects > 0
                                ? (100 * progress.ReceivedObjects) / progress.TotalObjects
                                : 0;
                            LogCloneTransferProgress(_logger, repo, percent, progress.ReceivedObjects, progress.TotalObjects);
                            return true;
                        };
                    }

                    Repository.Clone($"https://github.com/{repo}.git", repoDir, cloneOptions);
                    LogCloned(_logger, repo);
                }

                return ValueTask.CompletedTask;
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            if (isUpdate)
            {
                LogUpdateFailed(_logger, ex, repo);
            }
            else
            {
                LogCloneFailed(_logger, ex, repo);
            }
            return false;
        }
    }

    private static bool IsTransientException(LibGit2SharpException ex)
    {
        // Check for transient network errors that should be retried
        // LibGit2Sharp doesn't expose ErrorCode directly, so we need to rely on message parsing
        // Common transient error patterns in LibGit2Sharp:
        var message = ex.Message.ToLowerInvariant();
        
        // Network connectivity issues
        if (message.Contains("timeout") ||
            message.Contains("timed out") ||
            message.Contains("connection") ||
            message.Contains("could not resolve host") ||
            message.Contains("failed to receive") ||
            message.Contains("failed to connect") ||
            message.Contains("network is unreachable") ||
            message.Contains("temporary failure"))
        {
            return true;
        }
        
        // Don't retry permanent failures
        // - Authentication/permission errors
        // - Invalid repository paths
        // - Corrupted repositories
        if (message.Contains("authentication") ||
            message.Contains("permission denied") ||
            message.Contains("does not exist") ||
            message.Contains("not found") ||
            message.Contains("access denied") ||
            message.Contains("invalid") ||
            message.Contains("corrupt"))
        {
            return false;
        }
        
        // Default to not retrying if we can't classify the error
        return false;
    }

    [LoggerMessage(LogLevel.Information, "Updated {repo}")]
    static partial void LogUpdated(ILogger logger, string repo);

    [LoggerMessage(LogLevel.Information, "Cloned {repo}")]
    static partial void LogCloned(ILogger logger, string repo);

    [LoggerMessage(LogLevel.Warning, "Invalid repository format: {repo}")]
    static partial void LogInvalidRepo(ILogger logger, string repo);

    [LoggerMessage(LogLevel.Warning, "No repositories configured")]
    static partial void LogNoRepos(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to update {repo}")]
    static partial void LogUpdateFailed(ILogger logger, Exception ex, string repo);

    [LoggerMessage(LogLevel.Error, "Failed to clone {repo}")]
    static partial void LogCloneFailed(ILogger logger, Exception ex, string repo);

    [LoggerMessage(LogLevel.Debug, "[{repo}] Fetch progress: {percent}% ({receivedObjects}/{totalObjects})")]
    static partial void LogFetchProgress(ILogger logger, string repo, int percent, int receivedObjects, int totalObjects);

    [LoggerMessage(LogLevel.Debug, "[{repo}] Clone: {message}")]
    static partial void LogCloneProgress(ILogger logger, string repo, string message);

    [LoggerMessage(LogLevel.Debug, "[{repo}] Clone transfer: {percent}% ({receivedObjects}/{totalObjects})")]
    static partial void LogCloneTransferProgress(ILogger logger, string repo, int percent, int receivedObjects, int totalObjects);

    [LoggerMessage(LogLevel.Warning, "Retry {retryCount} due to: {error}")]
    static partial void LogRetry(ILogger logger, int retryCount, string error);

    [LoggerMessage(LogLevel.Information, "Completed: {succeeded}/{total} succeeded, {failed} failed")]
    static partial void LogHarvestSummary(ILogger logger, int succeeded, int total, int failed);
}