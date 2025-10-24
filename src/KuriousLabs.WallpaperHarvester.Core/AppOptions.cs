namespace KuriousLabs.WallpaperHarvester.Core;

public sealed class AppOptions
{
    public string WallpaperDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Wallpapers");
    public string ConfigFile { get; set; } = "appsettings.json";
    public bool UseParallel { get; set; } = true;
}