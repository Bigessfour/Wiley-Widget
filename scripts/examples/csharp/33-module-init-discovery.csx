#!/usr/bin/env dotnet-script
#r "nuget: Prism.DryIoc, 9.0.537"
#r "nuget: DryIoc, 5.4.3"
#r "nuget: Serilog, 4.2.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Microsoft.Extensions.Logging, 9.0.0"

using System;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Prism.Ioc;
using Prism.Modularity;
using DryIoc;
using Serilog;

/// <summary>
/// MCP Discovery Script: Analyze CoreModule and related types for test construction
/// Purpose: Extract actual properties, methods, and dependencies before building tests
/// </summary>

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

Console.WriteLine("=== MCP Module Initialization Discovery ===");
Console.WriteLine($"Script started at: {DateTime.UtcNow:O}");
Console.WriteLine();

// Define paths
var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repository Root: {repoRoot}");
Console.WriteLine($"Logs Directory: {logsDir}");
Console.WriteLine();

// Discovery results structure
var discoveryResults = new Dictionary<string, object>();

try
{
    Console.WriteLine("--- Phase 1: Prism Module Pattern Analysis ---");

    // Analyze IModule interface
    var imoduleType = typeof(IModule);
    Console.WriteLine($"✓ IModule Type: {imoduleType.FullName}");
    Console.WriteLine($"  Assembly: {imoduleType.Assembly.GetName().Name} v{imoduleType.Assembly.GetName().Version}");

    var moduleMethods = imoduleType.GetMethods();
    Console.WriteLine($"  Methods ({moduleMethods.Length}):");
    foreach (var method in moduleMethods)
    {
        Console.WriteLine($"    - {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
    }

    discoveryResults["IModule.Methods"] = moduleMethods.Select(m => new {
        Name = m.Name,
        ReturnType = m.ReturnType.Name,
        Parameters = m.GetParameters().Select(p => new { Type = p.ParameterType.Name, Name = p.Name }).ToArray()
    }).ToArray();

    Console.WriteLine();
    Console.WriteLine("--- Phase 2: IContainerProvider Analysis ---");

    var containerProviderType = typeof(IContainerProvider);
    Console.WriteLine($"✓ IContainerProvider Type: {containerProviderType.FullName}");

    var providerMethods = containerProviderType.GetMethods();
    Console.WriteLine($"  Key Methods ({providerMethods.Length}):");
    foreach (var method in providerMethods.Take(10))
    {
        Console.WriteLine($"    - {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
    }

    discoveryResults["IContainerProvider.Methods"] = providerMethods.Select(m => new {
        Name = m.Name,
        ReturnType = m.ReturnType.Name,
        IsGeneric = m.IsGenericMethod
    }).Take(10).ToArray();

    Console.WriteLine();
    Console.WriteLine("--- Phase 3: Exception Types Discovery ---");

    // Discover exception types available in DryIoc
    var dryIocAssembly = typeof(Container).Assembly;
    var exceptionTypes = dryIocAssembly.GetTypes()
        .Where(t => typeof(Exception).IsAssignableFrom(t))
        .ToList();

    Console.WriteLine($"✓ DryIoc Exception Types ({exceptionTypes.Count}):");
    foreach (var exType in exceptionTypes)
    {
        Console.WriteLine($"    - {exType.Name}");
        var constructors = exType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors.Take(2))
        {
            Console.WriteLine($"      Constructor: ({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }

    discoveryResults["DryIoc.Exceptions"] = exceptionTypes.Select(t => new {
        Name = t.Name,
        FullName = t.FullName,
        Constructors = t.GetConstructors().Select(c =>
            c.GetParameters().Select(p => p.ParameterType.Name).ToArray()
        ).ToArray()
    }).ToArray();

    Console.WriteLine();
    Console.WriteLine("--- Phase 4: ContainerResolutionException Simulation ---");

    // Create a test container to understand exception behavior
    var container = new Container();

    // Attempt to resolve unregistered type to see actual exception
    try
    {
        container.Resolve(typeof(IDisposable), IfUnresolved.Throw);
        Console.WriteLine("  ⚠ No exception thrown (unexpected)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✓ Exception Type: {ex.GetType().FullName}");
        Console.WriteLine($"  Message: {ex.Message}");
        Console.WriteLine($"  Base Type: {ex.GetType().BaseType?.Name}");

        discoveryResults["ActualException"] = new {
            Type = ex.GetType().FullName,
            Message = ex.Message,
            BaseType = ex.GetType().BaseType?.Name,
            Properties = ex.GetType().GetProperties().Select(p => p.Name).ToArray()
        };
    }

    Console.WriteLine();
    Console.WriteLine("--- Phase 5: Mock Strategy Recommendations ---");

    Console.WriteLine("✓ Recommended Test Pattern:");
    Console.WriteLine("  1. Create Mock<IContainerProvider>");
    Console.WriteLine("  2. Setup Resolve<IModuleHealthService>() to return mock health service");
    Console.WriteLine("  3. Setup Resolve<SettingsViewModel>() to throw ContainerResolutionException");
    Console.WriteLine("  4. Setup Resolve<IRegionManager>() to return mock region manager");
    Console.WriteLine("  5. Verify partial initialization (health service calls made before exception)");
    Console.WriteLine("  6. Verify exception is caught and logged, not rethrown");

    discoveryResults["TestPattern"] = new {
        Mocks = new[] { "IContainerProvider", "IModuleHealthService", "IRegionManager", "SettingsViewModel" },
        Scenarios = new[] {
            "SettingsViewModel resolution throws ContainerResolutionException",
            "IModuleHealthService RegisterModule succeeds",
            "Exception is logged via Serilog",
            "No exception propagates to caller",
            "Health monitoring still registers module"
        }
    };

    Console.WriteLine();
    Console.WriteLine("--- Phase 6: File Structure Discovery ---");

    // Determine actual file paths
    var coreModulePath = Path.Combine(repoRoot, "src", "Startup", "Modules", "CoreModule.cs");
    var testFilePath = Path.Combine(repoRoot, "WileyWidget.Tests", "ModuleInitializationTests.cs");

    Console.WriteLine($"✓ Expected Files:");
    Console.WriteLine($"  CoreModule: {coreModulePath}");
    Console.WriteLine($"    Exists: {File.Exists(coreModulePath)}");
    Console.WriteLine($"  Test File: {testFilePath}");
    Console.WriteLine($"    Exists: {File.Exists(testFilePath)}");

    discoveryResults["FilePaths"] = new {
        CoreModule = coreModulePath,
        CoreModuleExists = File.Exists(coreModulePath),
        TestFile = testFilePath,
        TestFileExists = File.Exists(testFilePath)
    };

    Console.WriteLine();
    Console.WriteLine("--- Phase 7: Dependency Analysis ---");

    Console.WriteLine("✓ Required NuGet Packages for Test:");
    var requiredPackages = new[] {
        "xUnit - test framework",
        "Moq - mocking framework",
        "FluentAssertions - assertion library",
        "Prism.DryIoc - module infrastructure",
        "Serilog - logging (for CoreModule)",
        "Microsoft.Extensions.Logging - ILogger<T>"
    };

    foreach (var pkg in requiredPackages)
    {
        Console.WriteLine($"    - {pkg}");
    }

    discoveryResults["RequiredPackages"] = requiredPackages;

    Console.WriteLine();
    Console.WriteLine("=== Discovery Summary ===");
    Console.WriteLine($"Total Discovery Categories: {discoveryResults.Count}");
    Console.WriteLine($"IModule Methods: {((Array)discoveryResults["IModule.Methods"]).Length}");
    Console.WriteLine($"DryIoc Exceptions: {((Array)discoveryResults["DryIoc.Exceptions"]).Length}");
    Console.WriteLine($"Script completed at: {DateTime.UtcNow:O}");

    // Write JSON output for consumption by test generator
    var outputPath = Path.Combine(logsDir, "module-init-discovery.json");
    Directory.CreateDirectory(logsDir);

    var jsonOutput = System.Text.Json.JsonSerializer.Serialize(discoveryResults, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true
    });

    File.WriteAllText(outputPath, jsonOutput);
    Console.WriteLine();
    Console.WriteLine($"✓ Discovery results written to: {outputPath}");

    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"❌ DISCOVERY FAILED: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Log.Error(ex, "Discovery script failed");
    Environment.Exit(1);
}
