using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using LibGit2Sharp;
using System.IO;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KuriousLabs.WallpaperHarvester.Core;

internal sealed partial class WallpaperHarvester : IWallpaperHarvester
{
    private readonly AppOptions _options;
    private readonly IConfiguration _config;

    public WallpaperHarvester(IOptions<AppOptions> options, IConfiguration config)
    {
        _options = options.Value;
        _config = config;
    }

    public async Task HarvestAsync()
    {
        var repos = _config.GetSection("WallpaperRepositories").Get<string[]>();
        if (repos is null || repos.Length == 0)
        {
            LogNoRepos(null!);
            return;
        }

        var directory = _options.WallpaperDirectory;
        Directory.CreateDirectory(directory);

        foreach (var repo in repos)
        {
            if (repo.Split('/') is not [var owner, var name])
            {
                LogInvalidRepo(null!, repo);
                continue;
            }

            var repoDir = Path.Combine(directory, name);

            if (Directory.Exists(repoDir))
            {
                // update
                try
                {
                    using var repository = new Repository(repoDir);
                    var signature = new Signature("WallpaperHarvester", "harvester@kuriouslabs.com", DateTimeOffset.Now);
                    Commands.Pull(repository, signature, new PullOptions());
                    LogUpdated(null!, repo);
                }
                catch (Exception ex)
                {
                    LogUpdateFailed(null!, ex, repo);
                }
            }
            else
            {
                // clone
                try
                {
                    Repository.Clone($"https://github.com/{repo}.git", repoDir);
                    LogCloned(null!, repo);
                }
                catch (Exception ex)
                {
                    LogCloneFailed(null!, ex, repo);
                }
            }
        }
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
}