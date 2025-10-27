using System.CommandLine;

using KuriousLabs.WallpaperHarvester.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var directoryOption = new Option<string>(
    aliases: ["--directory", "-d"],
    getDefaultValue: () => "appsettings.json",
    description: "Path to config file");

var parallelOption = new Option<bool>(
    aliases: ["--parallel", "-p"],
    getDefaultValue: () => true,
    description: "Enable parallel processing of repositories");

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    getDefaultValue: () => false,
    description: "Enable verbose output including progress reporting");

var rootCommand = new RootCommand("WallpaperHarvester - Clone and update wallpaper repositories")
{
    directoryOption,
    parallelOption,
    verboseOption
};

rootCommand.SetHandler(async (configFile, useParallel, verbose) =>
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddJsonFile(configFile, optional: false, reloadOnChange: false);
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<AppOptions>(context.Configuration.GetSection("AppOptions"));
            services.PostConfigure<AppOptions>(options =>
            {
                options.ConfigFile = configFile;
                options.UseParallel = useParallel;
                options.Verbose = verbose;
            });
            services.AddTransient<IWallpaperHarvester, WallpaperHarvester>();
        })
        .Build();

    var harvester = host.Services.GetRequiredService<IWallpaperHarvester>();
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await harvester.HarvestAsync(lifetime.ApplicationStopping);
}, directoryOption, parallelOption, verboseOption);

return await rootCommand.InvokeAsync(args);