using System.ComponentModel;
using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// KPI tile data for dashboard gauges and cards
    /// </summary>
    public class DashboardKpi
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = "$";
        public decimal Target { get; set; }
        public decimal PreviousValue { get; set; }
        public TrendDirection Trend { get; set; }
        public double ChangePercent => PreviousValue > 0 ? (double)((Value - PreviousValue) / PreviousValue * 100) : 0;
        public string Description { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public bool IsPrimary { get; set; } // Large tile vs small
    }

    public enum TrendDirection
    {
        [Description("📈")] Up,
        [Description("📉")] Down,
        [Description("➡️")] Stable
    }
}