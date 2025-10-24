namespace KuriousLabs.WallpaperHarvester.Core;

internal sealed record AppOptions
{
    public string WallpaperDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Wallpapers");
    public int MaxParallelism { get; init; } = 4;
}