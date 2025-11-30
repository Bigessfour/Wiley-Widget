using System;
using System.Linq;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Configuration
{
    public static class DemoDataSeeder
    {
        public static void SeedDemoData(AppDbContext dbContext)
        {
            if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));

            // If the database already has data, do nothing.
            if (dbContext.MunicipalAccounts.Any())
            {
                return;
            }

            // Log that we're seeding demo data into the configured database.
            try
            {
                Serilog.Log.Information("Seeding initial demo data into configured database.");
            }
            catch
            {
                // Best-effort logging — don't let logging failures break seeding
            }

            var now = DateTime.UtcNow;

            // Create budget period
            var budgetPeriod = new BudgetPeriod
            {
                Year = now.Year,
                StartDate = new DateTime(now.Year, 1, 1),
                EndDate = new DateTime(now.Year, 12, 31),
                Status = BudgetStatus.Adopted
            };
            dbContext.BudgetPeriods.Add(budgetPeriod);
            dbContext.SaveChanges();

            // Create department
            var dept = new Department
            {
                Name = "General Fund",
                DepartmentCode = "GEN"
            };
            dbContext.Departments.Add(dept);
            dbContext.SaveChanges();

            // Create demo accounts
            var accounts = new[]
            {
                new MunicipalAccount
                {
                    AccountNumber = new AccountNumber("1000-100"),
                    Name = "Cash - Operating",
                    Type = AccountType.Asset,
                    Fund = MunicipalFundType.General,
                    Balance = 150000m,
                    BudgetAmount = 150000m,
                    IsActive = true,
                    DepartmentId = dept.Id,
                    BudgetPeriodId = budgetPeriod.Id
                },
                new MunicipalAccount
                {
                    AccountNumber = new AccountNumber("2000-100"),
                    Name = "Accounts Payable",
                    Type = AccountType.Payables,
                    Fund = MunicipalFundType.General,
                    Balance = 25000m,
                    BudgetAmount = 25000m,
                    IsActive = true,
                    DepartmentId = dept.Id,
                    BudgetPeriodId = budgetPeriod.Id
                },
                new MunicipalAccount
                {
                    AccountNumber = new AccountNumber("3000-100"),
                    Name = "Property Tax Revenue",
                    Type = AccountType.Revenue,
                    Fund = MunicipalFundType.General,
                    Balance = 500000m,
                    BudgetAmount = 500000m,
                    IsActive = true,
                    DepartmentId = dept.Id,
                    BudgetPeriodId = budgetPeriod.Id
                },
                new MunicipalAccount
                {
                    AccountNumber = new AccountNumber("5000-100"),
                    Name = "Salaries & Wages",
                    Type = AccountType.Expense,
                    Fund = MunicipalFundType.General,
                    Balance = 300000m,
                    BudgetAmount = 300000m,
                    IsActive = true,
                    DepartmentId = dept.Id,
                    BudgetPeriodId = budgetPeriod.Id
                }
            };

            dbContext.MunicipalAccounts.AddRange(accounts);
            dbContext.SaveChanges();
        }
    }
}
