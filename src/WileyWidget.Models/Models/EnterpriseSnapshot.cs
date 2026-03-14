namespace WileyWidget.Models;

public class EnterpriseSnapshot
{
    public string Name { get; set; } = string.Empty;        // "Water", "Sewer", etc.
    public string DisplayCategory { get; set; } = "Utility";
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal PriorYearRevenue { get; set; }
    public decimal PriorYearExpenses { get; set; }
    public decimal CurrentYearEstimatedRevenue { get; set; }
    public decimal CurrentYearEstimatedExpenses { get; set; }
    public decimal BudgetYearRevenue { get; set; }
    public decimal BudgetYearExpenses { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal RecommendedRate { get; set; }
    public decimal ReserveCoverageMonths { get; set; }
    public decimal TargetReserveCoverageMonths { get; set; } = 6m;
    public decimal DebtServiceRatio { get; set; }
    public decimal NetPosition => Revenue - Expenses;
    public decimal CoverageRatio => Expenses > 0 ? Revenue / Expenses : 0m;
    public decimal CoveragePercent => CoverageRatio * 100m;
    public double BreakEvenRatio => (double)CoveragePercent;
    public bool IsSelfSustaining => NetPosition >= 0;
    public decimal OperatingMarginPercent => Revenue != 0 ? (NetPosition / Revenue) * 100m : 0m;
    public decimal RequiredRateIncrease => RecommendedRate > CurrentRate ? RecommendedRate - CurrentRate : 0m;
    public decimal RateAdequacyPercent => RecommendedRate > 0 ? (CurrentRate / RecommendedRate) * 100m : 0m;
    public decimal DebtServiceCoverageRatio => DebtServiceRatio > 0 ? 1m / DebtServiceRatio : 0m;
    public bool RequiresAttention => CoverageRatio < 1m
        || (ReserveCoverageMonths > 0 && ReserveCoverageMonths < TargetReserveCoverageMonths)
        || DebtServiceRatio > 0.20m;
    public string CrossSubsidyNote { get; set; } = "Self-funded";
    public string InsightSummary { get; set; } = string.Empty;
}
