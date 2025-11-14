using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

var repoRoot = Directory.GetCurrentDirectory();
// Allow running from /app in the container: repoRoot should map to workspace root
var xmlnsDeclRegex2 = new Regex(@"xmlns:(?<prefix>[a-zA-Z_][\w-]*)\s*=\s*""(?<value>[^""\\]+)""", RegexOptions.Compiled);
var relativePaths = new[] {
    Path.Combine("src","WileyWidget.WinUI","Resources","DataTemplates.xaml"),
    Path.Combine("src","WileyWidget.WinUI","Resources","Strings.xaml"),
    Path.Combine("src","WileyWidget.WinUI","Resources","Generic.xaml")
};

// Additional files we'll validate
var additionalPaths = new[] {
    Path.Combine("src","WileyWidget.WinUI","App.xaml"),
    Path.Combine("src","WileyWidget.WinUI","App.xaml.cs"),
    Path.Combine("src","WileyWidget.WinUI","Behaviors","FrameRegionAdapter.cs")
};

var failures = new List<string>();

Console.WriteLine($"Validating {relativePaths.Length} WinUI resource files in: {repoRoot}");

void ValidateResourceFile(string rel, string text)
{
    if(!xmlnsXRegex.IsMatch(text)){
        failures.Add($"{rel}: missing xmlns:x declaration");
    }

    if(!syncfusionUsingRegex.IsMatch(text)){
        Console.WriteLine($"{rel}: note: syncfusion 'using:' namespace not found — that may be fine if not used.");
    }

    if(text.Contains("pack://application:")){
        failures.Add($"{rel}: contains WPF pack URI (pack://application:) — convert to ms-appx:///Resources/...");
    }

    // collect declared prefixes
    var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(Match m in xmlnsDeclRegex2.Matches(text)){
        var p = m.Groups["prefix"].Value;
        declared.Add(p);
    }

    // collect used prefixes
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(Match m in prefixUseRegex.Matches(text)){
        var p = m.Groups["prefix"].Value;
        // ignore known intrinsic prefixes
        if(p == "x" || p == "syncfusion" || p == "sys" || p == "system" || p == "prism" || p == "xmlns" || p == "using" || p == "clr-namespace" || p == "pack" || p == "component") continue;
        used.Add(p);
    }

    var missingPrefixes = used.Except(declared, StringComparer.OrdinalIgnoreCase).ToList();
    if(missingPrefixes.Any()){
        failures.Add($"{rel}: used namespace prefixes not declared: {string.Join(", ", missingPrefixes)}");
    }

    // file-specific content checks
    if(rel.EndsWith("Strings.xaml", StringComparison.OrdinalIgnoreCase)){
        if(!text.Contains("MunicipalAccounts_AccessibleName")) failures.Add($"{rel}: expected key 'MunicipalAccounts_AccessibleName' not found");
        if(!text.Contains("AppTitle")) failures.Add($"{rel}: expected key 'AppTitle' not found");
    }
    if(rel.EndsWith("Generic.xaml", StringComparison.OrdinalIgnoreCase)){
        if(!text.Contains("ContentBackgroundBrush")) failures.Add($"{rel}: expected key 'ContentBackgroundBrush' not found");
        if(!text.Contains("CardBackgroundBrush")) failures.Add($"{rel}: expected key 'CardBackgroundBrush' not found");
    }
    if(rel.EndsWith("DataTemplates.xaml", StringComparison.OrdinalIgnoreCase)){
        if(!text.Contains("DataTemplate")) failures.Add($"{rel}: DataTemplate entries appear missing");
        // Ensure DataTemplate has vm: and views: prefixes used
        if(!text.Contains("vm:") && !text.Contains("views:")) failures.Add($"{rel}: expected vm: or views: namespace usage in DataTemplates");
    }
}


// helper regexes
// var xmlnsDeclRegex = new Regex(@"xmlns:(?<prefix>[a-zA-Z_][\w-]*)\s*=\s*\""(?<value>[^\"]+)\""", RegexOptions.Compiled);
var prefixUseRegex = new Regex(@"<(?<prefix>[a-zA-Z_][\w-]*):[A-Za-z_]", RegexOptions.Compiled);
var xmlnsXRegex = new Regex(@"xmlns:x\s*=\s*""http://schemas.microsoft.com/winfx/2006/xaml""", RegexOptions.Compiled);
var syncfusionUsingRegex = new Regex(@"xmlns:syncfusion\s*=\s*""using:Syncfusion.UI.Xaml.Grids""", RegexOptions.Compiled);

foreach(var rel in relativePaths){
    var full = Path.GetFullPath(Path.Combine(repoRoot, rel));
    Console.WriteLine($"\n-- Checking: {rel}");
    if(!File.Exists(full)){
        failures.Add($"Missing file: {rel}");
        continue;
    }

    string text;
    try{ text = File.ReadAllText(full); }
    catch(Exception ex){ failures.Add($"Failed read {rel}: {ex.Message}"); continue; }

    ValidateResourceFile(rel, text);
}

// Additional validations for App.xaml, App.xaml.cs and FrameRegionAdapter.cs
foreach(var rel in additionalPaths){
    var full = Path.GetFullPath(Path.Combine(repoRoot, rel));
    Console.WriteLine($"\n-- Checking: {rel}");
    if(!File.Exists(full)){
        failures.Add($"Missing file: {rel}");
        continue;
    }

    string text;
    try{ text = File.ReadAllText(full); }
    catch(Exception ex){ failures.Add($"Failed read {rel}: {ex.Message}"); continue; }

    if(rel.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase)){
        // Confirm the merge entries exist
        if(!text.Contains("ms-appx:///Resources/Generic.xaml")) failures.Add($"App.xaml: missing Generic.xaml merge entry");
        if(!text.Contains("ms-appx:///Resources/Strings.xaml")) failures.Add($"App.xaml: missing Strings.xaml merge entry");
        if(!text.Contains("ms-appx:///Resources/DataTemplates.xaml")) failures.Add($"App.xaml: missing DataTemplates.xaml merge entry");
    }

    if(rel.EndsWith("App.xaml.cs", StringComparison.OrdinalIgnoreCase)){
        // Check for key registrations mentioned in the PR description
        if(!text.Contains("RegisterSingleton<IDbContextFactory<AppDbContext>" ) && !text.Contains("AppDbContextFactory")) failures.Add($"App.xaml.cs: expected AppDbContextFactory or IDbContextFactory registration");
        if(!text.Contains("Register<IMunicipalAccountRepository") && !text.Contains("Register<IBudgetRepository" )) failures.Add($"App.xaml.cs: expected repository registrations for IMunicipalAccountRepository and IBudgetRepository");
        if(!text.Contains("RegisterSingleton<IQuickBooksService") && !text.Contains("IQuickBooksService")) failures.Add($"App.xaml.cs: IQuickBooksService registration missing");
        if(!text.Contains("RegisterForNavigation<SettingsView") && !text.Contains("SettingsView, SettingsViewModel")) failures.Add($"App.xaml.cs: View navigation registration missing (SettingsView)");
    }

    if(rel.EndsWith("FrameRegionAdapter.cs", StringComparison.OrdinalIgnoreCase)){
        if(!text.Contains("RegionAdapterBase<Frame>")) failures.Add($"FrameRegionAdapter.cs: class must inherit RegionAdapterBase<Frame>");
        if(!text.Contains("AllActiveRegion")) failures.Add($"FrameRegionAdapter.cs: CreateRegion must return AllActiveRegion");
    }
}


Console.WriteLine();
if(failures.Any()){
    Console.WriteLine("VALIDATION: FAIL\n");
    foreach(var f in failures) Console.WriteLine(" - " + f);
    Console.WriteLine();
    Console.WriteLine("Advice: fix namespace declarations (xmlns:prefix=...) or convert pack URIs to ms-appx:///Resources/ path. See repository's WinUI Resources folder.");
    Environment.Exit(2);
} else {
    Console.WriteLine("VALIDATION: PASS — all basic checks OK.");
    Environment.Exit(0);
}
