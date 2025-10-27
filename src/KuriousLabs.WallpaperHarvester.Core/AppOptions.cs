using System.ComponentModel.DataAnnotations;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed class AppOptions
{
    [Required(ErrorMessage = "WallpaperDirectory is required")]
    public string WallpaperDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Wallpapers");
    
    [Required(ErrorMessage = "ConfigFile is required")]
    public string ConfigFile { get; set; } = "appsettings.json";
    
    public bool UseParallel { get; set; } = true;
    
    public bool Verbose { get; set; }
}