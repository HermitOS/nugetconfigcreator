using System.Xml.Linq;
using NugetConfigCreator.Configuration;

namespace NugetConfigCreator.Templates;

public abstract class NuGetConfigTemplate
{
    protected readonly NuGetFeedsConfig _feedsConfig;
    
    protected NuGetConfigTemplate(NuGetFeedsConfig feedsConfig)
    {
        _feedsConfig = feedsConfig;
    }
    
    public abstract string GenerateConfig();
    
    protected XDocument CreateBaseConfig()
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("add",
                        new XAttribute("key", _feedsConfig.NuGetOrg.Key),
                        new XAttribute("value", _feedsConfig.NuGetOrg.Url),
                        new XAttribute("protocolVersion", _feedsConfig.NuGetOrg.ProtocolVersion ?? "3")
                    )
                )
            )
        );
    }
}

public class StandardNuGetConfigTemplate : NuGetConfigTemplate
{
    public StandardNuGetConfigTemplate(NuGetFeedsConfig feedsConfig) : base(feedsConfig)
    {
    }
    
    public override string GenerateConfig()
    {
        var config = CreateBaseConfig();
        return config.ToString();
    }
}

public class LocalFeedNuGetConfigTemplate : NuGetConfigTemplate
{
    private readonly string _localFeedPath;
    
    public LocalFeedNuGetConfigTemplate(NuGetFeedsConfig feedsConfig, string localFeedPath) : base(feedsConfig)
    {
        _localFeedPath = localFeedPath;
    }
    
    public override string GenerateConfig()
    {
        var config = CreateBaseConfig();
        var packageSources = config.Root?.Element("packageSources");
        
        packageSources?.Add(
            new XElement("add",
                new XAttribute("key", _feedsConfig.Local.Key),
                new XAttribute("value", _localFeedPath)
            )
        );
        
        return config.ToString();
    }
}

public class MyGetNuGetConfigTemplate : NuGetConfigTemplate
{
    public MyGetNuGetConfigTemplate(NuGetFeedsConfig feedsConfig) : base(feedsConfig)
    {
    }
    
    public override string GenerateConfig()
    {
        var config = CreateBaseConfig();
        var packageSources = config.Root?.Element("packageSources");
        
        packageSources?.Add(
            new XElement("add",
                new XAttribute("key", _feedsConfig.MyGet.Key),
                new XAttribute("value", _feedsConfig.MyGet.Url)
            )
        );
        
        return config.ToString();
    }
}
