using System.Xml.Linq;

namespace NugetConfigCreator.Utilities;

public class NuGetConfigManager
{
    private readonly string _configPath;
    private XDocument? _document;

    public NuGetConfigManager(string configPath = "NuGet.config")
    {
        _configPath = configPath;
        LoadExistingConfig();
    }

    private void LoadExistingConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                _document = XDocument.Load(_configPath);
            }
            catch
            {
                _document = null;
            }
        }
    }

    public bool ConfigExists => File.Exists(_configPath);

    public bool KeyExists(string key)
    {
        if (_document == null) return false;
        
        return _document.Descendants("add")
            .Any(add => add.Attribute("key")?.Value == key);
    }

    public void AddOrUpdateKey(string key, string value, string? protocolVersion = null)
    {
        if (_document == null)
        {
            CreateNewConfig();
        }

        var packageSources = _document!.Root?.Element("packageSources");
        if (packageSources == null) return;

        // Remove existing key if it exists
        var existingAdd = packageSources.Elements("add")
            .FirstOrDefault(add => add.Attribute("key")?.Value == key);
        
        if (existingAdd != null)
        {
            existingAdd.Remove();
        }

        // Add new key
        var newAdd = new XElement("add", new XAttribute("key", key), new XAttribute("value", value));
        if (!string.IsNullOrEmpty(protocolVersion))
        {
            newAdd.Add(new XAttribute("protocolVersion", protocolVersion));
        }
        
        packageSources.Add(newAdd);
    }

    public void RemoveKey(string key)
    {
        if (_document == null) return;

        var packageSources = _document.Root?.Element("packageSources");
        if (packageSources == null) return;

        var existingAdd = packageSources.Elements("add")
            .FirstOrDefault(add => add.Attribute("key")?.Value == key);
        
        if (existingAdd != null)
        {
            existingAdd.Remove();
        }
    }

    public void DisableKey(string key)
    {
        if (_document == null) return;

        var packageSources = _document.Root?.Element("packageSources");
        if (packageSources == null) return;

        var existingAdd = packageSources.Elements("add")
            .FirstOrDefault(add => add.Attribute("key")?.Value == key);
        
        if (existingAdd != null)
        {
            // Check if already commented
            if (existingAdd.Parent?.NodeType == System.Xml.XmlNodeType.Comment)
            {
                return; // Already disabled
            }

            // Comment out the element
            var comment = new XComment(existingAdd.ToString());
            existingAdd.ReplaceWith(comment);
        }
    }

    public void EnableKey(string key)
    {
        if (_document == null) return;

        var packageSources = _document.Root?.Element("packageSources");
        if (packageSources == null) return;

        // Find commented out elements
        var comments = packageSources.Nodes()
            .OfType<XComment>()
            .ToList();

        foreach (var comment in comments)
        {
            try
            {
                var commentDoc = XDocument.Parse(comment.Value);
                var addElement = commentDoc.Root;
                if (addElement?.Attribute("key")?.Value == key)
                {
                    // Replace comment with actual element
                    comment.ReplaceWith(addElement);
                    break;
                }
            }
            catch
            {
                // Ignore malformed comments
            }
        }
    }

    public void SaveConfig()
    {
        if (_document == null) return;

        _document.Save(_configPath);
    }

    private void CreateNewConfig()
    {
        _document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration",
                new XElement("packageSources")
            )
        );
    }

    public string GetConfigContent()
    {
        return _document?.ToString() ?? string.Empty;
    }
}
