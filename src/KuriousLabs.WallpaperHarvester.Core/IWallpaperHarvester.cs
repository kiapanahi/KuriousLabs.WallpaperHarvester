namespace KuriousLabs.WallpaperHarvester.Core;

public interface IWallpaperHarvester
{
    Task<HarvestResult> HarvestAsync(CancellationToken cancellationToken = default);
}