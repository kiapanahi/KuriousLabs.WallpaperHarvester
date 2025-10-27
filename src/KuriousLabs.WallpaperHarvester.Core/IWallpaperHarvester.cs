namespace KuriousLabs.WallpaperHarvester.Core;

public interface IWallpaperHarvester
{
    Task HarvestAsync(CancellationToken cancellationToken = default);
}