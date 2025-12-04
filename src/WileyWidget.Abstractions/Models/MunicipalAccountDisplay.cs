namespace WileyWidget.Abstractions.Models
{
    /// <summary>
    /// Lightweight display DTO used by UI components and services.
    /// Moved to Abstractions so both Business and WinForms projects may reference it without cycles.
    /// </summary>
    public class MunicipalAccountDisplay
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Fund { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal BudgetAmount { get; set; }
        public string Department { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool HasParent { get; set; }
    }
}