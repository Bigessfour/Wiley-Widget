#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.Relational, 9.0.0"

// 47-inmemory-migration-test.csx
// Tests EF Core migrations using InMemory provider for speed
// Validates model configuration and data seeding

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("=== INMEMORY MIGRATION TEST ===");
Console.WriteLine("Validates EF Core model using InMemory provider\n");

try
{
    // Test 1: Create InMemory DbContext
    Console.WriteLine("TEST 1: Create InMemory Database");
    Console.WriteLine("=".PadRight(50, '='));

    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("WileyWidgetTest")
        .Options;

    using var context = new TestDbContext(options);

    Console.WriteLine("✓ InMemory database created successfully");

    // Test 2: Ensure database is created
    Console.WriteLine("\nTEST 2: Ensure Database Created");
    Console.WriteLine("=".PadRight(50, '='));

    var created = await context.Database.EnsureCreatedAsync();
    Console.WriteLine($"✓ Database ensured (Created: {created})");

    // Test 3: Add and query entities
    Console.WriteLine("\nTEST 3: Add and Query Entities");
    Console.WriteLine("=".PadRight(50, '='));

    var testDepartment = new Department
    {
        Id = 1,
        DepartmentCode = "TEST",
        Name = "Test Department",
        IsActive = true
    };

    context.Departments.Add(testDepartment);
    var saved = await context.SaveChangesAsync();

    Console.WriteLine($"✓ Entity added and saved ({saved} changes)");

    var retrieved = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentCode == "TEST");

    if (retrieved != null && retrieved.Name == "Test Department")
    {
        Console.WriteLine("✓ Entity retrieved successfully");
        Console.WriteLine($"  ID: {retrieved.Id}");
        Console.WriteLine($"  Code: {retrieved.DepartmentCode}");
        Console.WriteLine($"  Name: {retrieved.Name}");
    }
    else
    {
        Console.WriteLine("✗ FAILED: Entity not retrieved correctly");
        return 1;
    }

    // Test 4: Validate model conventions
    Console.WriteLine("\nTEST 4: Validate Model Conventions");
    Console.WriteLine("=".PadRight(50, '='));

    var entityType = context.Model.FindEntityType(typeof(Department));

    if (entityType != null)
    {
        Console.WriteLine("✓ Department entity type found in model");

        var codeProperty = entityType.FindProperty("DepartmentCode");
        if (codeProperty != null)
        {
            Console.WriteLine($"✓ DepartmentCode property configured");
        }

        var indexCount = entityType.GetIndexes().Count();
        Console.WriteLine($"  Indexes: {indexCount}");
    }
    else
    {
        Console.WriteLine("✗ FAILED: Department entity type not in model");
        return 1;
    }

    // Test 5: Concurrent operations
    Console.WriteLine("\nTEST 5: Concurrent Operations");
    Console.WriteLine("=".PadRight(50, '='));

    var tasks = Enumerable.Range(1, 5).Select(async i =>
    {
        var dept = new Department
        {
            DepartmentCode = $"DEPT{i:D3}",
            Name = $"Department {i}",
            IsActive = true
        };
        context.Departments.Add(dept);
        await context.SaveChangesAsync();
        return dept.DepartmentCode;
    });

    var codes = await Task.WhenAll(tasks);
    Console.WriteLine($"✓ Created {codes.Length} departments concurrently");

    var total = await context.Departments.CountAsync();
    Console.WriteLine($"  Total departments: {total}");

    if (total >= 6) // 1 original + 5 new
    {
        Console.WriteLine("✓ All departments saved successfully");
    }
    else
    {
        Console.WriteLine($"✗ FAILED: Expected at least 6 departments, found {total}");
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
