namespace WileyWidget.Models;

/// <summary>
/// Fund types following GASB 34 standards
/// </summary>
/// <summary>
/// Defines the fundtype enumeration.
/// </summary>
public enum FundType
{
    GeneralFund = 1,
    EnterpriseFund, // e.g., Utilities
    SpecialRevenue,
    CapitalProjects,
    DebtService,
    PermanentFund
}
