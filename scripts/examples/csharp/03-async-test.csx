// Async/Await Operations Test
// Demonstrates Task-based asynchronous programming

using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== Async/Await Operations Test ===\n");

// Simulate a data service
public class BudgetDataService
{
    public async Task<List<string>> FetchDepartmentsAsync()
    {
        Console.WriteLine("⏳ Fetching departments...");
        await Task.Delay(100); // Simulate API call

        return new List<string>
        {
            "Police",
            "Fire",
            "Public Works",
            "Parks & Recreation",
            "Library",
            "City Hall"
        };
    }

    public async Task<Dictionary<string, decimal>> FetchBudgetsAsync(List<string> departments)
    {
        Console.WriteLine("⏳ Fetching budget data...");
        await Task.Delay(150); // Simulate API call

        var budgets = new Dictionary<string, decimal>
        {
            ["Police"] = 1_500_000m,
            ["Fire"] = 1_200_000m,
            ["Public Works"] = 800_000m,
            ["Parks & Recreation"] = 600_000m,
            ["Library"] = 400_000m,
            ["City Hall"] = 300_000m
        };

        return departments.ToDictionary(d => d, d => budgets.GetValueOrDefault(d, 0));
    }

    public async Task<decimal> CalculateTotalAsync(Dictionary<string, decimal> budgets)
    {
        Console.WriteLine("⏳ Calculating totals...");
        await Task.Delay(50); // Simulate calculation

        return budgets.Values.Sum();
    }
}

// Test async operations
var service = new BudgetDataService();
var startTime = DateTime.Now;

// Sequential operations
Console.WriteLine("1️⃣ Sequential operations:");
var departments = await service.FetchDepartmentsAsync();
Console.WriteLine($"   ✓ Fetched {departments.Count} departments");

var budgets = await service.FetchBudgetsAsync(departments);
Console.WriteLine($"   ✓ Fetched {budgets.Count} budget entries");

var total = await service.CalculateTotalAsync(budgets);
Console.WriteLine($"   ✓ Total budget: ${total:N2}");

var sequentialTime = (DateTime.Now - startTime).TotalMilliseconds;
Console.WriteLine($"   ⏱️  Sequential time: {sequentialTime:F0}ms\n");

// Parallel operations
Console.WriteLine("2️⃣ Parallel operations:");
startTime = DateTime.Now;

var tasks = departments.Select(async dept =>
{
    await Task.Delay(50);
    var budget = budgets[dept];
    return new { Department = dept, Budget = budget };
});

var results = await Task.WhenAll(tasks);
Console.WriteLine($"   ✓ Processed {results.Length} departments in parallel");

var parallelTime = (DateTime.Now - startTime).TotalMilliseconds;
Console.WriteLine($"   ⏱️  Parallel time: {parallelTime:F0}ms\n");

// Display results
Console.WriteLine("📊 Budget Summary:");
foreach (var result in results.OrderByDescending(r => r.Budget))
{
    Console.WriteLine($"   {result.Department,-20} ${result.Budget,12:N2}");
}

Console.WriteLine($"\n   {"Total",-20} ${total,12:N2}");
Console.WriteLine($"\n⚡ Speedup: {(sequentialTime / parallelTime):F2}x faster");

Console.WriteLine("\n✅ Async test completed successfully!");
return $"Total: ${total:N2}";
