using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Provides comprehensive fallback/sample activity data for scenarios where
    /// the activity repository is unavailable or times out.
    ///
    /// Uses a session-lifetime static cache to avoid re-serialization on repeated calls.
    /// Thread-safe with lock protection.
    ///
    /// Covers all activity types: Budget updates, QuickBooks sync, System events,
    /// User actions, Reports, and Payments.
    /// </summary>
    public static class ActivityFallbackDataService
    {
        private static readonly object _lock = new();
        private static IReadOnlyList<ActivityItem>? _cachedFallbackData;

        /// <summary>
        /// Gets or creates fallback activity data. Returns the same cached instance
        /// for the app lifetime (clears on restart).
        ///
        /// Includes comprehensive activities: Budget updates, QB sync, System events,
        /// User logins, Report generation, Payment processing, covering realistic
        /// municipal operations with staggered timestamps and realistic details.
        /// </summary>
        /// <returns>Read-only collection of fallback ActivityItem objects</returns>
        public static IReadOnlyList<ActivityItem> GetFallbackActivityData()
        {
            lock (_lock)
            {
                if (_cachedFallbackData != null)
                    return _cachedFallbackData;

                var fallbackActivities = new List<ActivityItem>
                {
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-2),
                        Activity = "Budget Variance Reconciled",
                        Details = "GL-1001: Variance $330,500 calculated",
                        User = "Mayor",
                        Category = "Budget",
                        ActivityType = "BudgetUpdate"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-8),
                        Activity = "Department Budget Approved",
                        Details = "Public Works: $1,250,000 approved for FY2026",
                        User = "Finance Director",
                        Category = "Budget",
                        ActivityType = "BudgetApproval"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-15),
                        Activity = "Payment Processed",
                        Details = "Invoice #PW-12847: $15,500 — Public Works",
                        User = "Treasurer",
                        Category = "Payment",
                        ActivityType = "PaymentProcessed"
                    },

                    // === QuickBooks Integration Events ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-22),
                        Activity = "QuickBooks Sync Completed",
                        Details = "42 accounts synced, 127 transactions imported",
                        User = "System",
                        Category = "Integration",
                        ActivityType = "QBSync"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-45),
                        Activity = "Account Balance Updated",
                        Details = "General Fund balance: $580,000 (reconciled)",
                        User = "System",
                        Category = "Integration",
                        ActivityType = "QBSync"
                    },

                    // === Report Generation ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-1),
                        Activity = "Monthly Report Generated",
                        Details = "Budget-to-Actual Report (PDF, 45 pages)",
                        User = "Scheduler",
                        Category = "Report",
                        ActivityType = "ReportGenerated"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-1).AddMinutes(-30),
                        Activity = "Report Exported",
                        Details = "Q4 Budget Summary exported to Excel",
                        User = "Treasurer",
                        Category = "Report",
                        ActivityType = "ReportExport"
                    },

                    // === User & System Events ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-2),
                        Activity = "User Login",
                        Details = "Mayor logged in from 192.168.1.100",
                        User = "Mayor",
                        Category = "System",
                        ActivityType = "UserLogin"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-2).AddMinutes(-15),
                        Activity = "Settings Updated",
                        Details = "Fiscal year changed to 2026, theme updated",
                        User = "Admin",
                        Category = "System",
                        ActivityType = "SettingsChanged"
                    },

                    // === Revenue & Collections ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-3),
                        Activity = "Revenue Batch Posted",
                        Details = "Property Tax Collection: $125,000 (847 payments)",
                        User = "Tax Collector",
                        Category = "Budget",
                        ActivityType = "RevenuePosted"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-3).AddMinutes(-30),
                        Activity = "Utility Payment Received",
                        Details = "Water/Sewer Revenue: $28,500 (94 accounts)",
                        User = "Utility Manager",
                        Category = "Budget",
                        ActivityType = "RevenuePosted"
                    },

                    // === System Maintenance ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-4),
                        Activity = "Backup Completed",
                        Details = "Database backup: 2.3 GB, 12.5 minutes",
                        User = "System",
                        Category = "System",
                        ActivityType = "SystemMaintenance"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-5),
                        Activity = "Cache Refreshed",
                        Details = "Dashboard and activity cache cleared and rebuilt",
                        User = "System",
                        Category = "System",
                        ActivityType = "SystemMaintenance"
                    },

                    // === Permission & Access Changes ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-6),
                        Activity = "Permission Granted",
                        Details = "User 'Treasurer' granted access to Advanced Reports",
                        User = "Admin",
                        Category = "System",
                        ActivityType = "PermissionChange"
                    },

                    // === Error Recovery & Alerts ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-7),
                        Activity = "Budget Threshold Alert",
                        Details = "Police Department approaching 85% of budget",
                        User = "System",
                        Category = "Budget",
                        ActivityType = "Alert"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-8),
                        Activity = "System Health Check Passed",
                        Details = "All systems operational, no issues detected",
                        User = "System",
                        Category = "System",
                        ActivityType = "SystemMaintenance"
                    },

                    // === Earlier Activity (Older History) ===
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddDays(-1).AddHours(-3),
                        Activity = "Monthly Close Completed",
                        Details = "Previous month reconciliation complete, all variances < 1%",
                        User = "Finance Director",
                        Category = "Budget",
                        ActivityType = "BudgetUpdate"
                    },
                };

                _cachedFallbackData = fallbackActivities.AsReadOnly();
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
