#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.8.0"
#r "nuget: System.Text.Json, 8.0.4"
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class RegionRegistration
{
    public string Region { get; set; } = string.Empty;
    public string ViewType { get; set; } = string.Empty;
}

public class CoreModuleReport
{
    public string ModulePath { get; set; } = string.Empty;
    public string ModuleName { get; set; } = "CoreModule";
    public List<string> ResolvedTypes { get; set; } = new();
    public List<RegionRegistration> Regions { get; set; } = new();
    public bool Health_RegisterModuleCalled { get; set; }
    public bool Health_MarkInitializedCalled { get; set; }
    public List<string> Dependents { get; set; } = new();
    public List<string> Methods { get; set; } = new();
}

public static class ModuleAnalyzer
{
    public static CoreModuleReport AnalyzeCoreModule(string repoRoot)
    {
        var report = new CoreModuleReport();

        // Locate CoreModule.cs (prefer main src, fallback to UI)
        var candidates = new[]
        {
            Path.Combine(repoRoot, "src", "Startup", "Modules", "CoreModule.cs"),
            Path.Combine(repoRoot, "WileyWidget.UI", "Startup", "Modules", "CoreModule.cs")
        };
        var modulePath = candidates.FirstOrDefault(File.Exists);
        if (modulePath == null)
        {
            throw new FileNotFoundException("CoreModule.cs not found in expected locations.");
        }

        report.ModulePath = modulePath;
        var text = File.ReadAllText(modulePath);

        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetCompilationUnitRoot();

        // Find class with [Module(ModuleName = "CoreModule")]
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl != null)
        {
            var moduleAttr = classDecl.AttributeLists
                .SelectMany(a => a.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Contains("Module") && a.ArgumentList != null && a.ArgumentList.Arguments.ToString().Contains("CoreModule"));
            if (moduleAttr != null)
            {
                var arg = moduleAttr.ArgumentList!.Arguments.FirstOrDefault();
                if (arg != null)
                {
                    report.ModuleName = "CoreModule"; // fixed per attribute
                }
            }
        }

        // Collect methods and invocations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        report.Methods = methods.Select(m => m.Identifier.Text).Distinct().ToList();

        foreach (var method in methods)
        {
            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var exprText = invocation.Expression.ToString();

                // containerProvider.Resolve<T>() or container.Resolve(typeof(T))
                if (exprText.Contains("Resolve"))
                {
                    // Try to extract generic type argument: Resolve<IModuleHealthService>
                    if (invocation.Expression is MemberAccessExpressionSyntax maes && maes.Name is GenericNameSyntax gns && gns.TypeArgumentList.Arguments.Count == 1)
                    {
                        var typeArg = gns.TypeArgumentList.Arguments[0].ToString();
                        report.ResolvedTypes.Add(typeArg);
                    }
                    else
                    {
                        // fallback: look for typeof(X)
                        var typeOfArg = invocation.ArgumentList?.Arguments.FirstOrDefault(a => a.ToString().StartsWith("typeof"))?.ToString();
                        if (!string.IsNullOrEmpty(typeOfArg))
                        {
                            report.ResolvedTypes.Add(typeOfArg);
                        }
                    }
                }

                // regionManager.RegisterViewWithRegion("Region", typeof(View))
                if (exprText.EndsWith("RegisterViewWithRegion"))
                {
                    var args = invocation.ArgumentList?.Arguments;
                    if (args != null && args.Value.Count >= 2)
                    {
                        var regionLit = args.Value[0].ToString().Trim('"');
                        var viewArg = args.Value[1].ToString();
                        var viewType = viewArg.StartsWith("typeof(") ? viewArg.Replace("typeof(", string.Empty).TrimEnd(')') : viewArg;
                        report.Regions.Add(new RegionRegistration
                        {
                            Region = regionLit.Trim('"'),
                            ViewType = viewType
                        });
                    }
                }

                // moduleHealthService.RegisterModule("CoreModule")
                if (exprText.Contains("RegisterModule") && invocation.ArgumentList != null)
                {
                    if (invocation.ArgumentList.Arguments.ToString().Contains("CoreModule"))
                    {
                        report.Health_RegisterModuleCalled = true;
                    }
                }

                // moduleHealthService.MarkModuleInitialized("CoreModule", ...)
                if (exprText.Contains("MarkModuleInitialized") && invocation.ArgumentList != null)
                {
                    if (invocation.ArgumentList.Arguments.ToString().Contains("CoreModule"))
                    {
                        report.Health_MarkInitializedCalled = true;
                    }
                }
            }
        }

        // Find other modules with [ModuleDependency("CoreModule")]
        var modulesDir = Path.Combine(repoRoot, "src", "Startup", "Modules");
        if (Directory.Exists(modulesDir))
        {
            foreach (var file in Directory.EnumerateFiles(modulesDir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var content = File.ReadAllText(file);
                if (content.Contains("ModuleDependency(\"CoreModule\")"))
                {
                    // Extract module name from [Module(ModuleName = "X")] attribute
                    var tree2 = CSharpSyntaxTree.ParseText(content);
                    var root2 = tree2.GetCompilationUnitRoot();
                    var moduleClass = root2.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (moduleClass != null)
                    {
                        var moduleAttr = moduleClass.AttributeLists.SelectMany(a => a.Attributes)
                            .FirstOrDefault(a => a.Name.ToString().Contains("Module") && a.ArgumentList != null);
                        string? depName = null;
                        if (moduleAttr?.ArgumentList != null)
                        {
                            // try ModuleName = "X"
                            var arg = moduleAttr.ArgumentList.Arguments.FirstOrDefault();
                            if (arg != null)
                            {
                                var txt = arg.ToString();
                                var idx = txt.IndexOf("\"", StringComparison.Ordinal);
                                if (idx >= 0)
                                {
                                    depName = txt.Substring(idx).Trim('"', ')', ' ', ',');
                                }
                            }
                        }
                        if (string.IsNullOrWhiteSpace(depName))
                        {
                            depName = moduleClass.Identifier.Text;
                        }
                        if (!string.IsNullOrWhiteSpace(depName))
                            report.Dependents.Add(depName);
                    }
                }
            }
        }

        // Normalize
        report.ResolvedTypes = report.ResolvedTypes.Distinct().OrderBy(x => x).ToList();
        report.Dependents = report.Dependents.Distinct().OrderBy(x => x).ToList();
        report.Regions = report.Regions
            .GroupBy(r => r.Region+"|"+r.ViewType)
            .Select(g => g.First())
            .OrderBy(r => r.Region).ToList();

        return report;
    }

    public static void Print(CoreModuleReport report)
    {
        Console.WriteLine($"CoreModule file: {report.ModulePath}");
        Console.WriteLine($"Module name: {report.ModuleName}");
        Console.WriteLine();
        Console.WriteLine("Connections:");
        Console.WriteLine("- DI resolves:");
        foreach (var t in report.ResolvedTypes)
            Console.WriteLine($"  • {t}");
        Console.WriteLine("- Region registrations:");
        foreach (var r in report.Regions)
            Console.WriteLine($"  • {r.Region} <= {r.ViewType}");
        Console.WriteLine("- Health service calls:");
        Console.WriteLine($"  • RegisterModule: {(report.Health_RegisterModuleCalled ? "YES" : "NO")}");
        Console.WriteLine($"  • MarkModuleInitialized: {(report.Health_MarkInitializedCalled ? "YES" : "NO")}");
        Console.WriteLine("- Methods:");
        foreach (var m in report.Methods)
            Console.WriteLine($"  • {m}");
        Console.WriteLine("- Modules depending on CoreModule:");
        foreach (var d in report.Dependents)
            Console.WriteLine($"  • {d}");
    }

    public static void WriteJson(CoreModuleReport report, string? outPath)
    {
        if (string.IsNullOrWhiteSpace(outPath)) return;
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(report, opts);
        File.WriteAllText(outPath!, json);
    }

    public static int Validate(CoreModuleReport report)
    {
        var failures = new List<string>();

        if (!report.Health_RegisterModuleCalled)
            failures.Add("IModuleHealthService.RegisterModule(\"CoreModule\") not found");
        if (!report.Health_MarkInitializedCalled)
            failures.Add("IModuleHealthService.MarkModuleInitialized(\"CoreModule\") not found");

        if (!report.Regions.Any(r => r.Region == "SettingsRegion"))
            failures.Add("SettingsRegion registration not found");
        if (!report.ResolvedTypes.Any(t => t.Contains("SettingsViewModel")))
            failures.Add("SettingsViewModel resolve not found");
        if (!report.ResolvedTypes.Any(t => t.Contains("IRegionManager")))
            failures.Add("IRegionManager resolve not found");

        if (failures.Count > 0)
        {
            Console.WriteLine("\n❌ CoreModule connection validation failed:");
            foreach (var f in failures) Console.WriteLine("  - " + f);
            return 1;
        }

        Console.WriteLine("\n✅ CoreModule connections validated");
        return 0;
    }
}
