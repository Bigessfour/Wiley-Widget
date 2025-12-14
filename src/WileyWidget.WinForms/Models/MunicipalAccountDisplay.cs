using WileyWidget.Models;

namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// Display model for municipal accounts consumed by WinForms views.
    /// </summary>
    public record MunicipalAccountDisplay
    {
        public int Id { get; init; }
        public string AccountNumber { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;
        public string Name => AccountName;
        public string? Description { get; init; }
        public string AccountType { get; init; } = string.Empty;
        public string Type => AccountType;
        public string FundName { get; init; } = string.Empty;
        public string Fund => FundName;
        public decimal CurrentBalance { get; init; }
        public decimal Balance => CurrentBalance;
        public decimal BudgetAmount { get; init; }
        public string Department { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool HasParent { get; init; }
    }
}
