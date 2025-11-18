namespace WileyWidget.Models;

/// <summary>
/// Fund types following GASB 34 standards
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
