using System.ComponentModel.DataAnnotations;

namespace KuriousLabs.WallpaperHarvester.Core;

public sealed class AppOptions
{
    [Required(ErrorMessage = "WallpaperDirectory is required")]
    public string WallpaperDirectory { get; set; } = DirectoryHelper.GetDefaultWallpaperDirectory();
    
    [Required(ErrorMessage = "ConfigFile is required")]
    public string ConfigFile { get; set; } = "appsettings.json";
    
    public bool UseParallel { get; set; } = true;
    
    public bool Verbose { get; set; }
}