namespace KuriousLabs.WallpaperHarvester.Core;

public sealed record HarvestResult(int Total, int Succeeded, int Failed, IReadOnlyList<string> FailedRepos);
