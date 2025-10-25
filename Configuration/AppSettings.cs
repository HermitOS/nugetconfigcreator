namespace NugetConfigCreator.Configuration;

public class NuGetFeedConfig
{
    public string Key { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ProtocolVersion { get; set; }
}

public class NuGetFeedsConfig
{
    public NuGetFeedConfig NuGetOrg { get; set; } = new();
    public NuGetFeedConfig MyGet { get; set; } = new();
    public LocalFeedConfig Local { get; set; } = new();
    // Additional custom feeds defined by users at runtime
    public Dictionary<string, NuGetFeedConfig> Custom { get; set; } = new();
}

public class LocalFeedConfig
{
    public string Key { get; set; } = "Local";
    public string Command { get; set; } = "local";
    public string DefaultPath { get; set; } = @"C:\nuget";
}

public class AppSettings
{
    public NuGetFeedsConfig NuGetFeeds { get; set; } = new();
}
