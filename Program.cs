using System.CommandLine;
using Microsoft.Extensions.Configuration;
using NugetConfigCreator.Templates;
using NugetConfigCreator.Configuration;
using NugetConfigCreator.Utilities;

namespace NugetConfigCreator;

class Program
{
    private static AppSettings? _appSettings;

    static async Task<int> Main(string[] args)
    {
        // Load configuration
        LoadConfiguration();

        var rootCommand = new RootCommand("NuGet Config Creator - A tool for generating NuGet.config files");

        // Create commands dynamically from configuration
        CreateDynamicCommands(rootCommand);

        // Handle direct execution without subcommand - create standard config by default
        if (args.Length == 0)
        {
            var template = new StandardNuGetConfigTemplate(_appSettings!.NuGetFeeds);
            var config = template.GenerateConfig();
            File.WriteAllText("NuGet.config", config);
            Console.WriteLine("Standard NuGet.config created successfully!");
            Console.WriteLine();
            ShowUsage();
            return 0;
        }

        return await rootCommand.InvokeAsync(args);
    }

    private static void CreateDynamicCommands(RootCommand rootCommand)
    {
        // Standard command (always available)
        var standardCommand = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Create a standard NuGet.config with only nuget.org feed");
        standardCommand.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (manager.ConfigExists)
            {
                if (manager.KeyExists(_appSettings!.NuGetFeeds.NuGetOrg.Key))
                {
                    Console.WriteLine($"NuGet.config already exists and contains the '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' key.");
                    return;
                }
                manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.NuGetOrg.Key, _appSettings!.NuGetFeeds.NuGetOrg.Url, _appSettings!.NuGetFeeds.NuGetOrg.ProtocolVersion);
                manager.SaveConfig();
                Console.WriteLine($"Added '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' key to existing NuGet.config!");
            }
            else
            {
                var template = new StandardNuGetConfigTemplate(_appSettings!.NuGetFeeds);
                var config = template.GenerateConfig();
                File.WriteAllText("NuGet.config", config);
                Console.WriteLine("Standard NuGet.config created successfully!");
            }
        });
        rootCommand.AddCommand(standardCommand);

        // Local feed command
        var localCommand = new Command(_appSettings!.NuGetFeeds.Local.Command, "Create a NuGet.config with local feed");
        var localPathOption = new Option<string>(
            name: "--path",
            description: "Path to the local NuGet feed",
            getDefaultValue: () => _appSettings!.NuGetFeeds.Local.DefaultPath
        );
        localCommand.AddOption(localPathOption);
        localCommand.SetHandler((string path) =>
        {
            var manager = new NuGetConfigManager();
            if (manager.ConfigExists)
            {
                if (manager.KeyExists(_appSettings!.NuGetFeeds.Local.Key))
                {
                    Console.WriteLine($"NuGet.config already exists and contains the '{_appSettings!.NuGetFeeds.Local.Key}' key.");
                    return;
                }
                manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.Local.Key, path);
                manager.SaveConfig();
                Console.WriteLine($"Added '{_appSettings!.NuGetFeeds.Local.Key}' key to existing NuGet.config!");
            }
            else
            {
                var template = new LocalFeedNuGetConfigTemplate(_appSettings!.NuGetFeeds, path);
                var config = template.GenerateConfig();
                File.WriteAllText("NuGet.config", config);
                Console.WriteLine($"NuGet.config with local feed ({path}) created successfully!");
            }
        }, localPathOption);
        rootCommand.AddCommand(localCommand);

        // MyGet command
        var mygetCommand = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Create a NuGet.config with MyGet.org feed");
        mygetCommand.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (manager.ConfigExists)
            {
                if (manager.KeyExists(_appSettings!.NuGetFeeds.MyGet.Key))
                {
                    Console.WriteLine($"NuGet.config already exists and contains the '{_appSettings!.NuGetFeeds.MyGet.Key}' key.");
                    return;
                }
                manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.MyGet.Key, _appSettings!.NuGetFeeds.MyGet.Url);
                manager.SaveConfig();
                Console.WriteLine($"Added '{_appSettings!.NuGetFeeds.MyGet.Key}' key to existing NuGet.config!");
            }
            else
            {
                var template = new MyGetNuGetConfigTemplate(_appSettings!.NuGetFeeds);
                var config = template.GenerateConfig();
                File.WriteAllText("NuGet.config", config);
                Console.WriteLine("NuGet.config with MyGet.org feed created successfully!");
            }
        });
        rootCommand.AddCommand(mygetCommand);

        // Remove command
        var removeCommand = new Command("remove", "Remove a key from existing NuGet.config");
        var removeKeyOption = new Option<string>(
            name: "--key",
            description: "The key to remove from NuGet.config"
        );
        removeCommand.AddOption(removeKeyOption);
        removeCommand.SetHandler((string key) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            if (!manager.KeyExists(key))
            {
                Console.WriteLine($"Key '{key}' not found in NuGet.config.");
                return;
            }

            manager.RemoveKey(key);
            manager.SaveConfig();
            Console.WriteLine($"Removed key '{key}' from NuGet.config successfully!");
        }, removeKeyOption);
        rootCommand.AddCommand(removeCommand);

        // Disable command
        var disableCommand = new Command("disable", "Disable a key in existing NuGet.config (comment out)");
        var disableKeyOption = new Option<string>(
            name: "--key",
            description: "The key to disable in NuGet.config"
        );
        disableCommand.AddOption(disableKeyOption);
        disableCommand.SetHandler((string key) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            if (!manager.KeyExists(key))
            {
                Console.WriteLine($"Key '{key}' not found in NuGet.config.");
                return;
            }

            manager.DisableKey(key);
            manager.SaveConfig();
            Console.WriteLine($"Disabled key '{key}' in NuGet.config successfully!");
        }, disableKeyOption);
        rootCommand.AddCommand(disableCommand);

        // Enable command
        var enableCommand = new Command("enable", "Enable a key in existing NuGet.config (uncomment)");
        var enableKeyOption = new Option<string>(
            name: "--key",
            description: "The key to enable in NuGet.config"
        );
        enableCommand.AddOption(enableKeyOption);
        enableCommand.SetHandler((string key) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            manager.EnableKey(key);
            manager.SaveConfig();
            Console.WriteLine($"Enabled key '{key}' in NuGet.config successfully!");
        }, enableKeyOption);
        rootCommand.AddCommand(enableCommand);
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine($"  nugetconfigcreator                    - Create standard config (default)");
        Console.WriteLine($"  nugetconfigcreator {_appSettings!.NuGetFeeds.Local.Command,-10} - Create config with local feed");
        Console.WriteLine($"  nugetconfigcreator {_appSettings!.NuGetFeeds.MyGet.Command,-10} - Create config with MyGet feed");
        Console.WriteLine();
        Console.WriteLine("Management Commands:");
        Console.WriteLine("  nugetconfigcreator remove --key <key>  - Remove a key from existing config");
        Console.WriteLine("  nugetconfigcreator disable --key <key> - Disable a key (comment out)");
        Console.WriteLine("  nugetconfigcreator enable --key <key>  - Enable a key (uncomment)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine($"  --path <path>  - Specify local feed path (default: {_appSettings!.NuGetFeeds.Local.DefaultPath})");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Edit appsettings.json in the tool directory to customize feed URLs and commands");
        Console.WriteLine("  You can add new feeds by adding them to the NuGetFeeds section");
    }

    private static void LoadConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
    }
}
