using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Database seeder for Phase 1 development and testing
/// </summary>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public DatabaseSeeder(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Seeds the database with sample enterprise data
    /// </summary>
    public async Task SeedAsync()
    {
        // Only seed if no enterprises exist
        if (await _context.Enterprises.AnyAsync())
        {
            Console.WriteLine("Database already seeded. Skipping...");
            return;
        }

        Console.WriteLine("Seeding database with sample enterprise data...");

        // Create sample enterprises
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Water",
                CurrentRate = 5.00m,
                MonthlyExpenses = 7500.00m,
                CitizenCount = 500,
                Notes = "Municipal water utility serving 500 citizens"
            },
            new Enterprise
            {
                Name = "Sewer",
                CurrentRate = 3.50m,
                MonthlyExpenses = 4200.00m,
                CitizenCount = 500,
                Notes = "Wastewater treatment and sewer services"
            },
            new Enterprise
            {
                Name = "Trash",
                CurrentRate = 15.00m,
                MonthlyExpenses = 2800.00m,
                CitizenCount = 500,
                Notes = "Solid waste collection and disposal"
            },
            new Enterprise
            {
                Name = "Apartments",
                CurrentRate = 0.00m, // Legacy apartments may not charge rates
                MonthlyExpenses = 1200.00m,
                CitizenCount = 50, // Fewer citizens in apartments
                Notes = "Legacy apartment complex maintenance"
            }
        };

        // MonthlyRevenue is now automatically calculated from CitizenCount * CurrentRate
        await _context.Enterprises.AddRangeAsync(enterprises);
        await _context.SaveChangesAsync();

        // Create sample budget interactions
        var interactions = new List<BudgetInteraction>
        {
            new BudgetInteraction
            {
                PrimaryEnterpriseId = enterprises.First(e => e.Name == "Water").Id,
                SecondaryEnterpriseId = enterprises.First(e => e.Name == "Sewer").Id,
                InteractionType = "SharedCost",
                Description = "Shared infrastructure maintenance costs",
                MonthlyAmount = 800.00m,
                IsCost = true,
                Notes = "Joint maintenance of shared water/sewer pipes"
            },
            new BudgetInteraction
            {
                PrimaryEnterpriseId = enterprises.First(e => e.Name == "Trash").Id,
                InteractionType = "Transfer",
                Description = "Surplus funds transfer to general fund",
                MonthlyAmount = 1200.00m,
                IsCost = false,
                Notes = "Monthly surplus transferred to municipal general fund"
            }
        };

        await _context.BudgetInteractions.AddRangeAsync(interactions);
        await _context.SaveChangesAsync();

        // Create initial overall budget snapshot
        var overallBudget = new OverallBudget
        {
            TotalMonthlyRevenue = enterprises.Sum(e => e.MonthlyRevenue),
            TotalMonthlyExpenses = enterprises.Sum(e => e.MonthlyExpenses),
            TotalCitizensServed = enterprises.Sum(e => e.CitizenCount),
            IsCurrent = true,
            Notes = "Initial budget snapshot created during Phase 1 setup"
        };

        overallBudget.TotalMonthlyBalance = overallBudget.TotalMonthlyRevenue - overallBudget.TotalMonthlyExpenses;
        overallBudget.AverageRatePerCitizen = overallBudget.TotalCitizensServed > 0 ?
            overallBudget.TotalMonthlyRevenue / overallBudget.TotalCitizensServed : 0;

        await _context.OverallBudgets.AddAsync(overallBudget);
        await _context.SaveChangesAsync();

        Console.WriteLine("Database seeded successfully!");
        Console.WriteLine($"Created {enterprises.Count} enterprises");
        Console.WriteLine($"Created {interactions.Count} budget interactions");
        Console.WriteLine($"Total Monthly Revenue: ${overallBudget.TotalMonthlyRevenue:F2}");
        Console.WriteLine($"Total Monthly Expenses: ${overallBudget.TotalMonthlyExpenses:F2}");
        Console.WriteLine($"Monthly Balance: ${overallBudget.TotalMonthlyBalance:F2}");
    }
}
