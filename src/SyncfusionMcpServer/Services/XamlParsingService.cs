using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;

namespace SyncfusionMcpServer.Services;

/// <summary>
/// Service for parsing and analyzing XAML files
/// </summary>
public class XamlParsingService
{
    private readonly ILogger<XamlParsingService> _logger;
    private static readonly XNamespace SyncfusionNs = "using:Syncfusion.UI.Xaml.DataGrid";
    private static readonly XNamespace SyncfusionChartsNs = "using:Syncfusion.UI.Xaml.Charts";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    public XamlParsingService(ILogger<XamlParsingService> logger)
    {
        _logger = logger;
    }

    public async Task<XamlParsingResult> ParseXamlFileAsync(string xamlPath, bool validateBindings, bool checkNamespaces)
    {
        var result = new XamlParsingResult { IsValid = true };

        try
        {
            if (!File.Exists(xamlPath))
            {
                result.IsValid = false;
                result.Errors.Add($"XAML file not found: {xamlPath}");
                return result;
            }

            var content = await File.ReadAllTextAsync(xamlPath);
            var doc = XDocument.Parse(content);

            // Extract components
            result.ComponentsFound = ExtractSyncfusionComponents(doc);
            result.TotalElements = doc.Descendants().Count();

            // Check namespaces
            if (checkNamespaces)
            {
                var namespaceIssues = ValidateNamespaces(doc);
                result.NamespaceIssues.AddRange(namespaceIssues);
            }

            // Extract binding paths
            if (validateBindings)
            {
                result.BindingPaths = ExtractBindingPaths(doc);
            }

            _logger.LogInformation("Parsed XAML file: {Path}, found {Count} Syncfusion components",
                xamlPath, result.ComponentsFound.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing XAML file: {Path}", xamlPath);
            result.IsValid = false;
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }

    private List<ComponentDescriptor> ExtractSyncfusionComponents(XDocument doc)
    {
        var components = new List<ComponentDescriptor>();
        var syncfusionPrefixes = new[] { "sf", "syncfusion", "charts", "editors", "grid" };

        foreach (var element in doc.Descendants())
        {
            var elementName = element.Name.LocalName;
            var namespaceName = element.Name.NamespaceName;

            // Check if it's a Syncfusion component
            bool isSyncfusion = syncfusionPrefixes.Any(prefix =>
                elementName.StartsWith("Sf", StringComparison.OrdinalIgnoreCase)) ||
                namespaceName.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase);

            if (isSyncfusion)
            {
                var component = new ComponentDescriptor
                {
                    Type = elementName,
                    Namespace = namespaceName,
                    Properties = element.Attributes()
                        .Where(a => a.Name.NamespaceName != XamlNs.NamespaceName)
                        .Select(a => a.Name.LocalName)
                        .ToList(),
                    Name = element.Attribute(XamlNs + "Name")?.Value,
                    LineNumber = ((IXmlLineInfo)element).LineNumber
                };

                components.Add(component);
            }
        }

        return components;
    }

    private List<string> ValidateNamespaces(XDocument doc)
    {
        var issues = new List<string>();
        var root = doc.Root;

        if (root == null)
        {
            return issues;
        }

        // Check for common Syncfusion namespace declarations
        var syncfusionNamespaces = root.Attributes()
            .Where(a => a.Value.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (syncfusionNamespaces.Count == 0)
        {
            issues.Add("No Syncfusion namespace declarations found");
        }

        // Check for proper xmlns declarations
        var requiredNamespaces = new Dictionary<string, string>
        {
            ["grid"] = "using:Syncfusion.UI.Xaml.DataGrid",
            ["chart"] = "using:Syncfusion.UI.Xaml.Charts",
            ["editors"] = "using:Syncfusion.UI.Xaml.Editors"
        };

        foreach (var ns in requiredNamespaces)
        {
            if (!syncfusionNamespaces.Any(a => a.Value.Contains(ns.Value)))
            {
                // Only warn if we find components that need this namespace
                var needsNamespace = doc.Descendants()
                    .Any(e => e.Name.LocalName.StartsWith("Sf", StringComparison.OrdinalIgnoreCase));

                if (needsNamespace)
                {
                    issues.Add($"Missing recommended namespace: xmlns:{ns.Key}=\"{ns.Value}\"");
                }
            }
        }

        return issues;
    }

    private List<string> ExtractBindingPaths(XDocument doc)
    {
        var bindings = new HashSet<string>();

        foreach (var element in doc.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                var value = attr.Value;
                if (value.Contains("{Binding", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract binding path
                    var pathMatch = System.Text.RegularExpressions.Regex.Match(
                        value, @"Path=([^,}]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (pathMatch.Success)
                    {
                        bindings.Add(pathMatch.Groups[1].Value.Trim());
                    }
                    else if (value.Contains("{Binding ") && !value.Contains("Path="))
                    {
                        // Simple binding like {Binding PropertyName}
                        var simpleMatch = System.Text.RegularExpressions.Regex.Match(
                            value, @"\{Binding\s+([^,}]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (simpleMatch.Success)
                        {
                            bindings.Add(simpleMatch.Groups[1].Value.Trim());
                        }
                    }
                }
            }
        }

        return bindings.ToList();
    }
}
