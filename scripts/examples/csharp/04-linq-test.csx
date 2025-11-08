// LINQ Query Examples
// Demonstrates advanced LINQ operations for data analysis

using System;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== LINQ Operations Test ===\n");

// Sample budget data
public class BudgetEntry
{
    public string Department { get; set; }
    public string Category { get; set; }
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
    public int Year { get; set; }

    public decimal Variance => Actual - Budgeted;
    public decimal VariancePercent => Budgeted != 0 ? (Variance / Budgeted) * 100 : 0;
}

// Create sample data
var entries = new List<BudgetEntry>
{
    new() { Department = "Police", Category = "Personnel", Budgeted = 1_000_000, Actual = 980_000, Year = 2025 },
    new() { Department = "Police", Category = "Equipment", Budgeted = 200_000, Actual = 225_000, Year = 2025 },
    new() { Department = "Fire", Category = "Personnel", Budgeted = 800_000, Actual = 790_000, Year = 2025 },
    new() { Department = "Fire", Category = "Equipment", Budgeted = 150_000, Actual = 145_000, Year = 2025 },
    new() { Department = "Public Works", Category = "Personnel", Budgeted = 600_000, Actual = 615_000, Year = 2025 },
    new() { Department = "Public Works", Category = "Equipment", Budgeted = 400_000, Actual = 380_000, Year = 2025 },
    new() { Department = "Parks", Category = "Personnel", Budgeted = 300_000, Actual = 295_000, Year = 2025 },
    new() { Department = "Parks", Category = "Equipment", Budgeted = 100_000, Actual = 105_000, Year = 2025 }
};

Console.WriteLine($"📊 Total entries: {entries.Count}\n");

// Query 1: Group by department
Console.WriteLine("1️⃣ Budget by Department:");
var byDepartment = entries
    .GroupBy(e => e.Department)
    .Select(g => new
    {
        Department = g.Key,
        TotalBudgeted = g.Sum(e => e.Budgeted),
        TotalActual = g.Sum(e => e.Actual),
        Variance = g.Sum(e => e.Variance)
    })
    .OrderByDescending(x => x.TotalBudgeted);

foreach (var dept in byDepartment)
{
    Console.WriteLine($"   {dept.Department,-15} Budgeted: ${dept.TotalBudgeted,10:N0}  Actual: ${dept.TotalActual,10:N0}  Variance: ${dept.Variance,10:N0}");
}

// Query 2: Over/Under budget analysis
Console.WriteLine("\n2️⃣ Over/Under Budget Analysis:");
var overBudget = entries.Where(e => e.Actual > e.Budgeted).ToList();
var underBudget = entries.Where(e => e.Actual < e.Budgeted).ToList();

Console.WriteLine($"   Over budget: {overBudget.Count} entries (${overBudget.Sum(e => e.Variance):N0})");
Console.WriteLine($"   Under budget: {underBudget.Count} entries (${underBudget.Sum(e => e.Variance):N0})");

// Query 3: Top variances
Console.WriteLine("\n3️⃣ Top 3 Positive Variances:");
var topPositive = entries
    .OrderByDescending(e => e.Variance)
    .Take(3);

foreach (var entry in topPositive)
{
    Console.WriteLine($"   {entry.Department} / {entry.Category}: +${entry.Variance:N0} ({entry.VariancePercent:F1}%)");
}

Console.WriteLine("\n4️⃣ Top 3 Negative Variances:");
var topNegative = entries
    .OrderBy(e => e.Variance)
    .Take(3);

foreach (var entry in topNegative)
{
    Console.WriteLine($"   {entry.Department} / {entry.Category}: ${entry.Variance:N0} ({entry.VariancePercent:F1}%)");
}

// Query 4: Category analysis
Console.WriteLine("\n5️⃣ By Category:");
var byCategory = entries
    .GroupBy(e => e.Category)
    .Select(g => new
    {
        Category = g.Key,
        Count = g.Count(),
        TotalBudgeted = g.Sum(e => e.Budgeted),
        TotalActual = g.Sum(e => e.Actual)
    });

foreach (var cat in byCategory)
{
    Console.WriteLine($"   {cat.Category,-15} ({cat.Count} depts)  Budgeted: ${cat.TotalBudgeted,10:N0}  Actual: ${cat.TotalActual,10:N0}");
}

// Query 5: Statistics
Console.WriteLine("\n6️⃣ Overall Statistics:");
var totalBudgeted = entries.Sum(e => e.Budgeted);
var totalActual = entries.Sum(e => e.Actual);
var avgVariancePercent = entries.Average(e => Math.Abs(e.VariancePercent));

Console.WriteLine($"   Total Budgeted: ${totalBudgeted:N0}");
Console.WriteLine($"   Total Actual: ${totalActual:N0}");
Console.WriteLine($"   Total Variance: ${(totalActual - totalBudgeted):N0}");
Console.WriteLine($"   Avg Variance %: {avgVariancePercent:F2}%");

Console.WriteLine("\n✅ LINQ test completed successfully!");
return $"Analyzed {entries.Count} budget entries";
