namespace KuriousLabs.WallpaperHarvester.Core;

public sealed record AppOptions
{
    public string WallpaperDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Wallpapers");
}