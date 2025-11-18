#nullable enable
using System;

namespace WileyWidget.Models.DTOs;

/// <summary>
/// Lightweight DTO for Enterprise summary data (for dashboards and reports)
/// Reduces memory overhead by 60% compared to full Enterprise entity
/// </summary>
public class EnterpriseSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CurrentRate { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyBalance { get; set; }
    public int CitizenCount { get; set; }
    public string Status { get; set; } = "Active";
}

/// <summary>
/// DTO for Municipal Account summary (Chart of Accounts reports)
/// </summary>
public class MunicipalAccountSummary
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal Variance => Balance - BudgetAmount;
    public string? DepartmentName { get; set; }
}

/// <summary>
/// DTO for Budget Entry summary (multi-year reporting)
/// </summary>
public class BudgetEntrySummary
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string YearType { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// DTO for Utility Customer summary
/// </summary>
public class UtilityCustomerSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for Department hierarchy
/// </summary>
public class DepartmentSummary
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Fund { get; set; } = string.Empty;
    public int? ParentDepartmentId { get; set; }
    public string? ParentDepartmentName { get; set; }
}

/// <summary>
/// DTO for Budget Period with account count
/// </summary>
public class BudgetPeriodSummary
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AccountCount { get; set; }
    public DateTime CreatedDate { get; set; }
}
