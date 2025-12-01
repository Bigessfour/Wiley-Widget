using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Services.Abstractions;

// Small validation tool - loads WinForms DI container and runs the DiValidationService

var baseDir = AppContext.BaseDirectory;
var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", ".."));

// Load configuration file if present (appsettings.Development.json or .example)
var builder = new ConfigurationBuilder()
    .SetBasePath(repoRoot)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddJsonFile("appsettings.Development.example.json", optional: true)
    .AddEnvironmentVariables();

var configuration = builder.Build();

// Ensure we have a WileyWidgetDb connection string (some dev environments keep this out of repo)
var cs = configuration.GetConnectionString("WileyWidgetDb");
if (string.IsNullOrWhiteSpace(cs))
{
    Console.WriteLine("No WileyWidgetDb connection string found in config; injecting LocalDB fallback for DI validation (won't be used to touch production data).");
    var merged = new ConfigurationBuilder()
        .AddConfiguration(configuration)
        .AddInMemoryCollection(new[] { new KeyValuePair<string, string>("ConnectionStrings:WileyWidgetDb", "Server=(localdb)\\mssqllocaldb;Database=WileyWidget_Local_DiValidator;Trusted_Connection=True;") })
        .Build();
    configuration = merged;
}

Console.WriteLine("DI validator: Building service provider from WinForms DI...");
IServiceProvider sp;
try
{
    sp = DependencyInjection.ConfigureServices(configuration);
}
catch (Exception ex)
{
    Console.WriteLine("Failed to configure services: " + ex.Message);
    return 1;
}

var validator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDiValidationService>(sp);
if (validator == null)
{
    Console.WriteLine("DiValidationService not available from container (missing registration)");
    return 2;
}

Console.WriteLine("Running full DI validation (this may take a moment)...");
var report = validator.ValidateRegistrations(includeGenerics: false);

Console.WriteLine("\n--- DI Validation Report ---\n");
Console.WriteLine($"Resolved: {report.ResolvedServices.Count}");
foreach (var s in report.ResolvedServices)
    Console.WriteLine("  ✓ " + s);

Console.WriteLine($"\nMissing: {report.MissingServices.Count}");
foreach (var s in report.MissingServices)
    Console.WriteLine("  ✗ " + s);

if (report.Errors.Count > 0)
{
    Console.WriteLine($"\nErrors: {report.Errors.Count}");
    foreach (var e in report.Errors)
    {
        Console.WriteLine("\n---\nService: " + e.ServiceType);
        Console.WriteLine("Message: " + e.ErrorMessage);
        if (!string.IsNullOrEmpty(e.SuggestedFix)) Console.WriteLine("Suggested fix: " + e.SuggestedFix);
        if (!string.IsNullOrEmpty(e.StackTrace)) Console.WriteLine("StackTrace: " + e.StackTrace);
    }
}
else
{
    Console.WriteLine("\nNo resolution errors reported.");
}

return 0;
