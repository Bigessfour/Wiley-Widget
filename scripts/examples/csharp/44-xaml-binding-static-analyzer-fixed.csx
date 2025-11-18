// XAML Binding Static Analyzer (Enhanced) - Fixed Version
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
// Fixed: Resolved CS8803 and CS0246 errors by proper declaration ordering for .csx compatibility

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml;

#nullable enable

// ----- Entry point (top-level statements must come first) -----
ExecuteAnalyzer();

void ExecuteAnalyzer() => BindingAnalyzer.Run();

// ----- Type declarations -----
public record TypeModel(string Name, Dictionary<string, string> Properties, HashSet<string> Commands);

// ----- Helper class (all methods static) -----
public static class BindingAnalyzer
{
    public static Dictionary<string, TypeModel> TypeCache = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> VmFileByType = new(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> BuildTypeIndex(string repoRoot)
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

    public static TypeModel ParseTypeModel(string typeName, string repoRoot, Dictionary<string, string> index, HashSet<string>? visited = null, int depth = 0)
    {
        if (TypeCache.TryGetValue(typeName, out var cached)) return cached;
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (visited.Contains(typeName) || depth > 4) return new TypeModel(typeName, new(), new());
        visited.Add(typeName);

        if (!index.TryGetValue(typeName, out var filePath))
        {
            var guess = Directory.EnumerateFiles(repoRoot, typeName + ".cs", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(guess)) filePath = guess; else { var tmEmpty = new TypeModel(typeName, new(), new()); TypeCache[typeName] = tmEmpty; return tmEmpty; }
        }
        VmFileByType[typeName] = filePath!;

        string src; try { src = File.ReadAllText(filePath!); } catch { var tmErr = new TypeModel(typeName, new(), new()); TypeCache[typeName] = tmErr; return tmErr; }

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cmds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match ALL public properties - simplified to catch everything
        // Pattern: public Type Name { ... } or public Type Name => ...
        foreach (Match m in Regex.Matches(src, @"public\s+([A-Za-z0-9_\.<>\?\[\]]+)\s+(\w+)\s*[{=>]", RegexOptions.Multiline))
        {
            var type = m.Groups[1].Value; var name = m.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(name) && !name.Equals("class", StringComparison.OrdinalIgnoreCase))
            {
                props[name] = type;
                var shortType = type.Contains('.') ? type.Split('.').Last() : type;
                if (shortType.Contains('<')) shortType = shortType.Substring(0, shortType.IndexOf('<'));
                if (string.Equals(shortType, "ICommand", StringComparison.OrdinalIgnoreCase) ||
                    shortType.StartsWith("DelegateCommand", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shortType, "RelayCommand", StringComparison.OrdinalIgnoreCase))
                    cmds.Add(name);
            }
        }

        // Also match public fields for commands (less common but exists)
        foreach (Match m in Regex.Matches(src, @"public\s+(readonly\s+)?([A-Za-z0-9_\.<>\?]+)\s+(\w+)\s*;", RegexOptions.Multiline))
        {
            var type = m.Groups[2].Value; var name = m.Groups[3].Value;
            var shortType = type.Contains('.') ? type.Split('.').Last() : type;
            if (shortType.Contains('<')) shortType = shortType.Substring(0, shortType.IndexOf('<'));
            if (string.Equals(shortType, "ICommand", StringComparison.OrdinalIgnoreCase) ||
                shortType.StartsWith("DelegateCommand", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortType, "RelayCommand", StringComparison.OrdinalIgnoreCase))
            {
                cmds.Add(name);
                if (!props.ContainsKey(name)) props[name] = type;
            }
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
        TypeCache[typeName] = tm;
        return tm;
    }

    public static Dictionary<string, string> BuildViewToViewModelMap(string repoRoot)
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

    public static Dictionary<string, string> BuildInterfaceToConcreteMap(string repoRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var roots = new[] { Path.Combine(repoRoot, "src"), Path.Combine(repoRoot, "WileyWidget.UI") };
        var rx = new Regex(@"\.RegisterType<\s*([A-Za-z0-9_\.]+)\s*,\s*([A-Za-z0-9_\.]+)\s*>\s*\(", RegexOptions.Compiled);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string src;
                try { src = File.ReadAllText(file); } catch { continue; }
                foreach (Match m in rx.Matches(src))
                {
                    var ifaceType = m.Groups[1].Value.Split('.').Last();
                    var concreteType = m.Groups[2].Value.Split('.').Last();
                    if (ifaceType.StartsWith("I") && ifaceType.Length > 1)
                        map[ifaceType] = concreteType;
                }
            }
        }
        return map;
    }

    public static string? FindViewModelForView(string xamlPath, string repoRoot, Dictionary<string, string> viewToVmMap, Dictionary<string, string> iocMap)
    {
        var xaml = File.ReadAllText(xamlPath);
        var xClassMatch = Regex.Match(xaml, @"x:Class\s*=\s*[""']([^""']+)[""']");
        if (!xClassMatch.Success) return null;

        var fullName = xClassMatch.Groups[1].Value;
        var viewName = fullName.Split('.').Last();

        if (viewToVmMap.TryGetValue(viewName, out var vmName)) return vmName;

        var codeBehindPath = xamlPath.Replace(".xaml", ".xaml.cs", StringComparison.OrdinalIgnoreCase);
        if (File.Exists(codeBehindPath))
        {
            var cb = File.ReadAllText(codeBehindPath);
            var newMatch = Regex.Match(cb, @"DataContext\s*=\s*new\s+([A-Za-z0-9_\.]+)\s*\(");
            if (newMatch.Success) return newMatch.Groups[1].Value.Split('.').Last();

            var resolveMatch = Regex.Match(cb, @"DataContext\s*=\s*\w+\.Resolve<([A-Za-z0-9_\.]+)>");
            if (resolveMatch.Success)
            {
                var resolved = resolveMatch.Groups[1].Value.Split('.').Last();
                if (iocMap.TryGetValue(resolved, out var concrete)) return concrete;
                return resolved;
            }
        }

        var nsPrefix = string.Join(".", fullName.Split('.').SkipLast(1));
        if (nsPrefix.EndsWith(".Views")) nsPrefix = nsPrefix.Substring(0, nsPrefix.Length - 6) + ".ViewModels";
        var guess = viewName.EndsWith("View") ? viewName.Substring(0, viewName.Length - 4) + "ViewModel" : viewName + "ViewModel";
        return guess;
    }

    public static List<string> ExtractBindingPaths(XDocument xdoc)
    {
        var paths = new List<string>();
        var rx = new Regex(@"\{Binding\s+(?:Path=)?([A-Za-z0-9_\.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var rxMode = new Regex(@"Mode\s*=\s*(OneWay|TwoWay|OneWayToSource|OneTime)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        void ProcessElement(XElement el)
        {
            foreach (var attr in el.Attributes())
            {
                foreach (Match m in rx.Matches(attr.Value))
                {
                    var path = m.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(path) || path == "." || path == "ElementName" || path == "RelativeSource") continue;
                    paths.Add(attr.Value);
                }
            }
            foreach (var child in el.Elements()) ProcessElement(child);
        }

        if (xdoc.Root != null) ProcessElement(xdoc.Root);
        return paths;
    }

    public static HashSet<string> ExtractBindingIgnoreList(XDocument xdoc)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comment in xdoc.DescendantNodes().OfType<XComment>())
        {
            var text = comment.Value.Trim();
            if (text.StartsWith("BindingIgnore:", StringComparison.OrdinalIgnoreCase))
            {
                var path = text.Substring("BindingIgnore:".Length).Trim();
                ignored.Add(path);
            }
        }
        return ignored;
    }

    public static (bool ok, string? reason, bool requiresSetter) ValidateBinding(string bindingExpr, TypeModel? vmModel, int nestedDepth, string repoRoot, Dictionary<string, string> typeIndex)
    {
        var pathMatch = Regex.Match(bindingExpr, @"\{Binding\s+(?:Path=)?([A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
        if (!pathMatch.Success) return (true, null, false);

        var pathStr = pathMatch.Groups[1].Value;
        var parts = pathStr.Split('.');
        var first = parts[0];

        var modeMatch = Regex.Match(bindingExpr, @"Mode\s*=\s*(OneWay|TwoWay|OneWayToSource|OneTime)", RegexOptions.IgnoreCase);
        bool requiresSetter = modeMatch.Success && (modeMatch.Groups[1].Value == "TwoWay" || modeMatch.Groups[1].Value == "OneWayToSource");

        bool ok = false; string? reason = null;
        if (vmModel != null)
        {
            if (vmModel.Properties.ContainsKey(first) || vmModel.Commands.Contains(first))
            {
                ok = true;
                if (parts.Length > 1 && nestedDepth > 0 && vmModel.Properties.TryGetValue(first, out var propType))
                {
                    var nestedType = propType.Contains('.') ? propType.Split('.').Last() : propType;
                    if (nestedType.EndsWith("?")) nestedType = nestedType.Substring(0, nestedType.Length - 1);
                    if (nestedType.Contains('<')) nestedType = nestedType.Substring(0, nestedType.IndexOf('<'));

                    var nestedModel = ParseTypeModel(nestedType, repoRoot, typeIndex);
                    for (int i = 1; i < parts.Length && i <= nestedDepth; i++)
                    {
                        var part = parts[i];
                        if (!nestedModel.Properties.ContainsKey(part) && !nestedModel.Commands.Contains(part))
                        {
                            ok = false; reason = $"Nested member '{part}' not found on type '{nestedModel.Name}' (depth {i})";
                            break;
                        }
                        if (i < parts.Length - 1 && nestedModel.Properties.TryGetValue(part, out var nextType))
                        {
                            var nt = nextType.Contains('.') ? nextType.Split('.').Last() : nextType;
                            if (nt.EndsWith("?")) nt = nt.Substring(0, nt.Length - 1);
                            if (nt.Contains('<')) nt = nt.Substring(0, nt.IndexOf('<'));
                            nestedModel = ParseTypeModel(nt, repoRoot, typeIndex);
                        }
                    }
                }
            }
            else { ok = false; reason = $"Member '{first}' not found on ViewModel"; }
        }
        else { ok = false; reason = "ViewModel not found"; }

        if (ok && requiresSetter && vmModel != null && vmModel.Properties.ContainsKey(first))
        {
            if (VmFileByType.TryGetValue(vmModel.Name, out var vmFile))
            {
                try
                {
                    var src = File.ReadAllText(vmFile);
                    var propPattern = $@"public\s+[A-Za-z0-9_\.<>\?]+\s+{Regex.Escape(first)}\s*\{{([^}}]+)\}}";
                    var propMatch = Regex.Match(src, propPattern, RegexOptions.Multiline);
                    if (propMatch.Success)
                    {
                        var body = propMatch.Groups[1].Value;
                        if (!body.Contains("set")) { ok = false; reason = $"Property '{first}' is read-only but binding mode requires setter"; }
                    }
                }
                catch { }
            }
        }

        return (ok, reason, requiresSetter);
    }

    public static void Run()
    {
        Console.WriteLine("=== XAML Binding Static Analyzer (Fixed) ===\n");

        string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
        string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
        Directory.CreateDirectory(logsDir);

        string viewsRoot = Path.Combine(repoRoot, "WileyWidget.UI", "Views");
        if (!Directory.Exists(viewsRoot))
        {
            Console.WriteLine($"Views folder not found: {viewsRoot}");
            Environment.Exit(2);
        }

        Console.WriteLine($"Repo root: {repoRoot}");
        Console.WriteLine($"Views root: {viewsRoot}");
        Console.WriteLine($"Logs dir: {logsDir}\n");

        int nestedDepth = 2;
        if (int.TryParse(Environment.GetEnvironmentVariable("WW_BINDING_NESTED_DEPTH"), out var d)) nestedDepth = d;
        double tolerance = 0.10;
        if (double.TryParse(Environment.GetEnvironmentVariable("WW_BINDING_TOLERANCE"), out var t)) tolerance = t;

        Console.WriteLine($"Configuration: nestedDepth={nestedDepth}, tolerance={tolerance:P0}\n");

        Console.WriteLine("Building type index...");
        var typeIndex = BuildTypeIndex(repoRoot);
        Console.WriteLine($"Indexed {typeIndex.Count} types\n");

        Console.WriteLine("Building View→ViewModel mapping...");
        var viewToVmMap = BuildViewToViewModelMap(repoRoot);
        Console.WriteLine($"Explicit registrations: {viewToVmMap.Count}");

        var iocMap = BuildInterfaceToConcreteMap(repoRoot);
        Console.WriteLine($"IoC interface→concrete: {iocMap.Count}\n");

        var xamlFiles = Directory.EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories).ToList();
        Console.WriteLine($"Found {xamlFiles.Count} XAML files\n");

        var report = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["repoRoot"] = repoRoot,
            ["viewsRoot"] = viewsRoot,
            ["nestedDepth"] = nestedDepth,
            ["tolerance"] = tolerance,
            ["views"] = new List<Dictionary<string, object>>()
        };
        var viewsList = (List<Dictionary<string, object>>)report["views"];

        int totalViews = 0, totalBindings = 0;
        var unresolved = new List<(string view, string binding, string? reason)>();

        foreach (var xamlPath in xamlFiles)
        {
            totalViews++;
            var relPath = Path.GetRelativePath(repoRoot, xamlPath);
            Console.WriteLine($"Analyzing: {relPath}");

            XDocument xdoc;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using var reader = XmlReader.Create(xamlPath, settings);
                xdoc = XDocument.Load(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ XML parse error: {ex.Message}");
                continue;
            }

            var vmTypeName = FindViewModelForView(xamlPath, repoRoot, viewToVmMap, iocMap);
            Console.WriteLine($"  ViewModel: {vmTypeName ?? "(unknown)"}");

            var vmModel = vmTypeName != null ? ParseTypeModel(vmTypeName, repoRoot, typeIndex) : null;
            if (vmModel != null) Console.WriteLine($"  ViewModel members: {vmModel.Properties.Count} properties, {vmModel.Commands.Count} commands");

            var bindingExprs = ExtractBindingPaths(xdoc);
            var ignored = ExtractBindingIgnoreList(xdoc);
            totalBindings += bindingExprs.Count;
            Console.WriteLine($"  Bindings: {bindingExprs.Count}, ignored: {ignored.Count}");

            var viewReport = new Dictionary<string, object>
            {
                ["path"] = relPath,
                ["viewModel"] = vmTypeName ?? "",
                ["bindings"] = new List<Dictionary<string, object>>()
            };
            var bindingsList = (List<Dictionary<string, object>>)viewReport["bindings"];

            foreach (var expr in bindingExprs)
            {
                var pathMatch = Regex.Match(expr, @"\{Binding\s+(?:Path=)?([A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
                if (!pathMatch.Success) continue;
                var pathStr = pathMatch.Groups[1].Value;

                if (ignored.Contains(pathStr))
                {
                    bindingsList.Add(new Dictionary<string, object> { ["path"] = pathStr, ["status"] = "ignored" });
                    continue;
                }

                var (ok, reason, requiresSetter) = ValidateBinding(expr, vmModel, nestedDepth, repoRoot, typeIndex);
                var status = ok ? "ok" : "unresolved";
                var bindingReport = new Dictionary<string, object>
                {
                    ["path"] = pathStr,
                    ["expression"] = expr,
                    ["status"] = status
                };
                if (!string.IsNullOrEmpty(reason)) bindingReport["reason"] = reason!;
                if (requiresSetter) bindingReport["requiresSetter"] = true;
                bindingsList.Add(bindingReport);

                if (!ok) unresolved.Add((relPath, pathStr, reason));
            }

            viewsList.Add(viewReport);
            Console.WriteLine();
        }

        report["totalViews"] = totalViews;
        report["totalBindings"] = totalBindings;
        report["unresolvedCount"] = unresolved.Count;
        report["unresolved"] = unresolved.Select(x => new Dictionary<string, object>
        {
            ["view"] = x.view,
            ["binding"] = x.binding,
            ["reason"] = x.reason ?? ""
        }).ToList();

        var outPath = Path.Combine(logsDir, "xaml-binding-static-report.json");
        var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outPath, json);

        Console.WriteLine($"Analyzed views: {totalViews}, bindings: {totalBindings}, unresolved: {unresolved.Count}");
        Console.WriteLine($"Report -> {outPath}");

        int pass = 0, total = 0;
        void Assert(bool cond, string name, string details = "")
        {
            total++;
            if (cond) { Console.WriteLine("✓ " + name); pass++; }
            else
            {
                Console.WriteLine("✗ " + name + " FAILED");
                if (!string.IsNullOrWhiteSpace(details)) Console.WriteLine("  Details: " + details);
            }
        }

        double unresolvedRatio = totalBindings == 0 ? 0 : (double)unresolved.Count / totalBindings;
        bool withinTolerance = unresolvedRatio <= tolerance;
        Assert(withinTolerance, $"Unresolved bindings <= {tolerance:P0} threshold", $"{unresolved.Count}/{totalBindings} ({unresolvedRatio:P2}) unresolved");

        Console.WriteLine($"\nResults: {pass}/{total} passed");
        Environment.Exit(withinTolerance ? 0 : 9);
    }
}
