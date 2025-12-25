using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Integration.Tests.Shared;

/// <summary>
/// Comprehensive test data seeder for integration tests with realistic municipal finance data
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Seeds the database with comprehensive test data including enterprises, accounts, budgets, and departments
    /// </summary>
    public static async Task SeedComprehensiveTestDataAsync(AppDbContext dbContext)
    {
        await SeedDepartmentsAsync(dbContext);
        await SeedEnterprisesAsync(dbContext);
        await SeedMunicipalAccountsAsync(dbContext);
        await SeedBudgetsAsync(dbContext);
        await SeedTransactionsAsync(dbContext);
        await SeedAuditEntriesAsync(dbContext);
    }

    /// <summary>
    /// Seeds departments with realistic municipal department data
    /// </summary>
    public static async Task SeedDepartmentsAsync(AppDbContext dbContext)
    {
        var departments = new[]
        {
            new Department { Name = "Public Works", DepartmentCode = "PW" },
            new Department { Name = "Water & Sewer", DepartmentCode = "WS" },
            new Department { Name = "Finance", DepartmentCode = "FIN" },
            new Department { Name = "Administration", DepartmentCode = "ADMIN" },
            new Department { Name = "Fire Department", DepartmentCode = "FIRE" },
            new Department { Name = "Police Department", DepartmentCode = "POLICE" }
        };

        await dbContext.Departments.AddRangeAsync(departments);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds enterprises with realistic municipal utility data
    /// </summary>
    public static async Task SeedEnterprisesAsync(AppDbContext dbContext)
    {
        var enterprises = new[]
        {
            new Enterprise
            {
                Name = "City Water Utility",
                Type = "Water",
                CurrentRate = 45.50m,
                CitizenCount = 12500,
                MonthlyExpenses = 85000m,
                Status = EnterpriseStatus.Active
            },
            new Enterprise
            {
                Name = "City Sewer System",
                Type = "Sewer",
                CurrentRate = 38.75m,
                CitizenCount = 11800,
                MonthlyExpenses = 72000m,
                Status = EnterpriseStatus.Active
            },
            new Enterprise
            {
                Name = "City Trash Collection",
                Type = "Trash",
                CurrentRate = 22.00m,
                CitizenCount = 12200,
                MonthlyExpenses = 45000m,
                Status = EnterpriseStatus.Active
            }
        };

        await dbContext.Enterprises.AddRangeAsync(enterprises);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds municipal accounts with comprehensive chart of accounts
    /// </summary>
    public static async Task SeedMunicipalAccountsAsync(AppDbContext dbContext)
    {
        var accounts = new List<MunicipalAccount>();

        // Water Fund Accounts
        accounts.AddRange(new[]
        {
            new MunicipalAccount
            {
                AccountNumber_Value = "401-1000-100",
                Name = "Water Revenue",
                Type = AccountType.Revenue,
                Fund = MunicipalFundType.Water,
                FundDescription = "Water Utility Fund",
                BudgetAmount = 5500000m,
                IsActive = true
            },
            new MunicipalAccount
            {
                AccountNumber_Value = "501-2000-200",
                Name = "Water Operating Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Water,
                FundDescription = "Water Utility Fund",
                BudgetAmount = 4200000m,
                IsActive = true
            },
            new MunicipalAccount
            {
                AccountNumber_Value = "301-3000-300",
                Name = "Water Capital Assets",
                Type = AccountType.Asset,
                Fund = MunicipalFundType.Water,
                FundDescription = "Water Utility Fund",
                BudgetAmount = 8500000m,
                IsActive = true
            }
        });

        // Sewer Fund Accounts
        accounts.AddRange(new[]
        {
            new MunicipalAccount
            {
                AccountNumber_Value = "402-1000-100",
                Name = "Sewer Revenue",
                Type = AccountType.Revenue,
                Fund = MunicipalFundType.Sewer,
                FundDescription = "Sewer Utility Fund",
                BudgetAmount = 4800000m,
                IsActive = true
            },
            new MunicipalAccount
            {
                AccountNumber_Value = "502-2000-200",
                Name = "Sewer Operating Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Sewer,
                FundDescription = "Sewer Utility Fund",
                BudgetAmount = 3800000m,
                IsActive = true
            }
        });

        // Trash Fund Accounts
        accounts.AddRange(new[]
        {
            new MunicipalAccount
            {
                AccountNumber_Value = "403-1000-100",
                Name = "Trash Collection Revenue",
                Type = AccountType.Revenue,
                Fund = MunicipalFundType.Trash,
                FundDescription = "Trash Collection Fund",
                BudgetAmount = 3200000m,
                IsActive = true
            },
            new MunicipalAccount
            {
                AccountNumber_Value = "503-2000-200",
                Name = "Trash Collection Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.Trash,
                FundDescription = "Trash Collection Fund",
                BudgetAmount = 2800000m,
                IsActive = true
            }
        });

        // General Fund Accounts
        accounts.AddRange(new[]
        {
            new MunicipalAccount
            {
                AccountNumber_Value = "101-1000-100",
                Name = "Property Taxes",
                Type = AccountType.Revenue,
                Fund = MunicipalFundType.General,
                FundDescription = "General Fund",
                BudgetAmount = 25000000m,
                IsActive = true
            },
            new MunicipalAccount
            {
                AccountNumber_Value = "201-2000-200",
                Name = "General Government Expenses",
                Type = AccountType.Expense,
                Fund = MunicipalFundType.General,
                FundDescription = "General Fund",
                BudgetAmount = 22000000m,
                IsActive = true
            }
        });

        await dbContext.Set<MunicipalAccount>().AddRangeAsync(accounts);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds budget entries with realistic historical and current data
    /// </summary>
    public static async Task SeedBudgetsAsync(AppDbContext dbContext)
    {
        var accounts = await dbContext.Set<MunicipalAccount>().ToListAsync();
        var currentDate = DateTime.Now;
        var budgets = new List<BudgetEntry>();

        foreach (var account in accounts)
        {
        var rawAccountNumber = account.AccountNumber?.Value ?? account.AccountNumber_Value ?? account.Name ?? $"ACC-{account.Id}";
        var accountNumber = NormalizeBudgetAccountNumber(rawAccountNumber);

        // Normalize and ensure we always have a valid, trimmed account number.
        accountNumber = accountNumber.Trim();
        if (string.IsNullOrWhiteSpace(accountNumber) || !Regex.IsMatch(accountNumber, @"^\d{3}(\.\d{1,2})?$"))
        {
            Console.WriteLine($"[TestDataSeeder] Normalizing invalid account '{rawAccountNumber}' -> '{accountNumber}' for MunicipalAccount.Id={account.Id}; defaulting to '000'.");
            accountNumber = "000";
        }

                var departmentId = account.DepartmentId != 0 ? account.DepartmentId : (await dbContext.Departments.Select(d => d.Id).FirstAsync());

                // Create budget entries for the last 3 fiscal years
                for (int yearOffset = -2; yearOffset <= 0; yearOffset++)
                {
                var fiscalYear = currentDate.Year + yearOffset;
                var budgetedAmount = account.BudgetAmount * (0.95m + (decimal)Random.Shared.NextDouble() * 0.1m); // ±5% variance

                budgets.Add(new BudgetEntry
                {
                    AccountNumber = accountNumber,
                    FiscalYear = fiscalYear,
                    StartPeriod = new DateTime(fiscalYear - 1, 7, 1),
                    EndPeriod = new DateTime(fiscalYear, 6, 30),
                    BudgetedAmount = budgetedAmount,
                    ActualAmount = budgetedAmount * (0.9m + (decimal)Random.Shared.NextDouble() * 0.2m), // 80-120% of budget
                    Description = $"{account.Name} - FY{fiscalYear}",
                    DepartmentId = departmentId,
                    MunicipalAccountId = account.Id,
                    CreatedAt = currentDate.AddDays(-Random.Shared.Next(365))
                });
            }
        }

        // Basic validation to ensure generated values conform to model constraints
        var invalidBudgets = budgets.Where(b => string.IsNullOrWhiteSpace(b.AccountNumber) || !Regex.IsMatch(b.AccountNumber, @"^\d{3}(\.\d{1,2})?$")).ToList();
        if (invalidBudgets.Any())
        {
            Console.WriteLine($"[TestDataSeeder] Found invalid AccountNumber values before SaveChanges: {string.Join(", ", invalidBudgets.Select(b => b.AccountNumber ?? "(null)").Distinct())}. Defaulting to '000'.");
            foreach (var b in invalidBudgets)
            {
                b.AccountNumber = "000";
            }
        }

        await dbContext.BudgetEntries.AddRangeAsync(budgets);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds transaction data for testing transaction repository
    /// </summary>
    public static async Task SeedTransactionsAsync(AppDbContext dbContext)
    {
        var accounts = await dbContext.Set<MunicipalAccount>().ToListAsync();
        var transactions = new List<Transaction>();

        for (int i = 0; i < 100; i++)
        {
            var account = accounts[Random.Shared.Next(accounts.Count)];
            var transactionDate = DateTime.Now.AddDays(-Random.Shared.Next(365));

            var budgetEntriesForAccount = await dbContext.BudgetEntries
                .Where(be => be.MunicipalAccountId == account.Id)
                .ToListAsync();

            if (budgetEntriesForAccount.Count == 0)
                continue;

            var selectedBudget = budgetEntriesForAccount[Random.Shared.Next(budgetEntriesForAccount.Count)];

            transactions.Add(new Transaction
            {
                BudgetEntryId = selectedBudget.Id,
                Amount = (decimal)(Random.Shared.NextDouble() * 10000 - 5000), // -5000 to +5000
                Description = $"Test transaction {i + 1} for {account.Name}",
                TransactionDate = transactionDate,
                Type = Random.Shared.Next(2) == 0 ? "Debit" : "Credit"
            });
        }

        await dbContext.Transactions.AddRangeAsync(transactions);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds audit entries for testing audit repository
    /// </summary>
    public static async Task SeedAuditEntriesAsync(AppDbContext dbContext)
    {
        var auditEntries = new List<AuditEntry>();

        for (int i = 0; i < 50; i++)
        {
            var timestamp = DateTime.Now.AddDays(-Random.Shared.Next(90));
            var entityTypes = new[] { "Enterprise", "MunicipalAccount", "BudgetEntry", "Transaction" };
            var actions = new[] { "Created", "Updated", "Deleted" };

            auditEntries.Add(new AuditEntry
            {
                EntityType = entityTypes[Random.Shared.Next(entityTypes.Length)],
                EntityId = Random.Shared.Next(1, 100),
                Action = actions[Random.Shared.Next(actions.Length)],
                User = $"user{Random.Shared.Next(1, 5)}",
                Timestamp = timestamp,
                OldValues = Random.Shared.Next(2) == 0 ? "{\"oldField\": \"oldValue\"}" : null,
                NewValues = "{\"newField\": \"newValue\"}"
            });
        }

        await dbContext.AuditEntries.AddRangeAsync(auditEntries);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Normalizes a raw account number into the short form expected by BudgetEntry (e.g., "401" or "410.1").
    /// This helps maintain compatibility between full Chart of Accounts codes and the simplified budget code used in tests.
    /// </summary>
    private static string NormalizeBudgetAccountNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "000";

        // If it already matches the target format, return as-is
        if (Regex.IsMatch(raw, @"^\d{3}(\.\d{1,2})?$")) return raw;

        // Try to capture a leading 3-digit code (e.g., "401" from "401-1000-100")
        var m = Regex.Match(raw, @"(\d{3}(\.\d{1,2})?)");
        if (m.Success) return m.Groups[1].Value;

        // Fallback: extract digits and build a 3-digit code
        var digits = Regex.Replace(raw, @"\D", "");
        if (digits.Length >= 3) return digits.Substring(0, 3);
        if (digits.Length > 0) return digits.PadLeft(3, '0');

        // Last resort default
        return "000";
    }

    /// <summary>
    /// Gets a random department code for test data
    /// </summary>
    private static string GetRandomDepartmentCode()
    {
        var codes = new[] { "PW", "WS", "FIN", "ADMIN", "FIRE", "POLICE" };
        return codes[Random.Shared.Next(codes.Length)];
    }

    /// <summary>
    /// Creates a single enterprise with related data for focused testing
    /// </summary>
    public static async Task<Enterprise> CreateTestEnterpriseAsync(AppDbContext dbContext, string name = "Test Enterprise", string type = "Water")
    {
        var enterprise = new Enterprise
        {
            Name = name,
            Type = type,
            CurrentRate = 50.00m,
            CitizenCount = 10000,
            MonthlyExpenses = 50000m,
            Status = EnterpriseStatus.Active
        };

        await dbContext.Enterprises.AddAsync(enterprise);
        await dbContext.SaveChangesAsync();

        return enterprise;
    }

    /// <summary>
    /// Creates budget entries for a specific enterprise
    /// </summary>
    public static async Task CreateEnterpriseBudgetAsync(AppDbContext dbContext, int enterpriseId, int fiscalYear)
    {
        var enterprise = await dbContext.Enterprises.FindAsync(enterpriseId);
        if (enterprise == null) return;

        var accounts = await dbContext.Set<MunicipalAccount>()
            .Where(a => a.Fund.ToString() == enterprise.Type)
            .ToListAsync();

        var budgetEntries = new List<BudgetEntry>();
        foreach (var account in accounts)
        {
            var accountNumber = NormalizeBudgetAccountNumber(account.AccountNumber?.Value ?? account.AccountNumber_Value ?? account.Name ?? $"ACC-{account.Id}");
            var departmentId = account.DepartmentId != 0 ? account.DepartmentId : (await dbContext.Departments.Select(d => d.Id).FirstAsync());

            budgetEntries.Add(new BudgetEntry
            {
                AccountNumber = accountNumber,
                FiscalYear = fiscalYear,
                StartPeriod = new DateTime(fiscalYear - 1, 7, 1),
                EndPeriod = new DateTime(fiscalYear, 6, 30),
                BudgetedAmount = account.BudgetAmount / 12, // Monthly budget
                ActualAmount = (account.BudgetAmount / 12) * 0.95m, // 95% of budget spent
                Description = $"{account.Name} - FY{fiscalYear}",
                DepartmentId = departmentId,
                MunicipalAccountId = account.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.BudgetEntries.AddRangeAsync(budgetEntries);
        await dbContext.SaveChangesAsync();
    }
}
