using System.IO;
using System.Threading.Tasks;

using LibGit2Sharp;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed partial class WallpaperHarvester : IWallpaperHarvester
{
    private readonly ILogger<WallpaperHarvester> _logger;
    private readonly AppOptions _options;
    private readonly IConfiguration _config;

    public WallpaperHarvester(ILogger<WallpaperHarvester> logger, IOptions<AppOptions> options, IConfiguration config)
    {
        _logger = logger;
        _options = options.Value;
        _config = config;
    }

    public async Task HarvestAsync(CancellationToken cancellationToken = default)
    {
        var repos = _config.GetSection("WallpaperRepositories").Get<string[]>();
        if (repos is null || repos.Length == 0)
        {
            LogNoRepos(_logger);
            return;
        }

        var directory = _options.WallpaperDirectory;
        Directory.CreateDirectory(directory);

        var validRepos = repos.Where(repo =>
        {
            if (repo.Split('/') is not [var owner, var name])
            {
                LogInvalidRepo(_logger, repo);
                return false;
            }
            return true;
        }).ToArray();

        if (_options.UseParallel)
        {
            await Parallel.ForEachAsync(validRepos,
                new ParallelOptions { CancellationToken = cancellationToken },
                async (repo, ct) => await ProcessRepositoryAsync(repo, directory, ct));
        }
        else
        {
            foreach (var repo in validRepos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessRepositoryAsync(repo, directory, cancellationToken);
            }
        }
    }

    private async Task ProcessRepositoryAsync(string repo, string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var name = repo.Split('/')[1];
        var repoDir = Path.Combine(directory, name);

        if (Directory.Exists(repoDir))
        {
            // update existing repository
            try
            {
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

                cancellationToken.ThrowIfCancellationRequested();

                // Hard reset to remote HEAD to avoid detached HEAD state and merge conflicts
                var remoteBranch = repository.Branches[$"origin/{repository.Head.FriendlyName}"];
                if (remoteBranch is not null)
                {
                    repository.Reset(ResetMode.Hard, remoteBranch.Tip);
                }

                LogUpdated(_logger, repo);
            }
            catch (Exception ex)
            {
                LogUpdateFailed(_logger, ex, repo);
            }
        }
        else
        {
            // clone new repository
            try
            {
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
            catch (Exception ex)
            {
                LogCloneFailed(_logger, ex, repo);
            }
        }

        await Task.CompletedTask;
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
}