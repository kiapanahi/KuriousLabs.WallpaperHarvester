using KuriousLabs.WallpaperHarvester.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<AppOptions>(context.Configuration.GetSection("AppOptions"));
        services.AddTransient<IWallpaperHarvester, WallpaperHarvester>();
    })
    .Build();

var harvester = host.Services.GetRequiredService<IWallpaperHarvester>();
await harvester.HarvestAsync();