using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using LibGit2Sharp;
using System.IO;

namespace KuriousLabs.WallpaperHarvester.Core;

public class WallpaperHarvester : IWallpaperHarvester
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
            _logger.LogWarning("No repositories configured");
            return;
        }

        var directory = _options.WallpaperDirectory;
        Directory.CreateDirectory(directory);

        foreach (var repo in repos)
        {
            var parts = repo.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid repository format: {repo}", repo);
                continue;
            }

            var owner = parts[0];
            var name = parts[1];
            var repoDir = Path.Combine(directory, name);

            if (Directory.Exists(repoDir))
            {
                // update
                try
                {
                    using var repository = new Repository(repoDir);
                    var signature = new Signature("WallpaperHarvester", "harvester@kuriouslabs.com", DateTimeOffset.Now);
                    Commands.Pull(repository, signature, new PullOptions());
                    _logger.LogInformation("Updated {repo}", repo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update {repo}", repo);
                }
            }
            else
            {
                // clone
                try
                {
                    Repository.Clone($"https://github.com/{repo}.git", repoDir);
                    _logger.LogInformation("Cloned {repo}", repo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clone {repo}", repo);
                }
            }
        }
    }
}