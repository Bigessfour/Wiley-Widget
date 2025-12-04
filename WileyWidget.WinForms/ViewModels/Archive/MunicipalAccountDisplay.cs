namespace WileyWidget.WinForms.ViewModels.Archive
{
    /// <summary>
    /// Lightweight display DTO used by the UI for listing and editing municipal accounts.
    /// Separated from AccountsViewModel to make the type available at the namespace level for forms.
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