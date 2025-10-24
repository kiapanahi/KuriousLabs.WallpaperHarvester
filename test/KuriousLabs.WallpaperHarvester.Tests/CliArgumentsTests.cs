using KuriousLabs.WallpaperHarvester.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KuriousLabs.WallpaperHarvester.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void DefaultOptions_ShouldUseDefaultValues()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", "/tmp/test")
        });
        var config = configBuilder.Build();

        // Act
        var options = new AppOptions();
        config.GetSection("AppOptions").Bind(options);

        // Assert
        Assert.Equal("appsettings.json", options.ConfigFile);
        Assert.True(options.UseParallel);
    }

    [Fact]
    public void AppOptions_ShouldAllowOverridingConfigFile()
    {
        // Arrange
        var options = new AppOptions();

        // Act
        options.ConfigFile = "/custom/path/config.json";

        // Assert
        Assert.Equal("/custom/path/config.json", options.ConfigFile);
    }

    [Fact]
    public void AppOptions_ShouldAllowDisablingParallel()
    {
        // Arrange
        var options = new AppOptions();

        // Act
        options.UseParallel = false;

        // Assert
        Assert.False(options.UseParallel);
    }

    [Fact]
    public void PostConfigure_ShouldOverrideOptionsFromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("AppOptions:WallpaperDirectory", "/tmp/test"),
            new KeyValuePair<string, string?>("AppOptions:UseParallel", "true")
        });
        var config = configBuilder.Build();

        services.Configure<AppOptions>(config.GetSection("AppOptions"));
        services.PostConfigure<AppOptions>(options =>
        {
            options.ConfigFile = "/custom/config.json";
            options.UseParallel = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var optionsSnapshot = serviceProvider.GetRequiredService<IOptions<AppOptions>>();

        // Act
        var options = optionsSnapshot.Value;

        // Assert
        Assert.Equal("/custom/config.json", options.ConfigFile);
        Assert.False(options.UseParallel);
        Assert.Equal("/tmp/test", options.WallpaperDirectory);
    }
}
