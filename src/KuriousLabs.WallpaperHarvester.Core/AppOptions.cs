namespace KuriousLabs.WallpaperHarvester.Core;

public class AppOptions
{
    public string WallpaperDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Wallpapers");
}