using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
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

    var rootCommand = new RootCommand("NuGet Config Creator - Generate and manage NuGet.config feeds");

        // Create commands dynamically from configuration
        CreateDynamicCommands(rootCommand);

        // No parameters: show help (no side effects)
        if (args.Length == 0)
        {
            return await rootCommand.InvokeAsync("--help");
        }

        return await rootCommand.InvokeAsync(args);
    }

    private static void CreateDynamicCommands(RootCommand rootCommand)
    {
        // Parent 'add' command for adding feeds
        var addCommand = new Command("add", "Add a feed to NuGet.config (creates file if missing)");

        // Standard (nuget.org) subcommand
        var standardCommand = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Add nuget.org feed (or create standard config if missing)");
        // Alias 'default' for convenience: `nugetc add default`
        standardCommand.AddAlias("default");

        // Define the standard handler once so we can reuse it for aliases and the 'create' command
        Action standardHandler = () =>
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
        };

        standardCommand.SetHandler(standardHandler);
        addCommand.AddCommand(standardCommand);

        // Local feed command
    var localCommand = new Command(_appSettings!.NuGetFeeds.Local.Command, "Add local feed (or create config with local feed)");
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
    addCommand.AddCommand(localCommand);

        // MyGet command
    var mygetCommand = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Add MyGet.org feed (or create config with MyGet)");
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
    addCommand.AddCommand(mygetCommand);

    // Register 'add' at root
    rootCommand.AddCommand(addCommand);

    // Top-level 'create' command that mirrors 'add default'
    var createCommand = new Command("create", "Create a standard NuGet.config (same as 'add default')");
    createCommand.SetHandler(standardHandler);
    rootCommand.AddCommand(createCommand);

        // Dynamically add commands for any custom feeds defined in appsettings.json
        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kvp in _appSettings!.NuGetFeeds.Custom)
            {
                var customName = kvp.Key; // logical name
                var feed = kvp.Value;
                if (string.IsNullOrWhiteSpace(feed.Command) || string.IsNullOrWhiteSpace(feed.Url) || string.IsNullOrWhiteSpace(feed.Key))
                {
                    continue; // skip incomplete entries
                }

                // Avoid collisions with built-ins
                var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    _appSettings!.NuGetFeeds.NuGetOrg.Command,
                    _appSettings!.NuGetFeeds.Local.Command,
                    _appSettings!.NuGetFeeds.MyGet.Command,
                    "add", "remove", "disable", "enable", "create", "config"
                };
                if (reserved.Contains(feed.Command))
                    continue;

                var customCommand = new Command(feed.Command, $"Add {customName} feed (from configuration)");
                customCommand.SetHandler(() =>
                {
                    var manager = new NuGetConfigManager();
                    if (manager.ConfigExists)
                    {
                        if (manager.KeyExists(feed.Key))
                        {
                            Console.WriteLine($"NuGet.config already exists and contains the '{feed.Key}' key.");
                            return;
                        }
                        manager.AddOrUpdateKey(feed.Key, feed.Url, feed.ProtocolVersion);
                        manager.SaveConfig();
                        Console.WriteLine($"Added '{feed.Key}' key to existing NuGet.config!");
                    }
                    else
                    {
                        // Create base with NuGet.org, then add custom
                        var template = new StandardNuGetConfigTemplate(_appSettings!.NuGetFeeds);
                        var configXml = template.GenerateConfig();
                        File.WriteAllText("NuGet.config", configXml);
                        var manager2 = new NuGetConfigManager();
                        manager2.AddOrUpdateKey(feed.Key, feed.Url, feed.ProtocolVersion);
                        manager2.SaveConfig();
                        Console.WriteLine($"NuGet.config with {customName} feed created successfully!");
                    }
                });
                addCommand.AddCommand(customCommand);
            }
        }

        // 'config' command group for managing appsettings.json custom feeds
        var configCommand = new Command("config", "Manage tool configuration (appsettings.json)");
        var configAdd = new Command("add", "Add or update a custom feed in appsettings.json");
        var nameOpt = new Option<string>("--name", description: "Feed name (e.g., GitHub) - also used as command (lowercase)") { IsRequired = true };
        var urlOpt = new Option<string>("--url", description: "Feed URL (e.g., https://nuget.pkg.github.com/ORG/index.json)") { IsRequired = true };
        var protoOpt = new Option<string?>("--protocol-version", () => null, description: "Optional protocol version (e.g., 3)");
        configAdd.AddOption(nameOpt);
        configAdd.AddOption(urlOpt);
        configAdd.AddOption(protoOpt);
        configAdd.SetHandler((string name, string url, string? protocol) =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);
            settings.NuGetFeeds.Custom ??= new();

            // Command is derived from name (lowercase)
            var command = name.ToLowerInvariant();

            // Prevent collisions with built-in commands
            var builtins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                settings.NuGetFeeds.NuGetOrg.Command,
                settings.NuGetFeeds.Local.Command,
                settings.NuGetFeeds.MyGet.Command,
                "add", "remove", "disable", "enable", "create", "config", "default", "standard"
            };
            if (builtins.Contains(command))
            {
                Console.WriteLine($"Name '{name}' conflicts with a built-in command. Choose another.");
                return;
            }

            settings.NuGetFeeds.Custom[name] = new NuGetFeedConfig
            {
                Key = name.ToLowerInvariant(),
                Command = command,
                Url = url,
                ProtocolVersion = protocol
            };
            SaveAppSettingsToFile(path, settings);
            Console.WriteLine($"Added/updated custom feed '{name}' with command '{command}'. Restart the tool to use 'nugetc add {command}'.");
        }, nameOpt, urlOpt, protoOpt);

        var configRemove = new Command("remove", "Remove a custom feed from appsettings.json");
        var removeNameOpt = new Option<string>("--name", description: "Feed name to remove") { IsRequired = true };
        configRemove.AddOption(removeNameOpt);
        configRemove.SetHandler((string name) =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);
            if (settings.NuGetFeeds.Custom == null || settings.NuGetFeeds.Custom.Count == 0)
            {
                Console.WriteLine("No custom feeds configured.");
                return;
            }

            if (!settings.NuGetFeeds.Custom.ContainsKey(name))
            {
                Console.WriteLine($"Custom feed '{name}' not found.");
                return;
            }

            settings.NuGetFeeds.Custom.Remove(name);
            SaveAppSettingsToFile(path, settings);
            Console.WriteLine($"Removed custom feed '{name}'. Restart the tool for changes to take effect.");
        }, removeNameOpt);

        configCommand.AddCommand(configAdd);
        configCommand.AddCommand(configRemove);
        
        // List custom feeds
        var configList = new Command("list", "List configured feeds (built-in and custom)");
        configList.SetHandler(() =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);

            Console.WriteLine("Built-in feeds:");
            Console.WriteLine($"  standard/default -> key='{settings.NuGetFeeds.NuGetOrg.Key}', cmd='{settings.NuGetFeeds.NuGetOrg.Command}', url='{settings.NuGetFeeds.NuGetOrg.Url}'");
            Console.WriteLine($"  local            -> key='{settings.NuGetFeeds.Local.Key}', cmd='{settings.NuGetFeeds.Local.Command}', defaultPath='{settings.NuGetFeeds.Local.DefaultPath}'");
            Console.WriteLine($"  myget            -> key='{settings.NuGetFeeds.MyGet.Key}', cmd='{settings.NuGetFeeds.MyGet.Command}', url='{settings.NuGetFeeds.MyGet.Url}'");
            Console.WriteLine();

            Console.WriteLine("Custom feeds:");
            if (settings.NuGetFeeds.Custom != null && settings.NuGetFeeds.Custom.Count > 0)
            {
                foreach (var kv in settings.NuGetFeeds.Custom)
                {
                    var f = kv.Value;
                    Console.WriteLine($"  {kv.Key} -> key='{f.Key}', cmd='{f.Command}', url='{f.Url}'");
                }
            }
            else
            {
                Console.WriteLine("  (none)");
            }
        });
        configCommand.AddCommand(configList);

        // Rename custom feed
        var configRename = new Command("rename", "Rename a custom feed");
        var renameOldNameArg = new Argument<string>("current-name", description: "Existing feed name");
        var renameNewNameArg = new Argument<string>("new-name", description: "New feed name");
        configRename.AddArgument(renameOldNameArg);
        configRename.AddArgument(renameNewNameArg);
        configRename.SetHandler((string oldName, string newName) =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);
            if (settings.NuGetFeeds.Custom == null || settings.NuGetFeeds.Custom.Count == 0)
            {
                Console.WriteLine("No custom feeds configured.");
                return;
            }

            if (!settings.NuGetFeeds.Custom.ContainsKey(oldName))
            {
                Console.WriteLine($"Custom feed '{oldName}' not found.");
                return;
            }

            var newCommand = newName.ToLowerInvariant();
            var builtinCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                settings.NuGetFeeds.NuGetOrg.Command,
                settings.NuGetFeeds.Local.Command,
                settings.NuGetFeeds.MyGet.Command,
                "add", "remove", "disable", "enable", "create", "config", "default", "standard"
            };

            if (builtinCommands.Contains(newCommand))
            {
                Console.WriteLine($"Cannot rename: name '{newName}' conflicts with a built-in command.");
                return;
            }

            if (settings.NuGetFeeds.Custom.ContainsKey(newName))
            {
                Console.WriteLine($"Cannot rename: name '{newName}' already exists.");
                return;
            }

            var feed = settings.NuGetFeeds.Custom[oldName];
            settings.NuGetFeeds.Custom.Remove(oldName);
            feed.Key = newCommand;
            feed.Command = newCommand;
            settings.NuGetFeeds.Custom[newName] = feed;

            SaveAppSettingsToFile(path, settings);
            Console.WriteLine($"Renamed custom feed from '{oldName}' to '{newName}' (command: '{newCommand}'). Restart the tool for changes to take effect.");
        }, renameOldNameArg, renameNewNameArg);

        configCommand.AddCommand(configRename);

        // Show details of a single feed
    var configShow = new Command("show", "Show details of a specific feed in JSON format");
    var showNameArg = new Argument<string>("name", description: "Feed name to show");
    configShow.AddArgument(showNameArg);
        configShow.SetHandler((string name) =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);

            NuGetFeedConfig? feedToShow = null;
            string feedType = "";

            if (string.Equals(name, settings.NuGetFeeds.NuGetOrg.Key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, settings.NuGetFeeds.NuGetOrg.Command, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
            {
                feedToShow = settings.NuGetFeeds.NuGetOrg;
                feedType = "Built-in (NuGet.org)";
            }
            else if (string.Equals(name, settings.NuGetFeeds.MyGet.Key, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, settings.NuGetFeeds.MyGet.Command, StringComparison.OrdinalIgnoreCase))
            {
                feedToShow = settings.NuGetFeeds.MyGet;
                feedType = "Built-in (MyGet)";
            }
            else if (string.Equals(name, settings.NuGetFeeds.Local.Key, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, settings.NuGetFeeds.Local.Command, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Feed: {settings.NuGetFeeds.Local.Key} (Built-in - Local)");
                Console.WriteLine(JsonSerializer.Serialize(settings.NuGetFeeds.Local, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            else if (settings.NuGetFeeds.Custom?.ContainsKey(name) == true)
            {
                feedToShow = settings.NuGetFeeds.Custom[name];
                feedType = "Custom";
            }

            if (feedToShow == null)
            {
                Console.WriteLine($"Feed '{name}' not found.");
                return;
            }

            Console.WriteLine($"Feed: {feedToShow.Key} ({feedType})");
            Console.WriteLine(JsonSerializer.Serialize(feedToShow, new JsonSerializerOptions { WriteIndented = true }));
    }, showNameArg);
        configCommand.AddCommand(configShow);

        // Validate configuration for collisions and issues
        var configValidate = new Command("validate", "Validate configuration for collisions and invalid URLs");
        configValidate.SetHandler(() =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);

            Console.WriteLine("Validating configuration...");
            Console.WriteLine();

            var issues = new List<string>();
            var builtinCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                settings.NuGetFeeds.NuGetOrg.Command,
                settings.NuGetFeeds.Local.Command,
                settings.NuGetFeeds.MyGet.Command,
                "add", "remove", "disable", "enable", "create", "config", "default", "standard"
            };

            var builtinKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                settings.NuGetFeeds.NuGetOrg.Key,
                settings.NuGetFeeds.Local.Key,
                settings.NuGetFeeds.MyGet.Key
            };

            // Validate built-in feeds
            if (string.IsNullOrWhiteSpace(settings.NuGetFeeds.NuGetOrg.Url))
                issues.Add("Built-in NuGet.org feed has empty URL");
            if (string.IsNullOrWhiteSpace(settings.NuGetFeeds.MyGet.Url))
                issues.Add("Built-in MyGet feed has empty URL");

            // Validate custom feeds
            if (settings.NuGetFeeds.Custom != null && settings.NuGetFeeds.Custom.Count > 0)
            {
                var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in settings.NuGetFeeds.Custom)
                {
                    var feed = kv.Value;
                    var feedName = kv.Key;

                    // Check for empty fields
                    if (string.IsNullOrWhiteSpace(feed.Command))
                        issues.Add($"Custom feed '{feedName}' has empty command");
                    if (string.IsNullOrWhiteSpace(feed.Key))
                        issues.Add($"Custom feed '{feedName}' has empty key");
                    if (string.IsNullOrWhiteSpace(feed.Url))
                        issues.Add($"Custom feed '{feedName}' has empty URL");

                    // Check for collisions with built-ins
                    if (!string.IsNullOrWhiteSpace(feed.Command) && builtinCommands.Contains(feed.Command))
                        issues.Add($"Custom feed '{feedName}' command '{feed.Command}' conflicts with built-in/reserved command");
                    if (!string.IsNullOrWhiteSpace(feed.Key) && builtinKeys.Contains(feed.Key))
                        issues.Add($"Custom feed '{feedName}' key '{feed.Key}' conflicts with built-in key");

                    // Check for duplicate commands/keys among custom feeds
                    if (!string.IsNullOrWhiteSpace(feed.Command))
                    {
                        if (seenCommands.Contains(feed.Command))
                            issues.Add($"Duplicate command '{feed.Command}' found in custom feeds");
                        else
                            seenCommands.Add(feed.Command);
                    }

                    if (!string.IsNullOrWhiteSpace(feed.Key))
                    {
                        if (seenKeys.Contains(feed.Key))
                            issues.Add($"Duplicate key '{feed.Key}' found in custom feeds");
                        else
                            seenKeys.Add(feed.Key);
                    }

                    // Basic URL validation
                    if (!string.IsNullOrWhiteSpace(feed.Url))
                    {
                        if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out var uri))
                            issues.Add($"Custom feed '{feedName}' has invalid URL: {feed.Url}");
                        else if (uri.Scheme != "https" && uri.Scheme != "http" && uri.Scheme != "file")
                            issues.Add($"Custom feed '{feedName}' URL has unsupported scheme: {uri.Scheme}");
                    }
                }
            }

            if (issues.Count == 0)
            {
                Console.WriteLine("✅ Configuration is valid! No issues found.");
            }
            else
            {
                Console.WriteLine($"❌ Found {issues.Count} issue(s):");
                Console.WriteLine();
                foreach (var issue in issues)
                {
                    Console.WriteLine($"  • {issue}");
                }
            }
        });
        configCommand.AddCommand(configValidate);

        rootCommand.AddCommand(configCommand);

        // Remove command (subcommands like 'add')
        var removeCommand = new Command("remove", "Remove a feed from existing NuGet.config");

        // remove standard/default
        var removeStandard = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Remove nuget.org feed");
        removeStandard.AddAlias("default");
        removeStandard.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.NuGetOrg.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' not found in NuGet.config.");
                return;
            }
            manager.RemoveKey(_appSettings!.NuGetFeeds.NuGetOrg.Key);
            manager.SaveConfig();
            Console.WriteLine($"Removed key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' from NuGet.config successfully!");
        });
        removeCommand.AddCommand(removeStandard);

        // remove local
        var removeLocal = new Command(_appSettings!.NuGetFeeds.Local.Command, "Remove local feed");
        removeLocal.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.Local.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.Local.Key}' not found in NuGet.config.");
                return;
            }
            manager.RemoveKey(_appSettings!.NuGetFeeds.Local.Key);
            manager.SaveConfig();
            Console.WriteLine($"Removed key '{_appSettings!.NuGetFeeds.Local.Key}' from NuGet.config successfully!");
        });
        removeCommand.AddCommand(removeLocal);

        // remove myget
        var removeMyGet = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Remove MyGet feed");
        removeMyGet.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.MyGet.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.MyGet.Key}' not found in NuGet.config.");
                return;
            }
            manager.RemoveKey(_appSettings!.NuGetFeeds.MyGet.Key);
            manager.SaveConfig();
            Console.WriteLine($"Removed key '{_appSettings!.NuGetFeeds.MyGet.Key}' from NuGet.config successfully!");
        });
        removeCommand.AddCommand(removeMyGet);

        // remove custom feeds
        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kv in _appSettings!.NuGetFeeds.Custom)
            {
                var customFeed = kv.Value;
                if (string.IsNullOrWhiteSpace(customFeed.Command) || string.IsNullOrWhiteSpace(customFeed.Key))
                    continue;

                var removeCustom = new Command(customFeed.Command, $"Remove custom feed '{kv.Key}'");
                removeCustom.SetHandler(() =>
                {
                    var manager = new NuGetConfigManager();
                    if (!manager.ConfigExists)
                    {
                        Console.WriteLine("No NuGet.config file found.");
                        return;
                    }
                    if (!manager.KeyExists(customFeed.Key))
                    {
                        Console.WriteLine($"Key '{customFeed.Key}' not found in NuGet.config.");
                        return;
                    }
                    manager.RemoveKey(customFeed.Key);
                    manager.SaveConfig();
                    Console.WriteLine($"Removed key '{customFeed.Key}' from NuGet.config successfully!");
                });
                removeCommand.AddCommand(removeCustom);
            }
        }

        rootCommand.AddCommand(removeCommand);

        // Disable command (subcommands like 'add')
        var disableCommand = new Command("disable", "Disable a feed in existing NuGet.config (comment out)");

        var disableStandard = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Disable nuget.org feed");
        disableStandard.AddAlias("default");
        disableStandard.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.NuGetOrg.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' not found in NuGet.config.");
                return;
            }
            manager.DisableKey(_appSettings!.NuGetFeeds.NuGetOrg.Key);
            manager.SaveConfig();
            Console.WriteLine($"Disabled key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' in NuGet.config successfully!");
        });
        disableCommand.AddCommand(disableStandard);

        var disableLocal = new Command(_appSettings!.NuGetFeeds.Local.Command, "Disable local feed");
        disableLocal.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.Local.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.Local.Key}' not found in NuGet.config.");
                return;
            }
            manager.DisableKey(_appSettings!.NuGetFeeds.Local.Key);
            manager.SaveConfig();
            Console.WriteLine($"Disabled key '{_appSettings!.NuGetFeeds.Local.Key}' in NuGet.config successfully!");
        });
        disableCommand.AddCommand(disableLocal);

        var disableMyGet = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Disable MyGet feed");
        disableMyGet.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.MyGet.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.MyGet.Key}' not found in NuGet.config.");
                return;
            }
            manager.DisableKey(_appSettings!.NuGetFeeds.MyGet.Key);
            manager.SaveConfig();
            Console.WriteLine($"Disabled key '{_appSettings!.NuGetFeeds.MyGet.Key}' in NuGet.config successfully!");
        });
        disableCommand.AddCommand(disableMyGet);

        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kv in _appSettings!.NuGetFeeds.Custom)
            {
                var feed = kv.Value;
                if (string.IsNullOrWhiteSpace(feed.Command) || string.IsNullOrWhiteSpace(feed.Key))
                    continue;
                var disableCustom = new Command(feed.Command, $"Disable custom feed '{kv.Key}'");
                disableCustom.SetHandler(() =>
                {
                    var manager = new NuGetConfigManager();
                    if (!manager.ConfigExists)
                    {
                        Console.WriteLine("No NuGet.config file found.");
                        return;
                    }
                    if (!manager.KeyExists(feed.Key))
                    {
                        Console.WriteLine($"Key '{feed.Key}' not found in NuGet.config.");
                        return;
                    }
                    manager.DisableKey(feed.Key);
                    manager.SaveConfig();
                    Console.WriteLine($"Disabled key '{feed.Key}' in NuGet.config successfully!");
                });
                disableCommand.AddCommand(disableCustom);
            }
        }

        rootCommand.AddCommand(disableCommand);

        // Enable command (subcommands like 'add')
        var enableCommand = new Command("enable", "Enable a feed in existing NuGet.config (uncomment)");

        var enableStandard = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Enable nuget.org feed");
        enableStandard.AddAlias("default");
        enableStandard.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            manager.EnableKey(_appSettings!.NuGetFeeds.NuGetOrg.Key);
            manager.SaveConfig();
            Console.WriteLine($"Enabled key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' in NuGet.config successfully!");
        });
        enableCommand.AddCommand(enableStandard);

        var enableLocal = new Command(_appSettings!.NuGetFeeds.Local.Command, "Enable local feed");
        enableLocal.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            manager.EnableKey(_appSettings!.NuGetFeeds.Local.Key);
            manager.SaveConfig();
            Console.WriteLine($"Enabled key '{_appSettings!.NuGetFeeds.Local.Key}' in NuGet.config successfully!");
        });
        enableCommand.AddCommand(enableLocal);

        var enableMyGet = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Enable MyGet feed");
        enableMyGet.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            manager.EnableKey(_appSettings!.NuGetFeeds.MyGet.Key);
            manager.SaveConfig();
            Console.WriteLine($"Enabled key '{_appSettings!.NuGetFeeds.MyGet.Key}' in NuGet.config successfully!");
        });
        enableCommand.AddCommand(enableMyGet);

        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kv in _appSettings!.NuGetFeeds.Custom)
            {
                var feed = kv.Value;
                if (string.IsNullOrWhiteSpace(feed.Command) || string.IsNullOrWhiteSpace(feed.Key))
                    continue;
                var enableCustom = new Command(feed.Command, $"Enable custom feed '{kv.Key}'");
                enableCustom.SetHandler(() =>
                {
                    var manager = new NuGetConfigManager();
                    if (!manager.ConfigExists)
                    {
                        Console.WriteLine("No NuGet.config file found.");
                        return;
                    }
                    manager.EnableKey(feed.Key);
                    manager.SaveConfig();
                    Console.WriteLine($"Enabled key '{feed.Key}' in NuGet.config successfully!");
                });
                enableCommand.AddCommand(enableCustom);
            }
        }

        rootCommand.AddCommand(enableCommand);
    }

    private static void ShowUsage()
    {
        // Kept for reference; help is now shown by default with no args via System.CommandLine
        Console.WriteLine("Usage:");
    Console.WriteLine($"  nugetc add {_appSettings!.NuGetFeeds.NuGetOrg.Command,-10} - Add nuget.org feed / create standard config (alias: 'default')");
    Console.WriteLine($"  nugetc create               - Create standard config (same as 'add default')");
        Console.WriteLine($"  nugetc add {_appSettings!.NuGetFeeds.Local.Command,-10} - Add local feed (--path optional)");
        Console.WriteLine($"  nugetc add {_appSettings!.NuGetFeeds.MyGet.Command,-10} - Add MyGet feed");
        Console.WriteLine();
        Console.WriteLine("Management Commands:");
    Console.WriteLine("  nugetc remove <feed>  - Remove a feed (e.g., local, myget, default)");
    Console.WriteLine("  nugetc disable <feed> - Disable a feed (comment out)");
    Console.WriteLine("  nugetc enable <feed>  - Enable a feed (uncomment)");
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
        // Before loading, check for a backup file and offer restore if structures match but content differs
        var configPath = GetAppSettingsPath();
        var backupPath = configPath + ".backup";

        if (File.Exists(backupPath) && File.Exists(configPath))
        {
            if (TryLoadAppSettingsFromFile(configPath, out var current) && TryLoadAppSettingsFromFile(backupPath, out var backup))
            {
                if (!AreAppSettingsEquivalent(current, backup))
                {
                    Console.Write($"A backup configuration was found (appsettings.json.backup) that differs from the current configuration. Restore backup? (Y/N): ");
                    var answer = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(answer) && (answer.StartsWith("Y", StringComparison.OrdinalIgnoreCase)))
                    {
                        File.Copy(backupPath, configPath, overwrite: true);
                        Console.WriteLine("Backup restored to appsettings.json.");
                    }
                }
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
    }

    private static bool TryLoadAppSettingsFromFile(string path, out AppSettings settings)
    {
        try
        {
            if (!File.Exists(path))
            {
                settings = new AppSettings();
                return false;
            }
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            return true;
        }
        catch
        {
            settings = new AppSettings();
            return false;
        }
    }

    private static bool AreAppSettingsEquivalent(AppSettings a, AppSettings b)
    {
        if (a == null || b == null) return false;
        if (!FeedEquals(a.NuGetFeeds.NuGetOrg, b.NuGetFeeds.NuGetOrg)) return false;
        if (!FeedEquals(a.NuGetFeeds.MyGet, b.NuGetFeeds.MyGet)) return false;
        if (!LocalEquals(a.NuGetFeeds.Local, b.NuGetFeeds.Local)) return false;

        var aCustom = a.NuGetFeeds.Custom ?? new Dictionary<string, NuGetFeedConfig>();
        var bCustom = b.NuGetFeeds.Custom ?? new Dictionary<string, NuGetFeedConfig>();

        if (aCustom.Count != bCustom.Count) return false;
        foreach (var kv in aCustom)
        {
            if (!bCustom.TryGetValue(kv.Key, out var bf)) return false;
            if (!FeedEquals(kv.Value, bf)) return false;
        }
        return true;
    }

    private static bool FeedEquals(NuGetFeedConfig x, NuGetFeedConfig y)
    {
        return string.Equals(x.Key, y.Key, StringComparison.Ordinal)
            && string.Equals(x.Command, y.Command, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Url, y.Url, StringComparison.Ordinal)
            && string.Equals(x.ProtocolVersion ?? string.Empty, y.ProtocolVersion ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool LocalEquals(LocalFeedConfig x, LocalFeedConfig y)
    {
        return string.Equals(x.Key, y.Key, StringComparison.Ordinal)
            && string.Equals(x.Command, y.Command, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.DefaultPath, y.DefaultPath, StringComparison.Ordinal);
    }

    private static string GetAppSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static AppSettings LoadAppSettingsFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
    }

    private static void SaveAppSettingsToFile(string path, AppSettings settings)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(settings, options);
        // Save primary file
        File.WriteAllText(path, json);
        // Also save a backup alongside the primary file
        var backupPath = path + ".backup";
        File.WriteAllText(backupPath, json);
    }
}
