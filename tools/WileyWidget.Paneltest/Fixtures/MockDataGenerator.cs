using System;
using System.Collections.Generic;

namespace WileyWidget.Paneltest.Fixtures;

/// <summary>
/// Mock data generator for panel testing.
/// Provides realistic sample data for WarRoom, Analytics, and other panels.
/// </summary>
public class MockDataGenerator
{
    /// <summary>
    /// Generate sample scenario projection data for WarRoom panel.
    /// </summary>
    public static List<Dictionary<string, object>> GenerateWarRoomProjections(int count = 5)
    {
        var projections = new List<Dictionary<string, object>>();

        for (int i = 1; i <= count; i++)
        {
            projections.Add(new Dictionary<string, object>
            {
                { "Year", 2024 + i },
                { "Revenue", 100000 + (i * 25000) },
                { "Expenses", 60000 + (i * 15000) },
                { "NetIncome", 40000 + (i * 10000) },
                { "GrowthRate", 5.5 + (i * 0.5) },
                { "RiskScore", 2.5 - (i * 0.2) }
            });
        }

        return projections;
    }

    /// <summary>
    /// Generate sample activity log entries for ActivityLog panel.
    /// </summary>
    public static List<Dictionary<string, object>> GenerateActivityLogEntries(int count = 10)
    {
        var entries = new List<Dictionary<string, object>>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            entries.Add(new Dictionary<string, object>
            {
                { "Id", Guid.NewGuid() },
                { "Action", SampleActions[i % SampleActions.Count] },
                { "User", SampleUsers[i % SampleUsers.Count] },
                { "Timestamp", now.AddMinutes(-i * 5) },
                { "Details", $"Activity log entry {i + 1}" },
                { "Status", i % 3 == 0 ? "Success" : "Warning" }
            });
        }

        return entries;
    }

    /// <summary>
    /// Generate sample gauge data for analytics visualization.
    /// </summary>
    public static Dictionary<string, double> GenerateGaugeMetrics()
    {
        return new Dictionary<string, double>
        {
            { "BudgetUtilization", 72.5 },
            { "ForecastAccuracy", 88.3 },
            { "IncomeVariance", 15.2 },
            { "ExpenseControl", 92.1 },
            { "CashFlowRatio", 1.45 }
        };
    }

    /// <summary>
    /// Generate sample chart data for revenue trends.
    /// </summary>
    public static List<Dictionary<string, object>> GenerateChartData(int months = 12)
    {
        var data = new List<Dictionary<string, object>>();
        var startDate = DateTime.Now.AddMonths(-months);

        for (int i = 0; i < months; i++)
        {
            var date = startDate.AddMonths(i);
            data.Add(new Dictionary<string, object>
            {
                { "Month", date.ToString("MMM yyyy") },
                { "Revenue", 50000 + (i * 3000) + new Random(i).Next(-5000, 5000) },
                { "Expenses", 30000 + (i * 1500) + new Random(i + 1).Next(-2000, 2000) },
                { "NetIncome", 20000 + (i * 1500) }
            });
        }

        return data;
    }

    /// <summary>
    /// Generate sample table data for grid controls.
    /// </summary>
    public static List<Dictionary<string, object>> GenerateTableData(int rows = 50)
    {
        var data = new List<Dictionary<string, object>>();

        for (int i = 1; i <= rows; i++)
        {
            data.Add(new Dictionary<string, object>
            {
                { "Id", i },
                { "Name", $"Item {i}" },
                { "Amount", 1000 + (i * 50) },
                { "Date", DateTime.Now.AddDays(-i) },
                { "Status", Statuses[i % Statuses.Count] },
                { "Category", Categories[i % Categories.Count] }
            });
        }

        return data;
    }

    private static readonly List<string> SampleActions = new()
    {
        "Created Budget",
        "Updated Forecast",
        "Imported QuickBooks Data",
        "Generated Report",
        "Modified Settings",
        "Analyzed Scenario",
        "Exported Data"
    };

    private static readonly List<string> SampleUsers = new()
    {
        "John Smith",
        "Jane Doe",
        "Bob Johnson",
        "Alice Williams",
        "System Admin"
    };

    private static readonly List<string> Statuses = new()
    {
        "Active",
        "Pending",
        "Completed",
        "On Hold",
        "Cancelled"
    };

    private static readonly List<string> Categories = new()
    {
        "Revenue",
        "Expenses",
        "Assets",
        "Liabilities",
        "Equity"
    };
}
