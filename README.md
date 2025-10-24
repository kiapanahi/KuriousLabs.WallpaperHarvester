# KuriousLabs Wallpaper Harvester

A .NET console application that automatically clones and updates GitHub repositories containing desktop wallpapers.

## Features

- 🎨 Clone wallpaper repositories from GitHub
- 🔄 Automatically update existing repositories with the latest content
- ⚙️ Configure repositories via `appsettings.json`
- 📁 Customizable wallpaper directory (defaults to `%USERPROFILE%\Pictures\Wallpapers`)
- 📝 Built-in logging using .NET's default logger
- 🚀 Uses .NET Generic Host for dependency injection and configuration

## Requirements

- .NET 10.0 or later

## Installation

Clone the repository:

```bash
git clone https://github.com/kiapanahi/KuriousLabs.WallpaperHarvester.git
cd KuriousLabs.WallpaperHarvester
```

## Configuration

Edit `src/KuriousLabs.WallpaperHarvester.Core/appsettings.json` to configure the wallpaper repositories you want to harvest:

```json
{
    "WallpaperRepositories": [
        "makccr/wallpapers",
        "SudhanFromGithub/Awesome-Wallpapers-Collection",
        "Ajaymanikandan0x/hyprland_wallpapers",
        "houssemko/Minimalistic-desktop-wallpapers"
    ]
}
```

## Usage

### Using the PowerShell script (Recommended)

```powershell
.\run.ps1
```

This runs the application in Release mode.

### Using .NET CLI

```bash
dotnet run --project src/KuriousLabs.WallpaperHarvester.Core
```

### Custom wallpaper directory

```bash
dotnet run --project src/KuriousLabs.WallpaperHarvester.Core --wallpaper-directory "C:\My\Custom\Path"
```

## How It Works

1. The application reads the list of GitHub repositories from `appsettings.json`
2. For each repository:
   - If not already cloned: Clones it to the wallpaper directory
   - If already exists: Fetches and updates to the latest version
3. All operations are logged to the console

## Project Structure

```
KuriousLabs.WallpaperHarvester/
├── src/
│   └── KuriousLabs.WallpaperHarvester.Core/  # Main console application
├── Directory.Build.props                      # Shared build properties
├── Directory.Packages.props                   # Central package management
└── run.ps1                                    # PowerShell run script
```

## Technologies Used

- .NET 10
- LibGit2Sharp for Git operations
- Microsoft.Extensions.Hosting for Generic Host
- Source-generated logging with LoggerMessage attributes

## License

MIT License - see [LICENSE](LICENSE) file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Author

**Kia Raad** - [@kiapanahirad](https://github.com/kiapanahi)
