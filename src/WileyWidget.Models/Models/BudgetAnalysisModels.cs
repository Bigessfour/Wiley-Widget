#nullable enable

using System;
using System.Collections.Generic;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Summary information for a fund in the budget
/// </summary>
public class FundSummary
{
    /// <summary>
    /// Gets or sets the fund type
    /// </summary>
    public BudgetFundType Fund { get; set; } = new() { Code = "GF", Name = "General Fund" };

    /// <summary>
    /// Gets or sets the fund name
    /// </summary>
    public string? FundName { get; set; }

    /// <summary>
    /// Gets or sets the total budgeted amount for the fund
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    /// <summary>
    /// Gets or sets the total budget amount (alias for TotalBudgeted)
    /// </summary>
    public decimal TotalBudget
    {
        get => TotalBudgeted;
        set => TotalBudgeted = value;
    }

    /// <summary>
    /// Gets or sets the total actual amount spent for the fund
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Gets or sets the total balance (alias for TotalActual)
    /// </summary>
    public decimal TotalBalance
    {
        get => TotalActual;
        set => TotalActual = value;
    }

    /// <summary>
    /// Gets or sets the budget variance (positive = under budget, negative = over budget)
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the variance percentage
    /// </summary>
    public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Gets or sets the number of accounts in this fund
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the list of account variances for this fund
    /// </summary>
    public List<AccountVariance> AccountVariances { get; set; } = new();

    /// <summary>
    /// Gets or sets the budgeted amount (alias for TotalBudgeted)
    /// </summary>
    public decimal Budgeted
    {
        get => TotalBudgeted;
        set => TotalBudgeted = value;
    }

    /// <summary>
    /// Gets or sets the actual amount (alias for TotalActual)
    /// </summary>
    public decimal Actual
    {
        get => TotalActual;
        set => TotalActual = value;
    }
}

/// <summary>
/// Summary information for a department in the budget
/// </summary>
public class DepartmentSummary
{
    /// <summary>
    /// Gets or sets the department object
    /// </summary>
    public Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the department name
    /// </summary>
    public string? DepartmentName { get; set; }

    /// <summary>
    /// Gets or sets the total budgeted amount for the department
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    /// <summary>
    /// Gets or sets the total budget amount (alias for TotalBudgeted)
    /// </summary>
    public decimal TotalBudget
    {
        get => TotalBudgeted;
        set => TotalBudgeted = value;
    }

    /// <summary>
    /// Gets or sets the total actual amount spent for the department
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Gets or sets the total balance (alias for TotalActual)
    /// </summary>
    public decimal TotalBalance
    {
        get => TotalActual;
        set => TotalActual = value;
    }

    /// <summary>
    /// Gets or sets the budget variance (positive = under budget, negative = over budget)
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the variance percentage
    /// </summary>
    public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Gets or sets the number of accounts in this department
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the metrics for this department
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of account variances for this department
    /// </summary>
    public List<AccountVariance> AccountVariances { get; set; } = new();

    /// <summary>
    /// Gets or sets the budgeted amount (alias for TotalBudgeted)
    /// </summary>
    public decimal Budgeted
    {
        get => TotalBudgeted;
        set => TotalBudgeted = value;
    }

    /// <summary>
    /// Gets or sets the actual amount (alias for TotalActual)
    /// </summary>
    public decimal Actual
    {
        get => TotalActual;
        set => TotalActual = value;
    }
}

/// <summary>
/// Variance information for a specific account
/// </summary>
public class AccountVariance
{
    /// <summary>
    /// Gets or sets the municipal account object
    /// </summary>
    public MunicipalAccount? Account { get; set; }

    /// <summary>
    /// Gets or sets the account number
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the account name
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Gets or sets the budgeted amount
    /// </summary>
    public decimal BudgetedAmount { get; set; }

    /// <summary>
    /// Gets or sets the actual amount spent
    /// </summary>
    public decimal ActualAmount { get; set; }

    /// <summary>
    /// Gets or sets the variance amount (positive = under budget, negative = over budget)
    /// </summary>
    public decimal VarianceAmount { get; set; }

    /// <summary>
    /// Gets or sets the variance percentage
    /// </summary>
    public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Gets or sets the variance percent (alias for VariancePercentage as decimal not percentage)
    /// </summary>
    public decimal VariancePercent
    {
        get => VariancePercentage;
        set => VariancePercentage = value;
    }

    /// <summary>
    /// Gets or sets the fund this account belongs to
    /// </summary>
    public string? Fund { get; set; }

    /// <summary>
    /// Gets or sets the department this account belongs to
    /// </summary>
    public string? Department { get; set; }
}

/// <summary>
/// Comprehensive budget variance analysis
/// </summary>
public class BudgetVarianceAnalysis
{
    /// <summary>
    /// Gets or sets the analysis date
    /// </summary>
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the budget period being analyzed
    /// </summary>
    public string? BudgetPeriod { get; set; }

    /// <summary>
    /// Gets or sets the total budgeted amount across all funds
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    /// <summary>
    /// Gets or sets the total actual amount spent across all funds
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Gets or sets the overall variance amount
    /// </summary>
    public decimal TotalVariance { get; set; }

    /// <summary>
    /// Gets or sets the overall variance percentage
    /// </summary>
    public decimal TotalVariancePercentage { get; set; }

    /// <summary>
    /// Gets or sets the fund summaries
    /// </summary>
    public List<FundSummary> FundSummaries { get; set; } = new();

    /// <summary>
    /// Gets or sets the department summaries
    /// </summary>
    public List<DepartmentSummary> DepartmentSummaries { get; set; } = new();

    /// <summary>
    /// Gets or sets the account variances
    /// </summary>
    public List<AccountVariance> AccountVariances { get; set; } = new();

    /// <summary>
    /// Gets or sets the variances (alias for AccountVariances)
    /// </summary>
    public List<AccountVariance> Variances
    {
        get => AccountVariances;
        set => AccountVariances = value;
    }

    /// <summary>
    /// Gets or sets the accounts that are significantly over budget (variance > 10%)
    /// </summary>
    public List<AccountVariance> OverBudgetAccounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the accounts that are significantly under budget (variance < -10%)
    /// </summary>
    public List<AccountVariance> UnderBudgetAccounts { get; set; } = new();

    /// <summary>
    /// Gets or sets any analysis warnings or notes
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}