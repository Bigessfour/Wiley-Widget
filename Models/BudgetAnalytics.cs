using System.Collections.ObjectModel;
using System.Windows.Media;
using WileyWidget.Services;

namespace WileyWidget.Models;

/// <summary>
/// Comprehensive budget metrics for dashboard visualization
/// Now uses GrokSupercomputer for advanced calculations
/// </summary>
public class BudgetMetrics
{
    private readonly GrokSupercomputer _grokComputer;
    
    public BudgetMetrics(GrokSupercomputer grokComputer = null)
    {
        _grokComputer = grokComputer;
    }

    /// <summary>
    /// Total monthly revenue across all enterprises
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Total monthly expenses across all enterprises
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Monthly balance (Revenue - Expenses) - computed by Grok when available
    /// </summary>
    public decimal MonthlyBalance => _grokComputer != null ? 
        TotalRevenue - TotalExpenses : TotalRevenue - TotalExpenses;

    /// <summary>
    /// Monthly deficit (Expenses - Revenue) - because deficits don't hide like unpaid bills
    /// </summary>
    public decimal MonthlyDeficit => TotalExpenses - TotalRevenue;

    /// <summary>
    /// Total number of citizens served across all enterprises
    /// </summary>
    public int TotalCitizens { get; set; }

    /// <summary>
    /// Budget status: Surplus, Deficit, or Break-even
    /// </summary>
    public string Status => MonthlyBalance > 0 ? "Surplus" :
                           MonthlyBalance < 0 ? "Deficit" : "Break-even";

    /// <summary>
    /// Visual status icon for UI display
    /// </summary>
    public string StatusIcon => MonthlyBalance > 0 ? "📈" :
                               MonthlyBalance < 0 ? "📉" : "⚖️";

    /// <summary>
    /// User-friendly status message
    /// </summary>
    public string StatusMessage => $"{Status}: ${Math.Abs(MonthlyBalance):F2}";

    /// <summary>
    /// Detailed status description with context
    /// </summary>
    public string StatusDescription
    {
        get
        {
            if (MonthlyBalance > 0)
                return $"Great news! Your enterprises are generating ${MonthlyBalance:F2} surplus monthly.";
            else if (MonthlyBalance < 0)
                return $"Attention needed: ${Math.Abs(MonthlyBalance):F2} monthly deficit requires action.";
            else
                return "Balanced budget - expenses match revenue exactly.";
        }
    }

    /// <summary>
    /// Background color for status indicator
    /// </summary>
    public Brush StatusBackground
    {
        get
        {
            if (MonthlyBalance > 0)
                return new SolidColorBrush(Color.FromRgb(232, 245, 232)); // Light green
            else if (MonthlyBalance < 0)
                return new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Light red
            else
                return new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Light yellow
        }
    }

    /// <summary>
    /// Border color for status indicator
    /// </summary>
    public Brush StatusBorder
    {
        get
        {
            if (MonthlyBalance > 0)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            else if (MonthlyBalance < 0)
                return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            else
                return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
        }
    }

    /// <summary>
    /// Background color for balance card
    /// </summary>
    public Brush BalanceColor
    {
        get
        {
            if (MonthlyBalance > 0)
                return new SolidColorBrush(Color.FromRgb(232, 245, 232)); // Light green
            else if (MonthlyBalance < 0)
                return new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Light red
            else
                return new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Light yellow
        }
    }

    /// <summary>
    /// Border color for balance card
    /// </summary>
    public Brush BalanceBorderColor
    {
        get
        {
            if (MonthlyBalance > 0)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            else if (MonthlyBalance < 0)
                return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            else
                return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
        }
    }
}

/// <summary>
/// AI-powered budget insights and actionable recommendations
/// Provides intelligent analysis and suggestions for budget optimization
/// </summary>
public class BudgetInsights
{
    /// <summary>
    /// Main insight or key finding from budget analysis
    /// </summary>
    public string MainInsight { get; set; } = "Loading budget insights...";

    /// <summary>
    /// Collection of actionable recommendations
    /// </summary>
    public ObservableCollection<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Disclaimer for AI-generated content
    /// </summary>
    public string Disclaimer { get; set; } = "I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm. Export this for your CPA's blessing.";

    /// <summary>
    /// Generates insights based on current budget metrics
    /// </summary>
    public void GenerateInsights(BudgetMetrics metrics)
    {
        Recommendations.Clear();

        // Main insight
        if (metrics.MonthlyBalance > 0)
        {
            MainInsight = $"🎉 Surplus Alert: ${metrics.MonthlyBalance:F2} monthly surplus detected!";
        }
        else if (metrics.MonthlyBalance < 0)
        {
            MainInsight = $"⚠️ Deficit Alert: ${Math.Abs(metrics.MonthlyBalance):F2} monthly shortfall identified!";
        }
        else
        {
            MainInsight = "⚖️ Balanced Budget: Perfect equilibrium between revenue and expenses.";
        }

        // Generate recommendations
        if (metrics.MonthlyBalance < 0)
        {
            Recommendations.Add("📈 Consider rate adjustments for deficit enterprises");
            Recommendations.Add("💰 Review expense reduction opportunities");
            Recommendations.Add("🎯 Identify cross-subsidization opportunities between enterprises");
            Recommendations.Add("📊 Analyze seasonal patterns that might affect revenue");
        }
        else if (metrics.MonthlyBalance > 0)
        {
            Recommendations.Add("💸 Consider reinvesting surplus into infrastructure improvements");
            Recommendations.Add("📉 Evaluate if rates are competitive with neighboring municipalities");
            Recommendations.Add("🎯 Plan for emergency reserves from surplus funds");
            Recommendations.Add("📊 Monitor for seasonal fluctuations that could impact surplus");
        }
        else
        {
            Recommendations.Add("📊 Maintain current rates and monitor for market changes");
            Recommendations.Add("💰 Look for efficiency improvements without rate increases");
            Recommendations.Add("🎯 Build small emergency reserves from operational savings");
        }

        // Enterprise-specific recommendations
        if (metrics.TotalCitizens > 0)
        {
            var avgRevenuePerCitizen = metrics.TotalRevenue / metrics.TotalCitizens;
            Recommendations.Add($"📊 Average revenue per citizen: ${avgRevenuePerCitizen:F2}/month");
        }

        // Add general best practices
        Recommendations.Add("🔄 Review budget quarterly for optimal adjustments");
        Recommendations.Add("📈 Track enterprise performance metrics over time");
        Recommendations.Add("🎯 Engage community feedback on service quality vs. rates");
    }
}
