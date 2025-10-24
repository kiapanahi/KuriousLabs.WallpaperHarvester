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

    public async Task HarvestAsync()
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

        var tasks = validRepos.Select(repo => ProcessRepositoryAsync(repo, directory));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessRepositoryAsync(string repo, string directory)
    {
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

                Commands.Fetch(repository, remote.Name, refSpecs, null, "Fetching updates");

                // Fast-forward merge if possible
                var remoteBranch = repository.Branches[$"origin/{repository.Head.FriendlyName}"];
                if (remoteBranch is not null)
                {
                    var signature = new Signature("WallpaperHarvester", "harvester@kuriouslabs.com", DateTimeOffset.Now);
                    Commands.Checkout(repository, remoteBranch.Tip);
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
                Repository.Clone($"https://github.com/{repo}.git", repoDir);
                LogCloned(_logger, repo);
            }
            catch (Exception ex)
            {
                LogCloneFailed(_logger, ex, repo);
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