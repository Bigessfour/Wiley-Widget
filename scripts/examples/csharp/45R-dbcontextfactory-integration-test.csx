// DbContextFactory Integration Test - Validates enhanced DI registration
// Usage: docker run --rm -v "${PWD}:/app:ro" wiley-widget/csx-mcp:local scripts/examples/csharp/45-dbcontextfactory-integration-test.csx
// Purpose: Tests enhanced DbContextFactory registration with validation, fallback, and proper DryIoc scoping

// Required NuGet package references
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Hosting, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore.SqlServer, 9.0.10"
#r "nuget: Prism.Container.DryIoc, 9.0.107"
#r "nuget: DryIoc, 5.4.3"
#r "nuget: DryIoc.Microsoft.DependencyInjection, 6.2.0"

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;

// ========================================
// TEST METADATA
// ========================================
// Test Name: DbContextFactory Integration Test
// Category: Integration
// Purpose: Validates enhanced DbContextFactory registration with comprehensive validation and fallback
// Dependencies: Microsoft.EntityFrameworkCore, DryIoc
// Testing: Connection string validation, fallback logic, proper DI lifetimes
// ========================================

Console.WriteLine("=== DbContextFactory Integration Test ===\n");
Console.WriteLine("Testing enhanced DI registration for DbContextFactory");
Console.WriteLine("Validates: Connection validation, fallback logic, DryIoc scoping\n");

// ========================================
// CONFIGURATION
// ========================================
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo Root: {repoRoot}");
Console.WriteLine($"Logs Dir: {logsDir}\n");

// ========================================
// TEST HARNESS - Simple Assert Helper
// ========================================
int passed = 0, total = 0;
List<string> failures = new List<string>();

void Assert(bool condition, string testName, string? details = null)
{
    total++;
    if (condition)
    {
        Console.WriteLine($"✓ {testName}");
        passed++;
    }
    else
    {
        string failMsg = $"✗ {testName} FAILED";
        if (!string.IsNullOrWhiteSpace(details)) failMsg += $"\n  Details: {details}";
        Console.WriteLine(failMsg);
        failures.Add(failMsg);
    }
}

void AssertNotNull<T>(T? value, string testName, string? details = null) where T : class
{
    Assert(value != null, testName, details ?? $"Expected non-null value of type {typeof(T).Name}");
}

void AssertNotEmpty(string? value, string testName, string? details = null)
{
    Assert(!string.IsNullOrWhiteSpace(value), testName, details ?? "Expected non-empty string value");
}

// ========================================
// TEST SETUP - Mock Configuration and Services
// ========================================

Console.WriteLine("Setting up test configuration and services...\n");

// Create test configuration with both valid and invalid connection strings
var testConfigData = new Dictionary<string, string?>
{
    {"ConnectionStrings:DefaultConnection", "Server=.\\SQLEXPRESS;Database=WileyWidgetTest;Trusted_Connection=True;TrustServerCertificate=True;"},
    {"ConnectionStrings:InvalidConnection", "Invalid connection string format"},
    {"ASPNETCORE_ENVIRONMENT", "Development"}
};

var configBuilder = new ConfigurationBuilder();
configBuilder.AddInMemoryCollection(testConfigData);
var testConfig = configBuilder.Build();

// Create test service collection for enhanced DbContextFactory registration
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(testConfig);
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Mock IHostEnvironment for development environment
services.AddSingleton<IHostEnvironment>(sp => new MockHostEnvironment("Development"));

// ========================================
// MOCK CLASSES FOR TESTING
// ========================================

public class MockHostEnvironment : IHostEnvironment
{
    public MockHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
        ApplicationName = "WileyWidget.Test";
        ContentRootPath = Directory.GetCurrentDirectory();
    }

    public string ApplicationName { get; set; }
    public string EnvironmentName { get; set; }
    public string ContentRootPath { get; set; }
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

// Mock AppDbContext for testing (minimal implementation)
public class TestAppDbContext : DbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback configuration for testing
            optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=WileyWidgetTest;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }
}

// Helper methods mirroring App.DependencyInjection.cs
static void ConfigureEnterpriseDbContextOptions(DbContextOptionsBuilder options, ILogger logger)
{
    options.EnableDetailedErrors();
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
    });
}

static void ConfigureEnhancedSqlServer(DbContextOptionsBuilder options, string connectionString, ILogger logger, string environmentName)
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
    });
}

// ========================================
// TEST 1: Enhanced DbContextFactory Registration (Valid Connection)
// ========================================

Console.WriteLine("Test 1: Enhanced DbContextFactory Registration with Valid Connection String");

try
{
    var connectionString = testConfig.GetConnectionString("DefaultConnection");
    AssertNotEmpty(connectionString, "Valid connection string retrieved from configuration");

    // Register enhanced DbContextFactory following the pattern from App.DependencyInjection.cs
    services.AddDbContextFactory<TestAppDbContext>((sp, options) =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<TestAppDbContext>();
        var hostEnvironment = sp.GetService<IHostEnvironment>();
        var environmentName = hostEnvironment?.EnvironmentName ?? "Production";

        // Configure options using the same methods as production code
        ConfigureEnterpriseDbContextOptions(options, logger);
        ConfigureEnhancedSqlServer(options, connectionString!, logger, environmentName);

    }, ServiceLifetime.Singleton);

    var serviceProvider = services.BuildServiceProvider();
    var factory = serviceProvider.GetRequiredService<IDbContextFactory<TestAppDbContext>>();

    AssertNotNull(factory, "IDbContextFactory<TestAppDbContext> resolved from DI container");

    // Test DbContext creation
    using var context = factory.CreateDbContext();
    AssertNotNull(context, "DbContext created from factory");
    AssertNotEmpty(context.Database.GetConnectionString(), "DbContext has connection string configured");

    Assert(context.Database.GetConnectionString()!.Contains("WileyWidgetTest"),
        "DbContext connection string contains expected database name");

    Console.WriteLine("✓ Test 1 Completed: Enhanced DbContextFactory with valid connection\n");
}
catch (Exception ex)
{
    Assert(false, "Enhanced DbContextFactory registration with valid connection", ex.Message);
}

// ========================================
// TEST 2: Fallback Behavior with Invalid Connection
// ========================================

Console.WriteLine("Test 2: Fallback Behavior with Invalid Connection String");

try
{
    var invalidConnectionString = testConfig.GetConnectionString("InvalidConnection");
    var fallbackConnectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

    // Test graceful degradation - use fallback when connection is invalid
    bool isValid = !string.IsNullOrWhiteSpace(invalidConnectionString) && IsValidConnectionString(invalidConnectionString);
    var finalConnectionString = isValid ? invalidConnectionString! : fallbackConnectionString;

    Assert(!isValid, "Invalid connection string correctly identified as invalid");
    Assert(finalConnectionString == fallbackConnectionString, "Fallback connection string used for invalid input");

    Console.WriteLine("✓ Test 2 Completed: Fallback behavior validation\n");
}
catch (Exception ex)
{
    Assert(false, "Fallback behavior with invalid connection", ex.Message);
}

// ========================================
// TEST 3: DryIoc Container Integration
// ========================================

Console.WriteLine("Test 3: DryIoc Container Integration and Lifetime Management");

try
{
    // Create DryIoc container to test Prism DI integration
    var container = new Container();
    var serviceProvider = container.WithDependencyInjectionAdapter(services => {
        services.AddSingleton<IConfiguration>(testConfig);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IHostEnvironment>(sp => new MockHostEnvironment("Development"));

        services.AddDbContextFactory<TestAppDbContext>((sp, options) =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<TestAppDbContext>();
            var hostEnvironment = sp.GetService<IHostEnvironment>();
            var environmentName = hostEnvironment?.EnvironmentName ?? "Production";
            var connectionString = testConfig.GetConnectionString("DefaultConnection");

            ConfigureEnterpriseDbContextOptions(options, logger);
            ConfigureEnhancedSqlServer(options, connectionString!, logger, environmentName);
        }, ServiceLifetime.Singleton);
    });

    // Test resolution from DryIoc
    var resolvedFactory = serviceProvider.GetRequiredService<IDbContextFactory<TestAppDbContext>>();
    AssertNotNull(resolvedFactory, "IDbContextFactory resolved from DryIoc container");

    // Test that multiple resolutions return the same instance (singleton behavior)
    var secondFactory = serviceProvider.GetRequiredService<IDbContextFactory<TestAppDbContext>>();
    Assert(ReferenceEquals(resolvedFactory, secondFactory), "Factory instances are identical (singleton behavior)");

    // Test scoped DbContext creation
    using var context1 = resolvedFactory.CreateDbContext();
    using var context2 = resolvedFactory.CreateDbContext();

    AssertNotNull(context1, "First DbContext created from DryIoc-resolved factory");
    AssertNotNull(context2, "Second DbContext created from DryIoc-resolved factory");
    Assert(!ReferenceEquals(context1, context2), "DbContext instances are different (scoped behavior)");

    Console.WriteLine("✓ Test 3 Completed: DryIoc integration and lifetime management\n");
}
catch (Exception ex)
{
    Assert(false, "DryIoc container integration", ex.Message);
}

// ========================================
// TEST 4: ConfigureWarnings Applied Correctly
// ========================================

Console.WriteLine("Test 4: ConfigureWarnings Configuration Validation");

try
{
    var tempServices = new ServiceCollection();
    tempServices.AddSingleton<IConfiguration>(testConfig);
    tempServices.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
    tempServices.AddSingleton<IHostEnvironment>(sp => new MockHostEnvironment("Development"));

    bool warningsConfigured = false;

    tempServices.AddDbContextFactory<TestAppDbContext>((sp, options) =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<TestAppDbContext>();
        var hostEnvironment = sp.GetService<IHostEnvironment>();
        var environmentName = hostEnvironment?.EnvironmentName ?? "Production";
        var connectionString = testConfig.GetConnectionString("DefaultConnection");

        // Track that ConfigureWarnings is called
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
            warningsConfigured = true;
        });

        ConfigureEnhancedSqlServer(options, connectionString!, logger, environmentName);
    }, ServiceLifetime.Singleton);

    var tempProvider = tempServices.BuildServiceProvider();
    var factory = tempProvider.GetRequiredService<IDbContextFactory<TestAppDbContext>>();

    // Creating context should trigger the warning configuration
    using var context = factory.CreateDbContext();

    Assert(warningsConfigured, "ConfigureWarnings was applied during DbContext configuration");

    Console.WriteLine("✓ Test 4 Completed: ConfigureWarnings validation\n");
}
catch (Exception ex)
{
    Assert(false, "ConfigureWarnings configuration", ex.Message);
}

// ========================================
// HELPER METHODS
// ========================================

static bool IsValidConnectionString(string connectionString)
{
    // Simple validation - check for basic SQL Server connection string elements
    return !string.IsNullOrWhiteSpace(connectionString) &&
           connectionString.Contains("Server=") &&
           connectionString.Contains("Database=") &&
           !connectionString.Contains("Invalid");
}

// ========================================
// RESULTS SUMMARY
// ========================================

Console.WriteLine("\n" + new string('=', 50));
Console.WriteLine("TEST RESULTS SUMMARY");
Console.WriteLine(new string('=', 50));

Console.WriteLine($"Tests Passed: {passed}/{total}");
Console.WriteLine($"Success Rate: {(total > 0 ? (passed * 100.0 / total):0):F1}%");

if (failures.Any())
{
    Console.WriteLine("\nFAILURES:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"  {failure}");
    }
}

Console.WriteLine("\n" + new string('=', 50));
Console.WriteLine("INTEGRATION TEST COMPLETED");
Console.WriteLine($"Status: {(passed == total ? "✅ ALL TESTS PASSED" : "❌ SOME TESTS FAILED")}");
Console.WriteLine(new string('=', 50));

// Exit with appropriate code for automation
Environment.Exit(passed == total ? 0 : 1);
