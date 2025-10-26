# NuGet Config Creator 🚀

**The Ultimate Tool for Creating Perfect NuGet.config Files!**

Tired of manually crafting NuGet.config files? Sick of copying and pasting the same configuration over and over again? Look no further! **NuGet Config Creator** is here to revolutionize how you manage your NuGet package sources!

## 🎯 **Why You'll Love This Tool**

This isn't just another boring configuration generator - this is a **game-changer** for .NET developers! Whether you're working solo or managing enterprise-level projects, this tool will save you hours of tedious configuration work and eliminate those frustrating "where did I put that feed URL?" moments.

### ✨ **Key Features That Will Blow Your Mind**

🔥 **Zero-Friction Setup** - Create a perfect NuGet.config with a single command!  
🎨 **Fully Customizable** - Add unlimited feeds, rename commands, and make it YOURS!  
⚡ **Lightning Fast** - No more waiting around for complex setup processes!  
🛠️ **Developer-Friendly** - Clean, intuitive commands that just make sense!  
🔄 **Hot Reload** - Change your configuration and see results instantly!  
🎯 **Smart Defaults** - Comes with sensible configurations out of the box!  
🚀 **Extensible** - Add company feeds, dev environments, staging servers - the sky's the limit!  
🧠 **Smart Config Management** - Detects existing configs and prevents duplicates!  
🎭 **Enable/Disable Magic** - Temporarily disable feeds with XML commenting!  
🗑️ **Easy Cleanup** - Remove keys when you're done debugging!  

### 🚀 **Real-World Usage Scenarios**

**Debugging Workflow:**

```bash
nugetc add myget                # Add debug feed (preconfigured in appsettings.json)
# ... do your debugging magic ...
nugetc disable myget            # Temporarily disable
# ... continue development ...
nugetc enable myget             # Re-enable when needed
nugetc remove myget             # Clean up when done
```

**Team Development:**

```bash
nugetc add local --path "D:\TeamFeed"  # Add team feed
# ... collaborate with team ...
nugetc remove local                    # Clean up
```

**Smart Duplicate Prevention:**

```bash
nugetc add myget    # First time: "NuGet.config with MyGet.org feed created successfully!"
nugetc add myget    # Second time: "NuGet.config already exists and contains the 'myget' key."
```

### 🎉 **What You Get**

When you run this tool, you'll get a **beautifully formatted, production-ready NuGet.config** file that includes:

- ✅ **Official NuGet.org feed** with proper protocol versioning
- ✅ **Two extra feeds** one for a local feed at C:\nuget, and one for NUnit's myget feed. (Yeah, we love NUnit)
- ✅ **Custom feed sources** Add any feed you like, MyGet, Azure Devops, Github, local feeds, company reposits.
- ✅ **Clean XML structure** that follows Microsoft's best practices
- ✅ **Proper configuration** that works with all NuGet clients
- ✅ **No more typos** or missing attributes in your config files!
- ✅ **Smart overwrites** - You can update the paths of existing feeds!
- ✅ **Temporary disabling** - comment out feeds after debugging, then re-enable for debugging!
- ✅ **Easy cleanup** - remove keys when you're done with them!

**Seriously, this tool is so good, you'll wonder how you ever lived without it!** 🎊

## Installation

```bash
dotnet tool install --global nugetc
```

## Usage

After installation, you can use the tool with the following commands:

```bash
# Show help and available commands
nugetc --help

# View the README in your browser
nugetc readme

# Create or add a standard NuGet.config with only nuget.org feed
nugetc add default    # primary name (alternate: 'standard')
nugetc add standard   # alternate name

# Alternatively, the top-level 'create' command does the same as 'add default'
nugetc create

# Create or add a NuGet.config with local feed (default path: C:\nuget)
nugetc add local

# Create or add a NuGet.config with local feed at custom path
nugetc add local --path "D:\MyNuGetFeed"

# Note on --path and updates (local feed)
# - If NuGet.config already contains the 'local' feed, running with --path updates the existing path.
# - Running without --path preserves the current path (no change).
# - To start fresh, remove then add: `nugetc remove local` → `nugetc add local --path "D:\NewPath"`

# Create or add a NuGet.config with MyGet.org feed
nugetc add myget
```

### 🛠️ **Management Commands - The Game Changers!**

```bash
# Remove a feed from existing NuGet.config
nugetc remove local    # Remove local feed
nugetc remove myget    # Remove MyGet feed
nugetc remove default  # Remove nuget.org feed

# Temporarily disable a feed (comments it out)
nugetc disable local
nugetc disable myget
nugetc disable default

# Re-enable a disabled feed (uncomments it)
nugetc enable local
nugetc enable myget
nugetc enable default

# Validate configuration (check for collisions and invalid URLs)
nugetc config validate
```

## Configuration Types

1. **Standard (alias: default)**: Creates a basic NuGet.config with only the official nuget.org feed. Invoke with `nugetc add default` (or `nugetc add standard`).
2. **Local**: Adds a local feed source (default: C:\nuget, customizable via --path parameter)
3. **MyGet**: Adds MyGet.org feed source (NUnit feed)

### 🧠 **Smart Behavior**

- **First Run**: Creates a new NuGet.config with the specified feed
- **Subsequent Runs**: Adds the feed to existing config (if not already present)
- **Duplicate Detection**: Informs you if the key already exists

## Add more Custom feeds

You can add your own feeds, if three feeds are not enough for you.

E.g. a GitHub Packages feed:

```bash
# Add a custom feed to appsettings.json (name is used as command in lowercase)
nugetc config add GitHub --url "https://nuget.pkg.github.com/NAMESPACE/index.json"

# Then use it to add the feed to NuGet.config (restart the tool first)
nugetc add github
```

You can remove it from the tool configuration later:

```bash
nugetc config remove GitHub
```

Note: newly added commands appear on the next run because commands are generated from appsettings.json at startup.

You can list and rename custom feeds:

```bash
# List all configured feeds (built-in and custom)
nugetc config list

# Show details of a specific feed in JSON format
nugetc config show GitHub
nugetc config show local
nugetc config show default

# Validate configuration for collisions and invalid URLs
nugetc config validate

# Rename a custom feed
nugetc config rename GitHub GitHubEnterprise
```

## Configuration

The tool uses `appsettings.json` for configuration, making it easy to customize feed URLs without recompiling.

### Default Configuration

The tool comes with sensible defaults in `appsettings.json`:

```json
{
  "NuGetFeeds": {
    "NuGetOrg": {
      "Key": "nuget.org",
      "Command": "standard",
      "Url": "https://api.nuget.org/v3/index.json",
      "ProtocolVersion": "3"
    },
    "MyGet": {
      "Key": "MyGet",
      "Command": "myget",
      "Url": "https://www.myget.org/F/nunit/api/v3/index.json"
    },
    "Local": {
      "Key": "Local",
      "Command": "local",
      "DefaultPath": "C:\\nuget"
    }
  }
}
```

### Key Features

- **Command Flexibility**: The `Command` property defines the CLI command name
- **Easy Customization**: Add new feeds by adding entries to `NuGetFeeds`
- **User-Friendly Commands**: Choose command names that make sense for your organization
- **Multiple Feeds**: Add as many custom feeds as needed
- **Hot Reload**: Configuration changes take effect immediately

### Customizing Configuration

After installing the tool globally, you can customize the configuration:

1. **Find the tool directory**: `%USERPROFILE%\.dotnet\tools\`
2. **Edit `appsettings.json`**: Modify URLs, keys, or default paths
3. **Restart the tool**: Changes take effect immediately

### Example Customizations

**Add a custom MyGet feed:**

```json
"MyGet": {
  "Key": "MyCustomFeed",
  "Command": "custom",
  "Url": "https://www.myget.org/F/my-company/api/v3/index.json"
}
```

**Change command names:**

```json
"MyGet": {
  "Key": "MyGet",
  "Command": "nunit",
  "Url": "https://www.myget.org/F/nunit/api/v3/index.json"
}
```

**Add multiple custom feeds:**

```json
"CompanyFeed": {
  "Key": "Company",
  "Command": "company",
  "Url": "https://nuget.company.com/v3/index.json"
},
"DevFeed": {
  "Key": "Dev",
  "Command": "dev",
  "Url": "https://dev.nuget.company.com/v3/index.json"
}
```

**Change default local path:**

```json
"Local": {
  "Key": "Local",
  "Command": "local",
  "DefaultPath": "D:\\MyNuGetFeeds"
}
```

## 🎊 **Amazing Output - See the Magic Happen!**

When you run this incredible tool, it creates a **perfectly crafted `NuGet.config`** file right in your current directory! No more hunting through documentation or guessing at the right format - you get exactly what you need, when you need it!

### 🎯 **What Makes Our Output Special**

✨ **Instant Results** - Your NuGet.config appears in seconds, not minutes!  
🎨 **Beautiful Formatting** - Clean, readable XML that your team will love!  
🔧 **Production Ready** - No debugging needed, it just works!  
📋 **Consistent Structure** - Every file follows the same perfect pattern!  
🚀 **Zero Configuration Drift** - No more "it works on my machine" issues!  

**Pro Tip**: The tool only creates the `NuGet.config` file when you run it - no messy config files cluttering up your development environment! The example files are just there to show you what's possible. 🎉
