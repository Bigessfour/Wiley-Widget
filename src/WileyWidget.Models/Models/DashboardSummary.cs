using System.Collections.Generic;

namespace WileyWidget.Models
{
    /// <summary>
    /// Summary container for dashboard data
    /// </summary>
    public class DashboardSummary
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public string FiscalYear { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public List<DashboardMetric> Metrics { get; set; } = new();

        // Aggregated totals shown in gauges
        public decimal TotalBudget { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetPosition { get; set; }

        public bool IsLoading { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
