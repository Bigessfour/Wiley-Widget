using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Provides comprehensive fallback/sample dashboard data for scenarios where
    /// real services (QuickBooks, repositories) are unavailable or still initializing.
    ///
    /// Uses a session-lifetime static cache to avoid re-serialization on repeated calls.
    /// Thread-safe with lock protection.
    /// </summary>
    public static class FallbackDataService
    {
        private static readonly object _lock = new();
        private static IReadOnlyList<DashboardItem>? _cachedFallbackData;

        /// <summary>
        /// Gets or creates fallback dashboard data. Returns the same cached instance
        /// for the app lifetime (clears on restart).
        ///
        /// Includes all key metrics: revenue, expenses, variance, cash balance,
        /// property tax, utility revenue, and activity entries.
        /// </summary>
        /// <returns>Read-only collection of fallback DashboardItem objects</returns>
        public static IReadOnlyList<DashboardItem> GetFallbackDashboardData()
        {
            lock (_lock)
            {
                if (_cachedFallbackData != null)
                    return _cachedFallbackData;

                var fallbackItems = new List<DashboardItem>
                {
                    // === Core Financial Metrics (YTD) ===
                    new()
                    {
                        Title = "Total Revenue YTD",
                        Value = "1450000.00",
                        Category = "revenue",
                        Description = "Combined revenue from all sources (Property Tax, Utility, Other)"
                    },
                    new()
                    {
                        Title = "Total Expenses YTD",
                        Value = "1120000.00",
                        Category = "expenses",
                        Description = "Total actual spending across all departments to date"
                    },
                    new()
                    {
                        Title = "Budget Variance",
                        Value = "330000.00",
                        Category = "variance",
                        Description = "Remaining budget available (positive = under budget)"
                    },
                    new()
                    {
                        Title = "Cash Balance",
                        Value = "580000.00",
                        Category = "liquidity",
                        Description = "Current liquid assets in general fund"
                    },

                    // === Revenue Breakdown ===
                    new()
                    {
                        Title = "Property Tax Revenue",
                        Value = "720000.00",
                        Category = "revenue-detail",
                        Description = "Property tax collected YTD"
                    },
                    new()
                    {
                        Title = "Utility Revenue",
                        Value = "380000.00",
                        Category = "revenue-detail",
                        Description = "Water, sewer, and utility fees collected YTD"
                    },
                    new()
                    {
                        Title = "Other Revenue",
                        Value = "350000.00",
                        Category = "revenue-detail",
                        Description = "Permits, fines, transfers, and miscellaneous revenue"
                    },

                    // === Account & Department Summary ===
                    new()
                    {
                        Title = "Active Accounts",
                        Value = "42",
                        Category = "accounts",
                        Description = "Number of active municipal accounts"
                    },
                    new()
                    {
                        Title = "Departments",
                        Value = "8",
                        Category = "departments",
                        Description = "Total departments tracked (Public Works, Finance, Police, etc.)"
                    },

                    // === Recent Activity (Simulated Historical Entries) ===
                    new()
                    {
                        Title = "Budget Variance Updated",
                        Value = "Quarterly reconciliation completed",
                        Category = "activity",
                        Description = "Latest quarterly budget review and variance adjustment"
                    },
                    new()
                    {
                        Title = "Payment Processed",
                        Value = "Invoice #PW-12847 — $15,500.00",
                        Category = "activity",
                        Description = "Public Works department invoice payment"
                    },
                    new()
                    {
                        Title = "Revenue Recorded",
                        Value = "Property Tax Batch — $125,000.00",
                        Category = "activity",
                        Description = "Monthly property tax revenue batch posted"
                    },
                    new()
                    {
                        Title = "Report Generated",
                        Value = "Monthly Budget Summary Report",
                        Category = "activity",
                        Description = "Automated monthly budget-to-actual report generated"
                    },
                    new()
                    {
                        Title = "Account Balance Updated",
                        Value = "General Fund: $580,000.00",
                        Category = "activity",
                        Description = "End-of-month general fund balance reconciliation"
                    },
                };

                _cachedFallbackData = fallbackItems.AsReadOnly();
                return _cachedFallbackData;
            }
        }

        /// <summary>
        /// Clears the fallback data cache. Useful for testing or forcing a refresh.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedFallbackData = null;
            }
        }
    }
}
