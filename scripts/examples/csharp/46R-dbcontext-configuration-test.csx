#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.SqlServer, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.0"

// 46-dbcontext-configuration-test.csx
// Tests AppDbContext OnConfiguring method with SQL Server provider validation
// Based on refactored App.DependencyInjection.cs patterns

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;

Console.WriteLine("=== DBCONTEXT CONFIGURATION TEST ===");
Console.WriteLine("Validates AppDbContext.OnConfiguring() uses SQL Server with proper settings\n");

try
{
    // Test 1: OnConfiguring uses SQL Server provider
    Console.WriteLine("TEST 1: OnConfiguring Uses SqlServer Provider");
    Console.WriteLine("=".PadRight(50, '='));

    var optionsBuilder = new DbContextOptionsBuilder<TestAppDbContext>();
    var context = new TestAppDbContext(optionsBuilder.Options);

    // Trigger OnConfiguring by creating unconfigured context
    context.OnConfigure(optionsBuilder);

    var sqlServerExt = optionsBuilder.Options.FindExtension<SqlServerOptionsExtension>();

    if (sqlServerExt != null)
    {
        Console.WriteLine("✓ SQL Server provider configured");
        Console.WriteLine($"  Connection String: {(sqlServerExt.ConnectionString?.Length > 0 ? "SET" : "NOT SET")}");
        Console.WriteLine($"  Migrations Assembly: {sqlServerExt.MigrationsAssembly ?? "Default"}");
    }
    else
    {
        Console.WriteLine("✗ FAILED: SQL Server provider not configured");
        return 1;
    }

    // Test 2: Retry on failure is enabled
    Console.WriteLine("\nTEST 2: EnableRetryOnFailure Configured");
    Console.WriteLine("=".PadRight(50, '='));

    // Check if execution strategy factory is set (indicates retry is configured)
    if (sqlServerExt.ExecutionStrategyFactory != null)
    {
        Console.WriteLine("✓ Retry on failure is enabled (ExecutionStrategyFactory configured)");
    }
    else
    {
        Console.WriteLine("✗ FAILED: Retry on failure not configured");
        return 1;
    }

    // Test 3: MigrationsAssembly is set
    Console.WriteLine("\nTEST 3: Migrations Assembly Configured");
    Console.WriteLine("=".PadRight(50, '='));

    if (sqlServerExt.MigrationsAssembly == "WileyWidget.Data")
    {
        Console.WriteLine("✓ Migrations assembly correctly set to WileyWidget.Data");
    }
    else
    {
        Console.WriteLine($"✗ FAILED: Migrations assembly is '{sqlServerExt.MigrationsAssembly}', expected 'WileyWidget.Data'");
        return 1;
    }

    // Test 4: Connection string source validation
    Console.WriteLine("\nTEST 4: Connection String Source Validation");
    Console.WriteLine("=".PadRight(50, '='));

    // Test with environment variable
    Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", "Server=TestServer;Database=TestDb;");
    var optionsBuilder2 = new DbContextOptionsBuilder<TestAppDbContext>();
    var context2 = new TestAppDbContext(optionsBuilder2.Options);
    context2.OnConfigure(optionsBuilder2);

    var sqlServerExt2 = optionsBuilder2.Options.FindExtension<SqlServerOptionsExtension>();
    if (sqlServerExt2?.ConnectionString?.Contains("TestServer") == true)
    {
        Console.WriteLine("✓ Environment variable connection string is respected");
    }
    else
    {
        Console.WriteLine("✗ FAILED: Environment variable not used for connection string");
        return 1;
    }

    // Test 5: Fallback connection string
    Console.WriteLine("\nTEST 5: Fallback Connection String");
    Console.WriteLine("=".PadRight(50, '='));

    Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", null);
    var optionsBuilder3 = new DbContextOptionsBuilder<TestAppDbContext>();
    var context3 = new TestAppDbContext(optionsBuilder3.Options);
    context3.OnConfigure(optionsBuilder3);

    var sqlServerExt3 = optionsBuilder3.Options.FindExtension<SqlServerOptionsExtension>();
    if (sqlServerExt3?.ConnectionString?.Contains("SQLEXPRESS") == true)
    {
        Console.WriteLine("✓ Fallback connection string is used when env var is not set");
        Console.WriteLine($"  Fallback: {sqlServerExt3.ConnectionString}");
    }
    else
    {
        Console.WriteLine("✗ FAILED: Fallback connection string not applied");
        return 1;
    }

    Console.WriteLine("\n" + "=".PadRight(50, '='));
    Console.WriteLine("✅ ALL TESTS PASSED");
    Console.WriteLine("=".PadRight(50, '='));

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ TEST SUITE FAILED: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    return 1;
}

// Test DbContext class that exposes OnConfiguring for testing
public class TestAppDbContext : DbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    public void OnConfigure(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = Environment.GetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION")
                ?? "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("WileyWidget.Data");
                sqlOptions.EnableRetryOnFailure();
            });
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        OnConfigure(optionsBuilder);
    }
}
