using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Diagnostics;
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
        // Ensure help shows the installed tool name, not the program class
        rootCommand.Name = "nugetc";

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
        // 'readme' command - open project README in default browser
        var readmeCommand = new Command("readme", "Open the project README in your browser");
        readmeCommand.SetHandler(() =>
        {
            var url = "https://github.com/HermitOS/nugetconfigcreator#readme";
            try
            {
                var psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
                Process.Start(psi);
                Console.WriteLine("See separate opened browser.");
            }
            catch
            {
                Console.WriteLine($"README: {url}");
            }
        });
        rootCommand.AddCommand(readmeCommand);

        // Parent 'add' command for adding feeds
        var addCommand = new Command("add", "Add a feed to NuGet.config, key only will pick from config, if with value (optionally) updates path/url, or if key doesn't exist, creates it.)");

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

    // Top-level 'reset' command - overwrite existing NuGet.config with standard (nuget.org only)
    var resetConfigCommand = new Command("reset", "Reset NuGet.config to only the standard nuget.org feed (if NuGet.config exists)");
    resetConfigCommand.SetHandler(() =>
    {
        var manager = new NuGetConfigManager();
        if (!manager.ConfigExists)
        {
            Console.WriteLine("No NuGet.config file found. No nuget.config to reset.");
            return;
        }

        var template = new StandardNuGetConfigTemplate(_appSettings!.NuGetFeeds);
        var config = template.GenerateConfig();
        File.WriteAllText("NuGet.config", config);
        Console.WriteLine("NuGet.config reset to standard configuration (nuget.org only).");
    });
    rootCommand.AddCommand(resetConfigCommand);

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

        // Add support for arbitrary key-value pairs (not in config)
        // Usage: nugetc add <key> <value>
        var addKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name");
        var addValueArg = new Argument<string?>("value", () => null, description: "Feed URL or path");
        addCommand.AddArgument(addKeyArg);
        addCommand.AddArgument(addValueArg);
        addCommand.SetHandler((string? key, string? value) =>
        {
            // Only handle if both key and value are provided (not a subcommand)
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return; // Let subcommands handle it
            }

            var manager = new NuGetConfigManager();
            if (manager.ConfigExists)
            {
                if (manager.KeyExists(key))
                {
                    Console.WriteLine($"NuGet.config already exists and contains the '{key}' key.");
                    return;
                }
                manager.AddOrUpdateKey(key.ToLowerInvariant(), value);
                manager.SaveConfig();
                Console.WriteLine($"Added '{key.ToLowerInvariant()}' key to existing NuGet.config!");
            }
            else
            {
                // Create base config with just this feed
                var template = new StandardNuGetConfigTemplate(_appSettings!.NuGetFeeds);
                var configXml = template.GenerateConfig();
                File.WriteAllText("NuGet.config", configXml);
                var manager2 = new NuGetConfigManager();
                manager2.AddOrUpdateKey(key.ToLowerInvariant(), value);
                manager2.SaveConfig();
                Console.WriteLine($"NuGet.config with '{key.ToLowerInvariant()}' feed created successfully!");
            }
        }, addKeyArg, addValueArg);

        // 'config' command group for managing appsettings.json custom feeds
        var configCommand = new Command("config", "Manage tool configuration (appsettings.json)");
    var configAdd = new Command("add", "Add or update a custom feed in appsettings.json");
    var nameArg = new Argument<string>("name", description: "Feed name (e.g., GitHub) - also used as command (lowercase)");
    var valueArg = new Argument<string>("value", description: "Feed URL or local path");
    var protoOpt = new Option<string?>("--protocol-version", () => null, description: "Optional protocol version (e.g., 3)");
    configAdd.AddArgument(nameArg);
    configAdd.AddArgument(valueArg);
    configAdd.AddOption(protoOpt);
    configAdd.SetHandler((string name, string value, string? protocol) =>
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
                Url = value,
                ProtocolVersion = protocol
            };
            SaveAppSettingsToFile(path, settings);
            Console.WriteLine($"Added/updated custom feed '{name}' with command '{command}'.");
    }, nameArg, valueArg, protoOpt);

    var configRemove = new Command("remove", "Remove a custom feed from appsettings.json");
    var removeNameArg = new Argument<string>("name", description: "Feed name to remove");
    configRemove.AddArgument(removeNameArg);
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
            Console.WriteLine($"Removed custom feed '{name}'.");
    }, removeNameArg);

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
            Console.WriteLine($"Renamed custom feed from '{oldName}' to '{newName}' (command: '{newCommand}').");
        }, renameOldNameArg, renameNewNameArg);

        configCommand.AddCommand(configRename);

        // Show feed details; without a name, show all configured feeds
    var configShow = new Command("show", "Show configured feeds; without a name, shows all");
    var showNameArg = new Argument<string?>("name", () => null, description: "Feed name to show");
    configShow.AddArgument(showNameArg);
        configShow.SetHandler((string? name) =>
        {
            var path = GetAppSettingsPath();
            var settings = LoadAppSettingsFromFile(path);

            if (string.IsNullOrWhiteSpace(name))
            {
                // Show all feeds (same format as 'config list')
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
                return;
            }

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

        // Reset configuration to factory defaults
        var configReset = new Command("reset", "Reset tool configuration: delete backup and restore factory default appsettings.json");
        configReset.SetHandler(() =>
        {
            var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nugetc");
            var backupPath = Path.Combine(userConfigDir, "appsettings.json.backup");
            var configPath = GetAppSettingsPath();
            var defaultPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json.default");

            // Delete backup if it exists
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Delete(backupPath);
                    Console.WriteLine($"Deleted backup: {backupPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not delete backup: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("No backup file to delete.");
            }

            // Restore factory default appsettings
            try
            {
                if (File.Exists(defaultPath))
                {
                    File.Copy(defaultPath, configPath, overwrite: true);
                    Console.WriteLine("Restored appsettings.json from factory default.");
                }
                else
                {
                    // Fallback: write a fresh default structure
                    var fresh = new AppSettings();
                    var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                    var json = JsonSerializer.Serialize(fresh, options);
                    File.WriteAllText(configPath, json);
                    Console.WriteLine("Factory default file not found; wrote a fresh default appsettings.json.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring factory defaults: {ex.Message}");
            }
        });
        configCommand.AddCommand(configReset);

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

        // Add support for arbitrary key removal (not necessarily in config)
        var removeKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name to remove");
        removeCommand.AddArgument(removeKeyArg);
        removeCommand.SetHandler((string? key) =>
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return; // Let subcommands handle it
            }

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
        }, removeKeyArg);

        rootCommand.AddCommand(removeCommand);

        // Update command (update existing feed URL/path in NuGet.config)
        var updateCommand = new Command("update", "Update an existing feed's URL or path in NuGet.config");

        var updateStandard = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Update nuget.org feed URL");
        updateStandard.AddAlias("default");
        var updateStandardArg = new Argument<string>("url", description: "New URL for nuget.org feed");
        updateStandard.AddArgument(updateStandardArg);
        updateStandard.SetHandler((string url) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.NuGetOrg.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' not found in NuGet.config. Use 'nugetc add default' first.");
                return;
            }
            manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.NuGetOrg.Key, url, _appSettings!.NuGetFeeds.NuGetOrg.ProtocolVersion);
            manager.SaveConfig();
            Console.WriteLine($"Updated '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' to URL: {url}");
        }, updateStandardArg);
        updateCommand.AddCommand(updateStandard);

        var updateLocal = new Command(_appSettings!.NuGetFeeds.Local.Command, "Update local feed path");
        var updateLocalArg = new Argument<string>("path", description: "New path for local feed");
        updateLocal.AddArgument(updateLocalArg);
        updateLocal.SetHandler((string path) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.Local.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.Local.Key}' not found in NuGet.config. Use 'nugetc add local' first.");
                return;
            }
            manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.Local.Key, path);
            manager.SaveConfig();
            Console.WriteLine($"Updated '{_appSettings!.NuGetFeeds.Local.Key}' to path: {path}");
        }, updateLocalArg);
        updateCommand.AddCommand(updateLocal);

        var updateMyGet = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Update MyGet feed URL");
        var updateMyGetArg = new Argument<string>("url", description: "New URL for MyGet feed");
        updateMyGet.AddArgument(updateMyGetArg);
        updateMyGet.SetHandler((string url) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(_appSettings!.NuGetFeeds.MyGet.Key))
            {
                Console.WriteLine($"Key '{_appSettings!.NuGetFeeds.MyGet.Key}' not found in NuGet.config. Use 'nugetc add myget' first.");
                return;
            }
            manager.AddOrUpdateKey(_appSettings!.NuGetFeeds.MyGet.Key, url);
            manager.SaveConfig();
            Console.WriteLine($"Updated '{_appSettings!.NuGetFeeds.MyGet.Key}' to URL: {url}");
        }, updateMyGetArg);
        updateCommand.AddCommand(updateMyGet);

        // Dynamic custom feeds
        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kv in _appSettings!.NuGetFeeds.Custom)
            {
                var customFeed = kv.Value;
                if (string.IsNullOrWhiteSpace(customFeed.Command) || string.IsNullOrWhiteSpace(customFeed.Key))
                    continue;

                var updateCustom = new Command(customFeed.Command, $"Update custom feed '{kv.Key}' URL/path");
                var updateCustomArg = new Argument<string>("value", description: "New URL or path for custom feed");
                updateCustom.AddArgument(updateCustomArg);
                updateCustom.SetHandler((string value) =>
                {
                    var manager = new NuGetConfigManager();
                    if (!manager.ConfigExists)
                    {
                        Console.WriteLine("No NuGet.config file found.");
                        return;
                    }
                    if (!manager.KeyExists(customFeed.Key))
                    {
                        Console.WriteLine($"Key '{customFeed.Key}' not found in NuGet.config. Use 'nugetc add {customFeed.Command}' first.");
                        return;
                    }
                    manager.AddOrUpdateKey(customFeed.Key, value, customFeed.ProtocolVersion);
                    manager.SaveConfig();
                    Console.WriteLine($"Updated '{customFeed.Key}' to: {value}");
                }, updateCustomArg);
                updateCommand.AddCommand(updateCustom);
            }
        }

        // Add support for arbitrary key update
        var updateKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name to update");
        var updateValueArg = new Argument<string?>("value", () => null, description: "New URL or path");
        updateCommand.AddArgument(updateKeyArg);
        updateCommand.AddArgument(updateValueArg);
        updateCommand.SetHandler((string? key, string? value) =>
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return; // Let subcommands handle it
            }

            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            if (!manager.KeyExists(key))
            {
                Console.WriteLine($"Key '{key}' not found in NuGet.config. Use 'nugetc add {key} \"{value}\"' to create it.");
                return;
            }
            manager.AddOrUpdateKey(key, value);
            manager.SaveConfig();
            Console.WriteLine($"Updated '{key}' to: {value}");
        }, updateKeyArg, updateValueArg);

        rootCommand.AddCommand(updateCommand);

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

        // Add support for arbitrary key disable
        var disableKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name to disable");
        disableCommand.AddArgument(disableKeyArg);
        disableCommand.SetHandler((string? key) =>
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return; // Let subcommands handle it
            }

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
        }, disableKeyArg);

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

        // Add support for arbitrary key enable
        var enableKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name to enable");
        enableCommand.AddArgument(enableKeyArg);
        enableCommand.SetHandler((string? key) =>
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return; // Let subcommands handle it
            }

            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }
            manager.EnableKey(key);
            manager.SaveConfig();
            Console.WriteLine($"Enabled key '{key}' in NuGet.config successfully!");
        }, enableKeyArg);

        rootCommand.AddCommand(enableCommand);

        // Show command
        var showCommand = new Command("show", "Show feed URL or path from NuGet.config");

        // Show without arguments shows all feeds
        showCommand.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            var feeds = manager.GetAllFeeds();
            if (feeds.Count == 0)
            {
                Console.WriteLine("No feeds found in NuGet.config.");
                return;
            }

            foreach (var feed in feeds)
            {
                Console.WriteLine($"{feed.Key}: {feed.Value}");
            }
        });

        // Show standard/default
        var showStandard = new Command(_appSettings!.NuGetFeeds.NuGetOrg.Command, "Show nuget.org feed");
        showStandard.AddAlias("default");
        showStandard.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            var value = manager.GetKeyValue(_appSettings!.NuGetFeeds.NuGetOrg.Key);
            if (value != null)
            {
                Console.WriteLine($"{_appSettings!.NuGetFeeds.NuGetOrg.Key}: {value}");
            }
            else
            {
                Console.WriteLine($"Feed '{_appSettings!.NuGetFeeds.NuGetOrg.Key}' not found in NuGet.config.");
            }
        });
        showCommand.AddCommand(showStandard);

        // Show local
        var showLocal = new Command(_appSettings!.NuGetFeeds.Local.Command, "Show local feed");
        showLocal.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            var value = manager.GetKeyValue(_appSettings!.NuGetFeeds.Local.Key);
            if (value != null)
            {
                Console.WriteLine($"{_appSettings!.NuGetFeeds.Local.Key}: {value}");
            }
            else
            {
                Console.WriteLine($"Feed '{_appSettings!.NuGetFeeds.Local.Key}' not found in NuGet.config.");
            }
        });
        showCommand.AddCommand(showLocal);

        // Show MyGet
        var showMyGet = new Command(_appSettings!.NuGetFeeds.MyGet.Command, "Show MyGet feed");
        showMyGet.SetHandler(() =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            var value = manager.GetKeyValue(_appSettings!.NuGetFeeds.MyGet.Key);
            if (value != null)
            {
                Console.WriteLine($"{_appSettings!.NuGetFeeds.MyGet.Key}: {value}");
            }
            else
            {
                Console.WriteLine($"Feed '{_appSettings!.NuGetFeeds.MyGet.Key}' not found in NuGet.config.");
            }
        });
        showCommand.AddCommand(showMyGet);

        // Add custom feeds
        if (_appSettings!.NuGetFeeds.Custom != null && _appSettings!.NuGetFeeds.Custom.Count > 0)
        {
            foreach (var kv in _appSettings!.NuGetFeeds.Custom)
            {
                var feed = kv.Value;
                if (string.IsNullOrWhiteSpace(feed.Command) || string.IsNullOrWhiteSpace(feed.Key))
                    continue;

                var showCustom = new Command(feed.Command, $"Show custom feed '{kv.Key}'");
                showCustom.SetHandler(() =>
                {
                    var manager = new NuGetConfigManager();
                    if (!manager.ConfigExists)
                    {
                        Console.WriteLine("No NuGet.config file found.");
                        return;
                    }

                    var value = manager.GetKeyValue(feed.Key);
                    if (value != null)
                    {
                        Console.WriteLine($"{feed.Key}: {value}");
                    }
                    else
                    {
                        Console.WriteLine($"Feed '{feed.Key}' not found in NuGet.config.");
                    }
                });
                showCommand.AddCommand(showCustom);
            }
        }

        // Add support for arbitrary key show
        var showKeyArg = new Argument<string?>("key", () => null, description: "Feed key/name to show");
        showCommand.AddArgument(showKeyArg);
        showCommand.SetHandler((string? key) =>
        {
            var manager = new NuGetConfigManager();
            if (!manager.ConfigExists)
            {
                Console.WriteLine("No NuGet.config file found.");
                return;
            }

            // If no key provided, show all feeds (already handled by default handler above)
            if (string.IsNullOrWhiteSpace(key))
            {
                var feeds = manager.GetAllFeeds();
                if (feeds.Count == 0)
                {
                    Console.WriteLine("No feeds found in NuGet.config.");
                    return;
                }

                foreach (var feed in feeds)
                {
                    Console.WriteLine($"{feed.Key}: {feed.Value}");
                }
                return;
            }

            // Show specific key
            var value = manager.GetKeyValue(key);
            if (value != null)
            {
                Console.WriteLine($"{key}: {value}");
            }
            else
            {
                Console.WriteLine($"Feed '{key}' not found in NuGet.config.");
            }
        }, showKeyArg);

        rootCommand.AddCommand(showCommand);
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
        // Use a version-independent location for backup and version flag
        var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nugetc");
        Directory.CreateDirectory(userConfigDir);
        
        var configPath = GetAppSettingsPath();
        // Ensure we preserve the factory defaults of the currently installed version
        // by capturing the shipped appsettings.json to appsettings.json.default before any edits.
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json.default");
        try
        {
            if (!File.Exists(defaultPath) && File.Exists(configPath))
            {
                File.Copy(configPath, defaultPath, overwrite: false);
            }
        }
        catch
        {
            // Non-fatal: default capture failure should not break normal execution
        }

        var backupPath = Path.Combine(userConfigDir, "appsettings.json.backup");
        var versionFlagPath = Path.Combine(userConfigDir, ".version-flag");
        var currentVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        bool isFirstRunAfterUpdate = false;
        if (File.Exists(versionFlagPath))
        {
            var lastVersion = File.ReadAllText(versionFlagPath).Trim();
            if (lastVersion != currentVersion)
            {
                isFirstRunAfterUpdate = true;
            }
        }
        else
        {
            // No flag file exists, assume first run
            isFirstRunAfterUpdate = true;
        }

        // Auto-restore from backup if this is first run after update and backup exists
        if (isFirstRunAfterUpdate && File.Exists(backupPath))
        {
            if (File.Exists(configPath))
            {
                // Compare to see if backup has custom feeds that current doesn't
                if (TryLoadAppSettingsFromFile(configPath, out var current) && 
                    TryLoadAppSettingsFromFile(backupPath, out var backup))
                {
                    var currentCustomCount = current.NuGetFeeds.Custom?.Count ?? 0;
                    var backupCustomCount = backup.NuGetFeeds.Custom?.Count ?? 0;
                    
                    // If backup has custom feeds and current doesn't, or they differ, restore
                    if (backupCustomCount > 0 || !AreAppSettingsEquivalent(current, backup))
                    {
                        File.Copy(backupPath, configPath, overwrite: true);
                        Console.WriteLine($"[nugetc] Restored custom feeds from backup (version {currentVersion})");
                    }
                }
            }
            else if (File.Exists(backupPath))
            {
                // Config missing but backup exists - restore it
                File.Copy(backupPath, configPath, overwrite: false);
                Console.WriteLine($"[nugetc] Restored configuration from backup (version {currentVersion})");
            }
        }

        // Update version flag
        File.WriteAllText(versionFlagPath, currentVersion);

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
        
        // Also ensure a one-time capture of factory defaults exists
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json.default");
        try
        {
            if (!File.Exists(defaultPath) && File.Exists(path))
            {
                // Capture whatever is currently on disk (pre-change) as factory default for this version
                // To do this safely, read from disk before our overwrite above:
                // However, we've already overwritten. If default doesn't exist by now, fall back to
                // capturing from our freshly written content to keep schema; startup also tries to capture early.
                File.WriteAllText(defaultPath, json);
            }
        }
        catch { /* best-effort only */ }
        
        // Save backup to version-independent user directory
        var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nugetc");
        Directory.CreateDirectory(userConfigDir);
        var backupPath = Path.Combine(userConfigDir, "appsettings.json.backup");
        File.WriteAllText(backupPath, json);
    }
}
