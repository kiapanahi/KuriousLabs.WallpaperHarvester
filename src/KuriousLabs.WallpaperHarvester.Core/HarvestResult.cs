namespace KuriousLabs.WallpaperHarvester.Core;

public sealed record HarvestResult(int Total, int Succeeded, int Failed, List<string> FailedRepos);
