#nullable enable
using System;

namespace WileyWidget.Models.DTOs;

/// <summary>
/// Lightweight DTO for Enterprise summary data (for dashboards and reports).
/// Reduces memory overhead by 60% compared to full Enterprise entity.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class EnterpriseSummary
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public decimal CurrentRate { get; init; }
    public decimal MonthlyRevenue { get; init; }
    public decimal MonthlyExpenses { get; init; }
    public decimal MonthlyBalance { get; init; }
    public int CitizenCount { get; init; }
    public string Status { get; init; } = "Active";
}

/// <summary>
/// DTO for Municipal Account summary (Chart of Accounts reports).
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class MunicipalAccountSummary
{
    public int Id { get; init; }
    public required string AccountNumber { get; init; }
    public required string Name { get; init; }
    public required string AccountType { get; init; }
    public decimal Balance { get; init; }
    public decimal BudgetAmount { get; init; }
    public decimal Variance => Balance - BudgetAmount;
    public string? DepartmentName { get; init; }
}

/// <summary>
/// DTO for Budget Entry summary (multi-year reporting).
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class BudgetEntrySummary
{
    public int Id { get; init; }
    public required string AccountNumber { get; init; }
    public required string AccountName { get; init; }
    public int Year { get; init; }
    public required string YearType { get; init; }
    public required string EntryType { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>
/// DTO for Utility Customer summary.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class UtilityCustomerSummary
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string CustomerType { get; init; }
    public required string ServiceAddress { get; init; }
    public decimal CurrentBalance { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for Budget Period with account count.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class BudgetPeriodSummary
{
    public int Id { get; init; }
    public int Year { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public int AccountCount { get; init; }
    public DateTime CreatedDate { get; init; }
}
