using System.ComponentModel;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Models;

/// <summary>
/// Display-friendly DTO for presentation in grids/lists.
/// Use attributes to control the grid column header and ordering.
/// </summary>
public sealed record MunicipalAccountDisplay
{
    [DisplayName("Account Number")]
    [GridDisplay(0, 140)]
    public string AccountNumber { get; init; } = string.Empty;

    [DisplayName("Account Name")]
    [GridDisplay(1, 320)]
    public string AccountName { get; init; } = string.Empty;

    [DisplayName("Department")]
    [GridDisplay(2, 220)]
    public string DepartmentName { get; init; } = string.Empty;

    [DisplayName("Fund")]
    [GridDisplay(3, 160)]
    public string FundType { get; init; } = string.Empty;

    [DisplayName("Type")]
    [GridDisplay(4, 160)]
    public string AccountType { get; init; } = string.Empty;

    [DisplayName("Current Balance")]
    [GridDisplay(5, 130, DecimalDigits = 2, Format = "C")]
    public decimal CurrentBalance { get; init; }

    [DisplayName("Budget Amount")]
    [GridDisplay(6, 130, DecimalDigits = 2, Format = "C")]
    public decimal BudgetAmount { get; init; }

    [DisplayName("Active")]
    [GridDisplay(7, 80)]
    public bool IsActive { get; init; }
}
