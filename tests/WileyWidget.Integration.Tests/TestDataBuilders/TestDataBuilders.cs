using System;
using System.Collections.Generic;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Integration.Tests.TestDataBuilders;

/// <summary>
/// Fluent builder for creating Enterprise entities with realistic test data
/// </summary>
public class EnterpriseBuilder
{
    private readonly Enterprise _enterprise;

    public EnterpriseBuilder()
    {
        _enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            Type = "Water",
            CurrentRate = 50.00m,
            CitizenCount = 10000,
            MonthlyExpenses = 50000m,
            Status = EnterpriseStatus.Active
        };
    }

    public EnterpriseBuilder WithName(string name)
    {
        _enterprise.Name = name;
        return this;
    }

    public EnterpriseBuilder WithType(string type)
    {
        _enterprise.Type = type;
        return this;
    }

    public EnterpriseBuilder WithRate(decimal rate)
    {
        _enterprise.CurrentRate = rate;
        return this;
    }

    public EnterpriseBuilder WithCitizenCount(int count)
    {
        _enterprise.CitizenCount = count;
        return this;
    }

    public EnterpriseBuilder WithMonthlyExpenses(decimal expenses)
    {
        _enterprise.MonthlyExpenses = expenses;
        return this;
    }

    public EnterpriseBuilder WithMonthlyRevenue(decimal revenue)
    {
        _enterprise.CurrentRate = revenue / _enterprise.CitizenCount;
        return this;
    }

    public EnterpriseBuilder Inactive()
    {
        _enterprise.Status = EnterpriseStatus.Inactive;
        return this;
    }

    public EnterpriseBuilder CreatedOn(DateTime date)
    {
        _enterprise.CreatedDate = date;
        return this;
    }

    public Enterprise Build()
    {
        return _enterprise;
    }
}

/// <summary>
/// Fluent builder for creating MunicipalAccount entities
/// </summary>
public class AccountBuilder
{
    private readonly MunicipalAccount _account;

    public AccountBuilder()
    {
        _account = new MunicipalAccount
        {
            AccountNumber_Value = $"TEST-{Guid.NewGuid().ToString().Substring(0, 8)}",
            Name = "Test Account",
            Type = AccountType.Revenue,
            Fund = MunicipalFundType.General,
            FundDescription = "General Fund",
            BudgetAmount = 100000m,
            IsActive = true
        };
    }

    public AccountBuilder WithAccountNumber(string accountNumber)
    {
        _account.AccountNumber_Value = accountNumber;
        return this;
    }

    public AccountBuilder WithName(string name)
    {
        _account.Name = name;
        return this;
    }

    public AccountBuilder WithType(AccountType type)
    {
        _account.Type = type;
        return this;
    }

    public AccountBuilder WithFund(MunicipalFundType fund)
    {
        _account.Fund = fund;
        _account.FundDescription = $"{fund} Fund";
        return this;
    }

    public AccountBuilder WithBudgetAmount(decimal amount)
    {
        _account.BudgetAmount = amount;
        return this;
    }

    public AccountBuilder Inactive()
    {
        _account.IsActive = false;
        return this;
    }

    public MunicipalAccount Build()
    {
        return _account;
    }
}

/// <summary>
/// Fluent builder for creating BudgetEntry entities
/// </summary>
public class BudgetBuilder
{
    private readonly BudgetEntry _budgetEntry;

    public BudgetBuilder()
    {
        var now = DateTime.Now;
        _budgetEntry = new BudgetEntry
        {
            AccountNumber = "TEST-001",
            FiscalYear = now.Year,
            StartPeriod = new DateTime(now.Year - 1, 7, 1),
            EndPeriod = new DateTime(now.Year, 6, 30),
            BudgetedAmount = 10000m,
            ActualAmount = 9500m,
            Description = "Test Budget Entry"
        };
    }

    public BudgetBuilder WithAccountNumber(string accountNumber)
    {
        _budgetEntry.AccountNumber = accountNumber;
        return this;
    }

    public BudgetBuilder WithFiscalYear(int year)
    {
        _budgetEntry.FiscalYear = year;
        _budgetEntry.StartPeriod = new DateTime(year - 1, 7, 1);
        _budgetEntry.EndPeriod = new DateTime(year, 6, 30);
        return this;
    }

    public BudgetBuilder WithBudgetedAmount(decimal amount)
    {
        _budgetEntry.BudgetedAmount = amount;
        return this;
    }

    public BudgetBuilder WithActualAmount(decimal amount)
    {
        _budgetEntry.ActualAmount = amount;
        return this;
    }

    public BudgetBuilder WithDescription(string description)
    {
        _budgetEntry.Description = description;
        return this;
    }

    public BudgetBuilder WithDepartmentCode(string code)
    {
        _budgetEntry.DepartmentCode = code;
        return this;
    }

    public BudgetBuilder OverBudget()
    {
        _budgetEntry.ActualAmount = _budgetEntry.BudgetedAmount * 1.1m;
        return this;
    }

    public BudgetBuilder UnderBudget()
    {
        _budgetEntry.ActualAmount = _budgetEntry.BudgetedAmount * 0.9m;
        return this;
    }

    public BudgetEntry Build()
    {
        return _budgetEntry;
    }
}

/// <summary>
/// Builder for creating complex test scenarios with related entities
/// </summary>
public class TestScenarioBuilder
{
    private readonly List<Enterprise> _enterprises = new();
    private readonly List<MunicipalAccount> _accounts = new();
    private readonly List<BudgetEntry> _budgetEntries = new();
    private readonly List<Department> _departments = new();

    public TestScenarioBuilder WithEnterprise(Action<EnterpriseBuilder> builderAction)
    {
        var builder = new EnterpriseBuilder();
        builderAction(builder);
        _enterprises.Add(builder.Build());
        return this;
    }

    public TestScenarioBuilder WithAccount(Action<AccountBuilder> builderAction)
    {
        var builder = new AccountBuilder();
        builderAction(builder);
        _accounts.Add(builder.Build());
        return this;
    }

    public TestScenarioBuilder WithBudget(Action<BudgetBuilder> builderAction)
    {
        var builder = new BudgetBuilder();
        builderAction(builder);
        _budgetEntries.Add(builder.Build());
        return this;
    }

    public TestScenarioBuilder WithDepartment(string name, string code)
    {
        _departments.Add(new Department
        {
            Name = name,
            DepartmentCode = code
        });
        return this;
    }

    public TestScenarioBuilder WithWaterUtilityScenario()
    {
        WithEnterprise(e => e
            .WithName("City Water Department")
            .WithType("Water")
            .WithRate(45.50m)
            .WithCitizenCount(12500));

        WithAccount(a => a
            .WithAccountNumber("401-1000-100")
            .WithName("Water Revenue")
            .WithType(AccountType.Revenue)
            .WithFund(MunicipalFundType.Water)
            .WithBudgetAmount(5500000m));

        WithAccount(a => a
            .WithAccountNumber("501-2000-200")
            .WithName("Water Operating Expenses")
            .WithType(AccountType.Expense)
            .WithFund(MunicipalFundType.Water)
            .WithBudgetAmount(4200000m));

        WithBudget(b => b
            .WithAccountNumber("401-1000-100")
            .WithFiscalYear(DateTime.Now.Year)
            .WithBudgetedAmount(458333.33m) // Monthly budget
            .WithActualAmount(450000m));

        return this;
    }

    public TestScenarioBuilder WithOverBudgetScenario()
    {
        WithEnterprise(e => e
            .WithName("Over Budget Department")
            .WithType("General")
            .WithRate(30.00m)
            .WithCitizenCount(8000));

        WithAccount(a => a
            .WithAccountNumber("201-2000-200")
            .WithName("General Government Expenses")
            .WithType(AccountType.Expense)
            .WithFund(MunicipalFundType.General)
            .WithBudgetAmount(2000000m));

        WithBudget(b => b
            .WithAccountNumber("201-2000-200")
            .WithFiscalYear(DateTime.Now.Year)
            .OverBudget()); // 110% of budget

        return this;
    }

    public TestScenario Build()
    {
        return new TestScenario
        {
            Enterprises = _enterprises,
            Accounts = _accounts,
            BudgetEntries = _budgetEntries,
            Departments = _departments
        };
    }
}

/// <summary>
/// Container for a complete test scenario with related entities
/// </summary>
public class TestScenario
{
    public List<Enterprise> Enterprises { get; set; } = new();
    public List<MunicipalAccount> Accounts { get; set; } = new();
    public List<BudgetEntry> BudgetEntries { get; set; } = new();
    public List<Department> Departments { get; set; } = new();
}
