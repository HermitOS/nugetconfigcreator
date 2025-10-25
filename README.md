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
📦 **Multiple Formats** - Works as both a .NET tool AND a dotnet new template!  
🎯 **Smart Defaults** - Comes with sensible configurations out of the box!  
🚀 **Extensible** - Add company feeds, dev environments, staging servers - the sky's the limit!  
🧠 **Smart Config Management** - Detects existing configs and prevents duplicates!  
🎭 **Enable/Disable Magic** - Temporarily disable feeds with XML commenting!  
🗑️ **Easy Cleanup** - Remove keys when you're done debugging!  
💡 **No More Overwrites** - Preserves your existing configurations perfectly!  

### 🚀 **Real-World Usage Scenarios**

**Debugging Workflow:**

```bash
nugetc myget                    # Add debug feed, myget is in the config, so we know the url
# ... do your debugging magic ...
nugetc disable --key MyGet      # Temporarily disable
# ... continue development ...
nugetc enable --key MyGet       # Re-enable when needed
nugetc remove --key MyGet       # Clean up when done
```

**Team Development:**

```bash
nugetc local --path "D:\TeamFeed"  # Add team feed
# ... collaborate with team ...
nugetc remove --key Local          # Clean up
```

**Smart Duplicate Prevention:**

```bash
nugetc myget    # First time: "NuGet.config with MyGet.org feed created successfully!"
nugetc myget    # Second time: "NuGet.config already exists and contains the 'MyGet' key."
```

### 🎉 **What You Get**

When you run this tool, you'll get a **beautifully formatted, production-ready NuGet.config** file that includes:

- ✅ **Official NuGet.org feed** with proper protocol versioning
- ✅ **Two extra feeds** one for a local feed at C:\nuget, and one for NUnit's myget feed. (Yeah, we love NUnit)
- ✅ **Custom feed sources** Add any feed you like, MyGet, Azure Devops, Github, local feeds, company reposits.
- ✅ **Clean XML structure** that follows Microsoft's best practices
- ✅ **Proper configuration** that works with all NuGet clients
- ✅ **No more typos** or missing attributes in your config files!
- ✅ **Smart duplicate detection** - never accidentally overwrite existing configs!
- ✅ **Temporary disabling** - comment out feeds after debugging, then re-enable for debugging!
- ✅ **Easy cleanup** - remove keys when you're done with them!

**Seriously, this tool is so good, you'll wonder how you ever lived without it!** 🎊

## Installation

```bash
dotnet tool install --global nugetc
```

## Usage

### As a .NET Tool

After installation, you can use the tool with the following commands:

```bash
# Create a standard NuGet.config with only nuget.org feed (default)
nugetc

# Create or add to a NuGet.config with local feed (default path: C:\nuget) 
nugetc local

# Create or add to, or modify a NuGet.config with local feed at custom path
nugetc local --path "D:\MyNuGetFeed"

# Create or add to a NuGet.config with MyGet.org feed
nugetc myget
```

### 🛠️ **Management Commands - The Game Changers!**

```bash
# Remove a key from existing NuGet.config
nugetc remove --key MyGet

# Temporarily disable a key (comments it out)
nugetc disable --key MyGet

# Re-enable a disabled key (uncomments it)
nugetc enable --key MyGet
```

### As a dotnet new template

You can also use this as a `dotnet new` template:

```bash
# Install the template
dotnet new --install .

# Create NuGet.config files
dotnet new nugetc --configType standard
dotnet new nugetc --configType local --localPath "D:\MyNuGetFeed"
dotnet new nugetc --configType myget
```

## Configuration Types

1. **Standard**: Creates a basic NuGet.config with only the official nuget.org feed. This is the default, so you rarely need to specify standard.
2. **Local**: Adds a local feed source (default: C:\nuget, customizable via --path parameter)
3. **MyGet**: Adds MyGet.org feed source (NUnit feed)

### 🧠 **Smart Behavior**

- **First Run**: Creates a new NuGet.config with the specified feed
- **Subsequent Runs**: Adds the feed to existing config (if not already present)
- **Duplicate Detection**: Informs you if the key already exists
- **No Overwrites**: Always preserves your existing configuration

## Add more Custom feeds

You can add your own feeds, if three feeds are not enough for you.

E.g. a github package feed:

```bash
nugetc add github --path "https://nuget.pkg.github.com/NAMESPACE/index.json"
```

and then you can use the command

```bash
nugetc github
```

and add that feed to your nuget.config files ! 

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
