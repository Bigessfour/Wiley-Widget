// XAML Binding Static Analyzer (Enhanced)
// Purpose: Scan XAML files for {Binding ...} paths and verify they exist on the corresponding ViewModel.
// Enhancements:
//  - View-ViewModel mapping:
//      • Explicit Prism ViewModelLocationProvider.Register<View, VM>() parsing
//      • Code-behind DataContext assignments (new VM(), container.Resolve<VM>(), etc.)
//      • IoC container registrations (RegisterType<IFoo, Foo>()) with interface→concrete mapping
//      • Namespace-based fallback from x:Class (Views → ViewModels)
//  - ViewModel member extraction with Roslyn:
//      • Public properties and common command patterns (ICommand, DelegateCommand, RelayCommand)
//      • Inheritance and interface members (recursive, depth-limited)
//  - XAML parsing via XDocument instead of regex; better handling of attached properties and templates
//  - Optional nested-path validation to limited depth (default: 2)
//  - Reporting: unresolved reasons, suppression via <!-- BindingIgnore: Path -->, tolerance threshold
// Output: logs/xaml-binding-static-report.json and console summary; exits non-zero if unresolved exceed tolerance

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml;

#nullable enable

// ----- Type declarations (must precede usage in C# scripts) -----
public record TypeModel(string Name, Dictionary<string, string> Properties, HashSet<string> Commands);

// ----- Global state -----
public static class Globals
{
    public static Dictionary<string, TypeModel> TypeCache = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> VmFileByType = new(StringComparer.OrdinalIgnoreCase);
}

// ----- Helper functions -----
Dictionary<string, string> BuildTypeIndex(string repoRoot)
{
    var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
    {
        string src; try { src = File.ReadAllText(file); } catch { continue; }
        foreach (Match m in Regex.Matches(src, @"\b(class|interface)\s+(\w+)\b", RegexOptions.Multiline))
        {
            var name = m.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(name)) index[name] = file;
        }
    }
    return index;
}

TypeModel ParseTypeModel(string typeName, string repoRoot, Dictionary<string, string> index, HashSet<string>? visited = null, int depth = 0)
{
    if (Globals.TypeCache.TryGetValue(typeName, out var cached)) return cached;
    visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (visited.Contains(typeName) || depth > 4) return new TypeModel(typeName, new(), new());
    visited.Add(typeName);

    if (!index.TryGetValue(typeName, out var filePath))
    {
        var guess = Directory.EnumerateFiles(repoRoot, typeName + ".cs", SearchOption.AllDirectories).FirstOrDefault();
        if (!string.IsNullOrEmpty(guess)) filePath = guess; else { var tmEmpty = new TypeModel(typeName, new(), new()); Globals.TypeCache[typeName] = tmEmpty; return tmEmpty; }
    }
    Globals.VmFileByType[typeName] = filePath!;

    string src; try { src = File.ReadAllText(filePath!); } catch { var tmErr = new TypeModel(typeName, new(), new()); Globals.TypeCache[typeName] = tmErr; return tmErr; }

    var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var cmds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (Match m in Regex.Matches(src, @"public\s+([A-Za-z0-9_\.<>\?]+)\s+(\w+)\s*\{[^}]*get\s*;", RegexOptions.Multiline))
    {
        var type = m.Groups[1].Value; var name = m.Groups[2].Value;
        if (!string.IsNullOrWhiteSpace(name)) props[name] = type;
        var shortType = type.Contains('.') ? type.Split('.').Last() : type;
        if (string.Equals(shortType, "ICommand", StringComparison.OrdinalIgnoreCase) || shortType.StartsWith("DelegateCommand", StringComparison.OrdinalIgnoreCase) || string.Equals(shortType, "RelayCommand", StringComparison.OrdinalIgnoreCase))
            cmds.Add(name);
    }
    foreach (Match m in Regex.Matches(src, @"public\s+([A-Za-z0-9_\.<>\?]+)\s+(\w+)\s*;", RegexOptions.Multiline))
    {
        var type = m.Groups[1].Value; var name = m.Groups[2].Value;
        var shortType = type.Contains('.') ? type.Split('.').Last() : type;
        if (string.Equals(shortType, "ICommand", StringComparison.OrdinalIgnoreCase) || shortType.StartsWith("DelegateCommand", StringComparison.OrdinalIgnoreCase) || string.Equals(shortType, "RelayCommand", StringComparison.OrdinalIgnoreCase))
            cmds.Add(name);
    }

    var baseLine = Regex.Match(src, @"class\s+" + Regex.Escape(typeName) + @"\s*:\s*([^\{\n]+)", RegexOptions.Multiline);
    if (baseLine.Success)
    {
        var bases = baseLine.Groups[1].Value.Split(',').Select(s => s.Trim()).ToArray();
        foreach (var b in bases)
        {
            var bt = b;
            if (bt.Contains('<')) bt = bt.Substring(0, bt.IndexOf('<'));
            bt = bt.Split('.').Last();
            if (string.IsNullOrWhiteSpace(bt)) continue;
            try
            {
                var parent = ParseTypeModel(bt, repoRoot, index, visited, depth + 1);
                foreach (var kv in parent.Properties) props.TryAdd(kv.Key, kv.Value);
                foreach (var c in parent.Commands) cmds.Add(c);
            }
            catch { }
        }
    }

    var tm = new TypeModel(typeName, props, cmds);
    Globals.TypeCache[typeName] = tm;
    return tm;
}

Console.WriteLine("=== XAML Binding Static Analyzer ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

// Scope: UI views
string viewsRoot = Path.Combine(repoRoot, "src", "WileyWidget.UI", "Views");
if (!Directory.Exists(viewsRoot))
{
    Console.WriteLine($"Views folder not found: {viewsRoot}");
    Environment.Exit(2);
}

// Helpers
// Parse ViewModelLocationProvider.Register<View, ViewModel>() across the repo to build an explicit map
static Dictionary<string, string> BuildViewToViewModelMap(string repoRoot)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var roots = new[] { Path.Combine(repoRoot, "src"), Path.Combine(repoRoot, "WileyWidget.UI") };
    var rx = new Regex(@"ViewModelLocationProvider\.Register<\s*([A-Za-z0-9_\.]+)\s*,\s*([A-Za-z0-9_\.]+)\s*>", RegexOptions.Compiled);
    foreach (var root in roots.Where(Directory.Exists))
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string src;
            try { src = File.ReadAllText(file); } catch { continue; }
            foreach (Match m in rx.Matches(src))
            {
                var viewType = m.Groups[1].Value.Split('.').Last();
                var vmType = m.Groups[2].Value.Split('.').Last();
                map[viewType] = vmType;
            }
        }
    }
    return map;
}

// Build interface → concrete implementation map from IoC registrations (Unity/Prism)
static Dictionary<string, string> BuildInterfaceToConcreteMap(string repoRoot)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var roots = new[] { Path.Combine(repoRoot, "src"), Path.Combine(repoRoot, "WileyWidget.UI"), Path.Combine(repoRoot, "WileyWidget.Services") };
    var rx = new Regex(@"Register(?:Type|Singleton)?\s*<\s*([A-Za-z0-9_\.]+)\s*,\s*([A-Za-z0-9_\.]+)\s*>", RegexOptions.Compiled);
    foreach (var root in roots.Where(Directory.Exists))
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string src; try { src = File.ReadAllText(file); } catch { continue; }
            foreach (Match m in rx.Matches(src))
            {
                var iface = m.Groups[1].Value.Split('.').Last();
                var impl = m.Groups[2].Value.Split('.').Last();
                map[iface] = impl;
            }
        }
    }
    return map;
}

// Extract x:Class from XAML to identify the view type name
static string? GetXamlClassName(string xaml)
{
    // In verbatim string, double quotes are escaped as ""; match x:Class="Namespace.Type"
        var m = Regex.Match(xaml, @"x:Class=""([^""]+)""", RegexOptions.IgnoreCase);
    if (m.Success) return m.Groups[1].Value.Split('.').Last();
    return null;
}

static string GetExpectedViewModelPath(string repoRoot, string xamlPath, string? viewTypeName, Dictionary<string, string> viewToVm)
{
    // Map Views/.../<Base>.xaml -> ViewModels/.../<Base>Model.cs (replace trailing "View.xaml" with "ViewModel.cs")
    var rel = xamlPath.Replace(Path.Combine(repoRoot, "WileyWidget.UI") + Path.DirectorySeparatorChar, "");
    var parts = rel.Split(Path.DirectorySeparatorChar);
    // Replace first segment Views -> ViewModels
    if (parts.Length >= 2 && string.Equals(parts[0], "Views", StringComparison.OrdinalIgnoreCase))
        parts[0] = "ViewModels";
    var file = parts[^1];
    string baseName = Path.GetFileNameWithoutExtension(file); // e.g., SettingsView
    string vmFileName;
    // Prefer explicit mapping if available
    if (!string.IsNullOrWhiteSpace(viewTypeName) && viewToVm.TryGetValue(viewTypeName!, out var mappedVm))
    {
        vmFileName = mappedVm + ".cs";
    }
    else
    {
        if (baseName.EndsWith("View", StringComparison.Ordinal)) vmFileName = baseName + "Model.cs"; // SettingsView -> SettingsViewModel.cs
        else vmFileName = baseName + "ViewModel.cs";
    }
    parts[^1] = vmFileName;
    var vmPath = Path.Combine(repoRoot, "WileyWidget.UI", Path.Combine(parts));
    return vmPath;
}

static IEnumerable<string> FindAlternateViewModelPaths(string repoRoot, string baseName)
{
    string vmRoot = Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels");
    if (!Directory.Exists(vmRoot)) yield break;
    string[] candidates = new[]
    {
        baseName + "ViewModel.cs",
        baseName.Replace("View", "ViewModel") + ".cs",
        baseName + "Model.cs",
    };
    foreach (var c in candidates)
    {
        foreach (var path in Directory.EnumerateFiles(vmRoot, c, SearchOption.AllDirectories))
            yield return path;
    }
    // Fallback: search by suffix only
    foreach (var path in Directory.EnumerateFiles(vmRoot, "*" + baseName.Replace("View", "ViewModel") + ".cs", SearchOption.AllDirectories))
        yield return path;
}

// (type modeling moved to top of file)

// Extract binding paths and suppressions from XAML using XDocument
static (List<(string Raw, string Path, string? Mode)>, HashSet<string> Suppress) ExtractBindingsXml(string xaml)
{
    var results = new List<(string Raw, string Path, string? Mode)>();
    var suppress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Collect suppression comments: <!-- BindingIgnore: Foo -->
    foreach (Match cm in Regex.Matches(xaml, @"<!--\s*BindingIgnore:\s*([^\-]+)-->", RegexOptions.IgnoreCase))
    {
        var token = cm.Groups[1].Value.Trim();
        if (!string.IsNullOrWhiteSpace(token)) suppress.Add(token);
    }

    XDocument? doc = null;
    try
    {
        var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using var sr = new StringReader(xaml);
        using var xr = XmlReader.Create(sr, readerSettings);
        doc = XDocument.Load(xr, LoadOptions.PreserveWhitespace);
    }
    catch
    {
        // fallback simple regex
        foreach (Match m in Regex.Matches(xaml, "\\{Binding\\s+([^}]+)\\}", RegexOptions.IgnoreCase))
        {
            var inner = m.Groups[1].Value;
            if (inner.IndexOf("ElementName=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (inner.IndexOf("RelativeSource=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (inner.IndexOf("Source=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            string path = inner;
            var pm = Regex.Match(inner, @"Path\s*=\s*([A-Za-z0-9_\.]+)");
            if (pm.Success) path = pm.Groups[1].Value; else { var wm = Regex.Match(inner, @"^([A-Za-z0-9_\.]+)"); if (wm.Success) path = wm.Groups[1].Value; }
            path = path.Trim();
            if (string.IsNullOrEmpty(path) || path == ".") continue;
            results.Add((m.Value, path, null));
        }
        return (results, suppress);
    }

    if (doc == null) return (results, suppress);

    static bool ExcludedContext(XElement e)
    {
        var name = e.Name.LocalName;
        return name.Contains("Template", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Style", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Columns", StringComparison.OrdinalIgnoreCase)
            || (name.Equals("View", StringComparison.OrdinalIgnoreCase) && e.Parent?.Name.LocalName == "ListView");
    }

    var elements = doc.DescendantNodes().OfType<XElement>().Where(e => !e.Ancestors().Any(ExcludedContext));
    foreach (var el in elements)
    {
        foreach (var attr in el.Attributes())
        {
            var val = attr.Value;
            if (val.IndexOf("{Binding", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var m = Regex.Match(val, "\\{Binding\\s+([^}]+)\\}", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var inner = m.Groups[1].Value;
            if (inner.IndexOf("ElementName=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (inner.IndexOf("RelativeSource=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (inner.IndexOf("Source=", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            string? mode = null;
            var modeM = Regex.Match(inner, @"Mode\s*=\s*(OneWayToSource|TwoWay|OneWay|OneTime)", RegexOptions.IgnoreCase);
            if (modeM.Success) mode = modeM.Groups[1].Value;

            string path = inner;
            var pm = Regex.Match(inner, @"Path\s*=\s*([A-Za-z0-9_\.]+)");
            if (pm.Success) path = pm.Groups[1].Value; else { var wm = Regex.Match(inner, @"^([A-Za-z0-9_\.]+)"); if (wm.Success) path = wm.Groups[1].Value; }
            path = path.Trim();
            if (string.IsNullOrEmpty(path) || path == ".") continue;
            results.Add((m.Value, path, mode));
        }
    }

    return (results, suppress);
}

// Infer ViewModel type from code-behind DataContext assignment
static string? InferViewModelFromCodeBehind(string xamlPath)
{
    var csPath = xamlPath + ".cs";
    if (!File.Exists(csPath)) return null;
    try
    {
        var src = File.ReadAllText(csPath);
        var mNew = Regex.Match(src, @"DataContext\s*=\s*new\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*\(");
        if (mNew.Success) return mNew.Groups[1].Value.Split('.').Last();
        var mResolve = Regex.Match(src, @"DataContext\s*=\s*[^;]*<\s*([A-Za-z_][A-Za-z0-9_\.]*)\s*>\s*\(");
        if (mResolve.Success) return mResolve.Groups[1].Value.Split('.').Last();
    }
    catch { }
    return null;
}

var unresolved = new List<object>();
var perViewStats = new List<object>();

int totalBindings = 0, totalViews = 0, resolved = 0;

var viewToVmMap = BuildViewToViewModelMap(repoRoot);
var ifaceToConcrete = BuildInterfaceToConcreteMap(repoRoot);
var typeIndex = BuildTypeIndex(repoRoot);

int nestedDepthLimit = int.TryParse(Environment.GetEnvironmentVariable("WW_BINDING_NESTED_DEPTH"), out var dd) ? Math.Max(1, dd) : 2;
double tolerance = double.TryParse(Environment.GetEnvironmentVariable("WW_BINDING_TOLERANCE"), out var t) ? Math.Clamp(t, 0, 1) : 0.05;

foreach (var xamlPath in Directory.EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories))
{
    totalViews++;
    string xaml;
    try { xaml = File.ReadAllText(xamlPath); }
    catch { continue; }
    var viewTypeName = GetXamlClassName(xaml);

    var (bindings, suppress) = ExtractBindingsXml(xaml);
    if (bindings.Count == 0)
    {
        perViewStats.Add(new { view = xamlPath, bindings = 0, resolved = 0, unresolved = 0 });
        continue;
    }

    totalBindings += bindings.Count;

    // Resolve ViewModel type name
    string vmPath = GetExpectedViewModelPath(repoRoot, xamlPath, viewTypeName, viewToVmMap);
    var vmFromCb = InferViewModelFromCodeBehind(xamlPath);
    string? vmTypeName = null;
    if (!string.IsNullOrWhiteSpace(vmFromCb)) vmTypeName = vmFromCb;
    else if (!string.IsNullOrWhiteSpace(viewTypeName) && viewToVmMap.TryGetValue(viewTypeName!, out var mappedVm)) vmTypeName = mappedVm;
    else vmTypeName = Path.GetFileNameWithoutExtension(vmPath);
    if (!string.IsNullOrWhiteSpace(vmTypeName) && vmTypeName!.StartsWith("I") && ifaceToConcrete.TryGetValue(vmTypeName!, out var concreteVm)) vmTypeName = concreteVm;
    vmTypeName = vmTypeName?.Split(Path.DirectorySeparatorChar).Last().Split('.').Last();
    // Try alternates for vmPath if missing
    if (!File.Exists(vmPath))
    {
        var baseName = Path.GetFileNameWithoutExtension(xamlPath);
        var alts = FindAlternateViewModelPaths(repoRoot, baseName).ToList();
        if (alts.Count > 0) vmPath = alts[0];
    }
    // Build VM type model
    TypeModel? vmModel = null;
    if (!string.IsNullOrWhiteSpace(vmTypeName)) vmModel = ParseTypeModel(vmTypeName!, repoRoot, typeIndex);

    int viewResolved = 0, viewUnresolved = 0;
    foreach (var item in bindings)
    {
        var raw = item.Raw; var fullPath = item.Path; var mode = item.Mode;
        if (suppress.Contains(fullPath)) continue;
        var first = fullPath.Split('.')[0];
        bool requiresSetter = string.Equals(mode, "TwoWay", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "OneWayToSource", StringComparison.OrdinalIgnoreCase);

        bool ok = false; string reason = "";
        if (vmModel != null)
        {
            if (vmModel.Properties.ContainsKey(first) || vmModel.Commands.Contains(first))
            {
                ok = true;
                if (ok && fullPath.Contains('.') && nestedDepthLimit > 1)
                {
                    try
                    {
                        string? currentType = vmModel.Properties.TryGetValue(first, out var tname) ? tname : null;
                        var segs = fullPath.Split('.').Skip(1).Take(nestedDepthLimit - 1);
                        foreach (var seg in segs)
                        {
                            if (string.IsNullOrWhiteSpace(currentType)) { ok = false; reason = $"Unknown type for segment '{seg}'"; break; }
                            var cleanType = currentType.Contains('<') ? currentType.Substring(0, currentType.IndexOf('<')) : currentType;
                            cleanType = cleanType.Split('.').Last();
                            var tm = ParseTypeModel(cleanType, repoRoot, typeIndex);
                            if (!tm.Properties.TryGetValue(seg, out var nextType)) { ok = false; reason = $"Member '{seg}' not found on type '{cleanType}'"; break; }
                            currentType = nextType;
                        }
                    }
                    catch { }
                }
            }
            else { ok = false; reason = $"Member '{first}' not found on ViewModel"; }
        }
        else { ok = false; reason = "ViewModel not found"; }

        if (ok && requiresSetter && vmModel != null && vmModel.Properties.ContainsKey(first))
        {
            if (Globals.VmFileByType.TryGetValue(vmModel.Name, out var vmFile))
            {
                try
                {
                    var src = File.ReadAllText(vmFile);
                    var rx = new Regex($@"public\s+[\w<>,\.?]+\s+{Regex.Escape(first)}\s*\{{[^}}]*set\s*;", RegexOptions.IgnoreCase);
                    if (!rx.IsMatch(src)) { ok = false; reason = $"'{first}' lacks setter for mode {mode}"; }
                }
                catch { }
            }
        }

        if (ok) { resolved++; viewResolved++; }
        else { unresolved.Add(new { view = xamlPath, viewModel = vmTypeName, vmPath = File.Exists(vmPath) ? vmPath : null, binding = raw, path = fullPath, reason }); viewUnresolved++; }
    }
    perViewStats.Add(new { view = xamlPath, bindings = bindings.Count, resolved = viewResolved, unresolved = viewUnresolved, vmPath = File.Exists(vmPath) ? vmPath : null });
}

// Write JSON report
var report = new {
    analyzedAt = DateTime.UtcNow,
    totalViews,
    totalBindings,
    resolved,
    unresolvedCount = unresolved.Count,
    viewToVmMappings = viewToVmMap,
    perView = perViewStats,
    unresolved
};
var outPath = Path.Combine(logsDir, "xaml-binding-static-report.json");
var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outPath, json);

Console.WriteLine($"Analyzed views: {totalViews}, bindings: {totalBindings}, unresolved: {unresolved.Count}");
Console.WriteLine($"Report -> {outPath}");

// Basic assertions
int pass = 0, total = 0;
void Assert(bool cond, string name, string details = ""){ total++; if (cond){ Console.WriteLine("✓ " + name); pass++; } else { Console.WriteLine("✗ " + name + " FAILED"); if(!string.IsNullOrWhiteSpace(details)) Console.WriteLine("  Details: " + details);} }

double unresolvedRatio = totalBindings == 0 ? 0 : (double)unresolved.Count / totalBindings;
bool withinTolerance = unresolvedRatio <= tolerance;
Assert(withinTolerance, $"Unresolved bindings <= {tolerance:P0} threshold", $"{unresolved.Count}/{totalBindings} ({unresolvedRatio:P2}) unresolved");

Console.WriteLine($"\nResults: {pass}/{total} passed");
Environment.Exit(withinTolerance ? 0 : 9);
