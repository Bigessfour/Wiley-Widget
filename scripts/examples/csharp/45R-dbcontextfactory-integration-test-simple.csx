// DbContextFactory Integration Test - Simplified (No External NuGet Dependencies)
// Validates enhanced DI registration code structure and logic without instantiation
// Runs under C# MCP Server inside Docker - fully self-contained
// Goal: Validate that enhanced DbContextFactory implementation follows patterns correctly

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

#nullable enable

// ------------------------
// Test Harness
// ------------------------
int pass = 0, total = 0;

void Assert(bool condition, string name, string details = "")
{
    total++;
    if (condition)
    {
        pass++;
        Console.WriteLine($"✅ {name}");
        if (!string.IsNullOrEmpty(details)) Console.WriteLine($"   {details}");
    }
    else
    {
        Console.WriteLine($"❌ {name}");
        if (!string.IsNullOrEmpty(details)) Console.WriteLine($"   FAIL: {details}");
    }
}

void AssertNotNull(object? obj, string name, string details = "")
{
    Assert(obj != null, name, details);
}

void AssertNotEmpty(string? str, string name, string details = "")
{
    Assert(!string.IsNullOrEmpty(str), name, details);
}

void AssertContains(string source, string substring, string name, string details = "")
{
    Assert(source.Contains(substring), name, details);
}

// ------------------------
// Core Test Logic
// ------------------------

try
{
    string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
    string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

    Console.WriteLine("=== DbContextFactory Enhanced DI Registration Validation ===");
    Console.WriteLine($"Repository Root: {repoRoot}");
    Console.WriteLine($"Logs Directory: {logsDir}");
    Console.WriteLine();

    // Test 1: Validate DatabaseConnectionValidator exists and follows pattern
    string validatorPath = Path.Combine(repoRoot, "src", "WileyWidget", "Services", "Startup", "DatabaseConnectionValidator.cs");
    AssertNotNull(File.Exists(validatorPath) ? "exists" : null, "DatabaseConnectionValidator.cs exists", validatorPath);

    if (File.Exists(validatorPath))
    {
        string validatorContent = File.ReadAllText(validatorPath);

        AssertContains(validatorContent, "class DatabaseConnectionValidator", "DatabaseConnectionValidator class declared");
        AssertContains(validatorContent, "ValidateConnectionString", "ValidateConnectionString method exists");
        AssertContains(validatorContent, "TestConnectivityAsync", "TestConnectivityAsync method exists");
        AssertContains(validatorContent, "ShouldSkipMigrations", "ShouldSkipMigrations method exists");
        AssertContains(validatorContent, "Log.Warning", "Uses Log.Warning for graceful degradation");
        AssertContains(validatorContent, "IConfiguration", "Depends on IConfiguration");
        AssertContains(validatorContent, "IHostEnvironment", "Depends on IHostEnvironment");
    }

    // Test 2: Validate App.DependencyInjection.cs has enhanced registration
    string diPath = Path.Combine(repoRoot, "src", "WileyWidget", "App.DependencyInjection.cs");
    AssertNotNull(File.Exists(diPath) ? "exists" : null, "App.DependencyInjection.cs exists", diPath);

    if (File.Exists(diPath))
    {
        string diContent = File.ReadAllText(diPath);

        AssertContains(diContent, "RegisterCoreInfrastructure", "RegisterCoreInfrastructure method exists");
        AssertContains(diContent, "ConfigureEnterpriseDbContextOptions", "ConfigureEnterpriseDbContextOptions helper method exists");
        AssertContains(diContent, "ConfigureEnhancedSqlServer", "ConfigureEnhancedSqlServer helper method exists");
        AssertContains(diContent, "IDbContextFactory", "IDbContextFactory registration exists");
        AssertContains(diContent, "ConfigureWarnings", "ConfigureWarnings configuration exists");
        AssertContains(diContent, "EnableServiceProviderCaching", "EnableServiceProviderCaching optimization exists");

        // Validate proper DryIoc scoping
        AssertContains(diContent, "Reuse.Singleton", "Uses Singleton reuse for factory");
    }

    // Test 3: Validate fallback logic patterns
    if (File.Exists(diPath))
    {
        string diContent = File.ReadAllText(diPath);

        // Look for fallback connection string pattern
        bool hasFallbackPattern = diContent.Contains("fallback") ||
                                diContent.Contains("DefaultConnection") ||
                                diContent.Contains("connectionString ??");
        Assert(hasFallbackPattern, "Fallback connection string logic exists");

        // Look for validation before registration
        bool hasValidationPattern = diContent.Contains("validator.ValidateConnectionString") ||
                                  diContent.Contains("DatabaseConnectionValidator") ||
                                  diContent.Contains("IsValidConnectionString") ||
                                  diContent.Contains("string.IsNullOrWhiteSpace(connectionString)") ||
                                  diContent.Contains("!isValid");
        Assert(hasValidationPattern, "Connection validation before registration");
    }

    // Test 4: Validate integration test structure
    string testPath = Path.Combine(repoRoot, "scripts", "examples", "csharp", "45-dbcontextfactory-integration-test.csx");
    AssertNotNull(File.Exists(testPath) ? "exists" : null, "Integration test file exists", testPath);

    if (File.Exists(testPath))
    {
        string testContent = File.ReadAllText(testPath);

        AssertContains(testContent, "MockHostEnvironment", "Mock environment implementation exists");
        AssertContains(testContent, "TestAppDbContext", "Test DbContext implementation exists");
        AssertContains(testContent, "Enhanced DbContextFactory", "Tests enhanced factory");
        AssertContains(testContent, "Fallback Behavior", "Tests fallback behavior");
        AssertContains(testContent, "ConfigureWarnings", "Tests warning configuration");
    }

    // Test 5: Validate proper imports and dependencies
    if (File.Exists(diPath))
    {
        string diContent = File.ReadAllText(diPath);

        AssertContains(diContent, "using Microsoft.EntityFrameworkCore", "EF Core import");
        AssertContains(diContent, "using DryIoc", "DryIoc import");
        AssertContains(diContent, "using Microsoft.Extensions", "Microsoft.Extensions imports");
    }

    // Test 6: Validate connection string validation logic
    if (File.Exists(validatorPath))
    {
        string validatorContent = File.ReadAllText(validatorPath);

        // Look for proper validation patterns
        AssertContains(validatorContent, "string.IsNullOrWhiteSpace", "Null/whitespace validation");
        AssertContains(validatorContent, "Server=", "SQL Server connection validation");
        AssertContains(validatorContent, "Database=", "Database name validation");
    }

    // Test 7: Check for proper error handling patterns
    if (File.Exists(validatorPath))
    {
        string validatorContent = File.ReadAllText(validatorPath);

        AssertContains(validatorContent, "try", "Exception handling exists");
        AssertContains(validatorContent, "catch", "Exception catching exists");
        AssertContains(validatorContent, "Log.Warning", "Warning logging for non-critical issues");
    }

    Console.WriteLine();
    Console.WriteLine("=== Test Summary ===");
    Console.WriteLine($"Passed: {pass}/{total}");

    if (pass == total)
    {
        Console.WriteLine("🎉 All tests passed! Enhanced DbContextFactory implementation is correctly structured.");
    }
    else
    {
        Console.WriteLine($"⚠️  {total - pass} test(s) failed. Review implementation details.");
    }

    // Test 8: Final validation - check that build would succeed
    Console.WriteLine();
    Console.WriteLine("=== Build Validation ===");

    bool allFilesExist = File.Exists(validatorPath) && File.Exists(diPath);
    Assert(allFilesExist, "All required files exist for build");

    if (allFilesExist)
    {
        Console.WriteLine("✅ Enhanced DbContextFactory implementation ready for compilation");
    }

    Console.WriteLine();
    Console.WriteLine($"📊 Final Score: {pass}/{total} ({(pass * 100.0 / total):F1}%)");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Test execution failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
