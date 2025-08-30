using System.Collections.ObjectModel;
using System.Windows.Media;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for BudgetAnalytics models including BudgetMetrics and BudgetInsights
/// </summary>
public class BudgetAnalyticsTests
{
    [Fact]
    public void BudgetMetrics_CalculatesMonthlyBalanceCorrectly()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 10000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };

        // Act & Assert
        Assert.Equal(2500m, metrics.MonthlyBalance);
    }

    [Fact]
    public void BudgetMetrics_ReturnsCorrectStatus_Surplus()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 10000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };

        // Act & Assert
        Assert.Equal("Surplus", metrics.Status);
        Assert.Equal("📈", metrics.StatusIcon);
        Assert.Contains("Surplus: $2500.00", metrics.StatusMessage);
    }

    [Fact]
    public void BudgetMetrics_ReturnsCorrectStatus_Deficit()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 5000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };

        // Act & Assert
        Assert.Equal("Deficit", metrics.Status);
        Assert.Equal("📉", metrics.StatusIcon);
        Assert.Contains("Deficit: $2500.00", metrics.StatusMessage);
    }

    [Fact]
    public void BudgetMetrics_ReturnsCorrectStatus_BreakEven()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 7500m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };

        // Act & Assert
        Assert.Equal("Break-even", metrics.Status);
        Assert.Equal("⚖️", metrics.StatusIcon);
        Assert.Contains("Break-even: $0.00", metrics.StatusMessage);
    }

    [Fact]
    public void BudgetMetrics_StatusBackground_ReturnsCorrectColor()
    {
        // Arrange & Act
        var surplusMetrics = new BudgetMetrics { TotalRevenue = 10000m, TotalExpenses = 7500m };
        var deficitMetrics = new BudgetMetrics { TotalRevenue = 5000m, TotalExpenses = 7500m };
        var breakEvenMetrics = new BudgetMetrics { TotalRevenue = 7500m, TotalExpenses = 7500m };

        // Assert
        Assert.Equal("#FFE8F5E8", ((SolidColorBrush)surplusMetrics.StatusBackground).Color.ToString()); // Light green
        Assert.Equal("#FFFFEBEE", ((SolidColorBrush)deficitMetrics.StatusBackground).Color.ToString()); // Light red
        Assert.Equal("#FFFFF8E1", ((SolidColorBrush)breakEvenMetrics.StatusBackground).Color.ToString()); // Light yellow
    }

    [Fact]
    public void BudgetInsights_GeneratesCorrectInsights_ForSurplus()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 10000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };
        var insights = new BudgetInsights();

        // Act
        insights.GenerateInsights(metrics);

        // Assert
        Assert.Contains("Surplus Alert", insights.MainInsight);
        Assert.Contains("reinvesting surplus", insights.Recommendations[0]);
        Assert.Contains("emergency reserves", insights.Recommendations[2]);
    }

    [Fact]
    public void BudgetInsights_GeneratesCorrectInsights_ForDeficit()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 5000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };
        var insights = new BudgetInsights();

        // Act
        insights.GenerateInsights(metrics);

        // Assert
        Assert.Contains("Deficit Alert", insights.MainInsight);
        Assert.Contains("rate adjustments", insights.Recommendations[0]);
        Assert.Contains("expense reduction", insights.Recommendations[1]);
    }

    [Fact]
    public void BudgetInsights_IncludesCitizenAverageRevenue()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 10000m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };
        var insights = new BudgetInsights();

        // Act
        insights.GenerateInsights(metrics);

        // Assert
        Assert.Contains("$10.00/month", string.Join(" ", insights.Recommendations));
    }

    [Fact]
    public void BudgetInsights_AlwaysIncludesGeneralRecommendations()
    {
        // Arrange
        var metrics = new BudgetMetrics
        {
            TotalRevenue = 7500m,
            TotalExpenses = 7500m,
            TotalCitizens = 1000
        };
        var insights = new BudgetInsights();

        // Act
        insights.GenerateInsights(metrics);

        // Assert
        var allRecommendations = string.Join(" ", insights.Recommendations);
        Assert.Contains("quarterly", allRecommendations);
        Assert.Contains("performance metrics", allRecommendations);
        Assert.Contains("community feedback", allRecommendations);
    }
}
