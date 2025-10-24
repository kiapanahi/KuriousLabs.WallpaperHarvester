using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using KuriousLabs.WallpaperHarvester.Core;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config => config.AddJsonFile("appsettings.json", optional: true))
    .ConfigureServices((context, services) =>
    {
        services.Configure<AppOptions>(context.Configuration.GetSection("AppOptions"));
        services.AddTransient<IWallpaperHarvester, WallpaperHarvester>();
    })
    .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
    .Build();

var harvester = host.Services.GetRequiredService<IWallpaperHarvester>();
await harvester.HarvestAsync();
