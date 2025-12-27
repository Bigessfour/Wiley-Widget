#nullable enable
using System;

namespace WileyWidget.Models.DTOs;

/// <summary>
/// Lightweight DTO for Enterprise summary data (for dashboards and reports)
/// Reduces memory overhead by 60% compared to full Enterprise entity
/// </summary>
/// <summary>
/// Represents a class for enterprisesummary.
/// </summary>
public class EnterpriseSummary
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the currentrate.
    /// </summary>
    public decimal CurrentRate { get; set; }
    /// <summary>
    /// Gets or sets the monthlyrevenue.
    /// </summary>
    public decimal MonthlyRevenue { get; set; }
    /// <summary>
    /// Gets or sets the monthlyexpenses.
    /// </summary>
    public decimal MonthlyExpenses { get; set; }
    /// <summary>
    /// Gets or sets the monthlybalance.
    /// </summary>
    public decimal MonthlyBalance { get; set; }
    /// <summary>
    /// Gets or sets the citizencount.
    /// </summary>
    public int CitizenCount { get; set; }
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = "Active";
}

/// <summary>
/// DTO for Municipal Account summary (Chart of Accounts reports)
/// </summary>
/// <summary>
/// Represents a class for municipalaccountsummary.
/// </summary>
public class MunicipalAccountSummary
{
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the accountnumber.
    /// </summary>
    /// <summary>
    /// Gets or sets the accountnumber.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the accounttype.
    /// </summary>
    public string AccountType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the balance.
    /// </summary>
    public decimal Balance { get; set; }
    /// <summary>
    /// Gets or sets the budgetamount.
    /// </summary>
    public decimal BudgetAmount { get; set; }
    public decimal Variance => Balance - BudgetAmount;
    public string? DepartmentName { get; set; }
}

/// <summary>
/// DTO for Budget Entry summary (multi-year reporting)
/// </summary>
/// <summary>
/// Represents a class for budgetentrysummary.
/// </summary>
public class BudgetEntrySummary
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the accountname.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    public int Year { get; set; }
    /// <summary>
    /// Gets or sets the yeartype.
    /// </summary>
    public string YearType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the entrytype.
    /// </summary>
    public string EntryType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// DTO for Utility Customer summary
/// </summary>
/// <summary>
/// Represents a class for utilitycustomersummary.
/// </summary>
public class UtilityCustomerSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the customertype.
    /// </summary>
    public string CustomerType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the serviceaddress.
    /// </summary>
    public string ServiceAddress { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the currentbalance.
    /// </summary>
    public decimal CurrentBalance { get; set; }
    /// <summary>
    /// Gets or sets the isactive.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for Department hierarchy
/// </summary>
/// <summary>
/// Represents a class for departmentsummary.
/// </summary>
public class DepartmentSummary
{
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the fund.
    /// </summary>
    public string Fund { get; set; } = string.Empty;
    public int? ParentDepartmentId { get; set; }
    public string? ParentDepartmentName { get; set; }
}

/// <summary>
/// DTO for Budget Period with account count
/// </summary>
/// <summary>
/// Represents a class for budgetperiodsummary.
/// </summary>
public class BudgetPeriodSummary
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the accountcount.
    /// </summary>
    public int AccountCount { get; set; }
    /// <summary>
    /// Gets or sets the createddate.
    /// </summary>
    public DateTime CreatedDate { get; set; }
}
