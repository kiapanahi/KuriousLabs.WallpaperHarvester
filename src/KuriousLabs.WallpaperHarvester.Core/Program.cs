using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using KuriousLabs.WallpaperHarvester.Core;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<AppOptions>(context.Configuration.GetSection("AppOptions"));
        services.AddTransient<IWallpaperHarvester, WallpaperHarvester>();
    })
    .Build();

var harvester = host.Services.GetRequiredService<IWallpaperHarvester>();
await harvester.HarvestAsync();
