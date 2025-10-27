using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    private readonly IConfiguration _configuration;

    public AppOptionsValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var errors = new List<string>();

        // Validate that WallpaperRepositories section exists
        var reposSection = _configuration.GetSection("WallpaperRepositories");
        if (!reposSection.Exists())
        {
            errors.Add("Configuration section 'WallpaperRepositories' is missing.");
        }

        // Validate that at least one repository is configured
        var repos = reposSection.Get<string[]>();
        if (repos is null || repos.Length == 0)
        {
            errors.Add("At least one repository must be configured in 'WallpaperRepositories'.");
        }
        else
        {
            // Validate repository name format
            foreach (var repo in repos)
            {
                if (!IsValidRepositoryFormat(repo))
                {
                    errors.Add($"Invalid repository format: '{repo}'. Expected format: 'owner/repo'");
                }
            }
        }

        // Validate directory writeability
        try
        {
            ValidateDirectoryAccess(options.WallpaperDirectory);
        }
        catch (Exception ex)
        {
            errors.Add($"Cannot write to wallpaper directory '{options.WallpaperDirectory}': {ex.Message}");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }

    public static bool IsValidRepositoryFormat(string repo)
    {
        return repo.Split('/') is [var owner, var name]
            && !string.IsNullOrWhiteSpace(owner)
            && !string.IsNullOrWhiteSpace(name);
    }

    private static void ValidateDirectoryAccess(string directory)
    {
        Directory.CreateDirectory(directory);
        var testFile = Path.Combine(directory, ".write-test");
        try
        {
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Cannot write to directory: {directory}", ex);
        }
    }
}
