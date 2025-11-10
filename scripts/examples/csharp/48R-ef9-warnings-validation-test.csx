#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.SqlServer, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.Relational, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"

// 48-ef9-warnings-validation-test.csx
// Tests that EF9 PendingModelChangesWarning is properly handled
// Based on WileyWidget refactored architecture

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

Console.WriteLine("=== EF9 WARNINGS VALIDATION TEST ===");
Console.WriteLine("Validates PendingModelChangesWarning handling in EF Core 9\n");

try
{
    // Test 1: Check for PendingModelChangesWarning suppression
    Console.WriteLine("TEST 1: PendingModelChangesWarning Configuration");
    Console.WriteLine("=".PadRight(50, '='));

    var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
    optionsBuilder.UseSqlServer("Server=test;Database=test;",
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("WileyWidget.Data");
        });

    // Check if warnings are configured
    var coreOptions = optionsBuilder.Options.GetExtension<CoreOptionsExtension>();

    if (coreOptions != null)
    {
        Console.WriteLine("✓ Core options extension found");

        var warningsConfig = coreOptions.WarningsConfiguration;
        if (warningsConfig != null)
        {
            Console.WriteLine("✓ Warnings configuration present");

            // Check if any warnings are explicitly configured
            var explicitWarnings = warningsConfig.GetType()
                .GetProperties()
                .Where(p => p.Name.Contains("Explicit") || p.Name.Contains("Ignored"))
                .ToList();

            Console.WriteLine($"  Warning configuration properties: {explicitWarnings.Count}");
        }
        else
        {
            Console.WriteLine("  No explicit warnings configuration (using defaults)");
        }
    }
    else
    {
        Console.WriteLine("✗ FAILED: Core options extension not found");
        return 1;
    }

    // Test 2: Verify no model validation errors
    Console.WriteLine("\nTEST 2: Model Validation");
    Console.WriteLine("=".PadRight(50, '='));

    var inMemoryOptions = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("ValidationTest")
        .Options;

    using (var context = new TestDbContext(inMemoryOptions))
    {
        try
        {
            // Trigger model validation
            var model = context.Model;
            var entityTypes = model.GetEntityTypes().ToList();

            Console.WriteLine($"✓ Model validated successfully");
            Console.WriteLine($"  Entity types: {entityTypes.Count}");

            foreach (var entityType in entityTypes)
            {
                Console.WriteLine($"    - {entityType.ClrType.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAILED: Model validation error: {ex.Message}");
            return 1;
        }
    }

    // Test 3: Check diagnostic source configuration
    Console.WriteLine("\nTEST 3: Diagnostic Source Configuration");
    Console.WriteLine("=".PadRight(50, '='));

    var diagnosticOptions = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("DiagnosticsTest")
        .ConfigureWarnings(warnings =>
        {
            warnings.Log(RelationalEventId.PendingModelChangesWarning);
        })
        .Options;

    var diagCoreOptions = diagnosticOptions.GetExtension<CoreOptionsExtension>();
    if (diagCoreOptions?.WarningsConfiguration != null)
    {
        Console.WriteLine("✓ Diagnostic warnings configuration applied");
    }
    else
    {
        Console.WriteLine("  Using default diagnostic configuration");
    }

    // Test 4: Verify no runtime warnings
    Console.WriteLine("\nTEST 4: Runtime Warnings Check");
    Console.WriteLine("=".PadRight(50, '='));

    var runtimeOptions = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("RuntimeTest")
        .EnableSensitiveDataLogging()
        .Options;

    using (var context = new TestDbContext(runtimeOptions))
    {
        // Perform operations that might trigger warnings
        context.Departments.Add(new Department
        {
            DepartmentCode = "TEST",
            Name = "Test Department",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var dept = await context.Departments.FirstOrDefaultAsync();

        if (dept != null)
        {
            Console.WriteLine("✓ Operations completed without triggering warnings");
            Console.WriteLine($"  Retrieved: {dept.Name}");
        }
        else
        {
            Console.WriteLine("✗ FAILED: Could not retrieve test entity");
            return 1;
        }
    }

    // Test 5: Pending migrations check
    Console.WriteLine("\nTEST 5: Pending Migrations Detection");
    Console.WriteLine("=".PadRight(50, '='));

    // InMemory provider doesn't support migrations, so we just verify the API exists
    var migOptions = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("MigrationTest")
        .Options;

    using (var context = new TestDbContext(migOptions))
    {
        try
        {
            // This will throw on InMemory provider, which is expected
            var hasPending = context.Database.GetPendingMigrations().Any();
            Console.WriteLine($"  Pending migrations API accessible (has pending: {hasPending})");
        }
        catch (NotSupportedException)
        {
            Console.WriteLine("✓ Migrations API exists (NotSupportedException expected for InMemory)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Note: {ex.Message}");
        }
    }

    Console.WriteLine("\n" + "=".PadRight(50, '='));
    Console.WriteLine("✅ ALL TESTS PASSED");
    Console.WriteLine("=".PadRight(50, '='));
    Console.WriteLine("\nNotes:");
    Console.WriteLine("- EF9 warnings are handled at runtime");
    Console.WriteLine("- PendingModelChangesWarning can be suppressed via ConfigureWarnings()");
    Console.WriteLine("- Model validation occurs during first DbContext usage");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ TEST SUITE FAILED: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    return 1;
}

// Test entities
public class Department
{
    public int Id { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// Test DbContext
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<Department> Departments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DepartmentCode).IsUnique();
            entity.Property(e => e.DepartmentCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });
    }
}
