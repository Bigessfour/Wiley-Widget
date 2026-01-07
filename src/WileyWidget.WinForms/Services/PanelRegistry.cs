using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms.Tools;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.ChatUI;


namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Central registry of application panels and their preferred docking styles.
    /// Used by RibbonFactory (and other UI surfaces) to expose navigation for all panels.
    /// Keep this list as the single source of truth for panel display names and defaults.
    /// </summary>
    public static class PanelRegistry
    {
        public sealed record PanelEntry(Type PanelType, string DisplayName, DockingStyle DefaultDock = DockingStyle.Right, bool ShowInRibbonPanelsMenu = true);

        // NOTE: Keep this list alphabetized by DisplayName for readability.
        public static readonly IReadOnlyList<PanelEntry> Panels = new List<PanelEntry>
        {
            new PanelEntry(typeof(AccountsPanel), "Municipal Accounts", DockingStyle.Left, false), // primary: in nav
            new PanelEntry(typeof(AnalyticsPanel), "Budget Analytics & Insights", DockingStyle.Right, false),
            new PanelEntry(typeof(AuditLogPanel), "Audit Log & Activity", DockingStyle.Bottom, false),
            new PanelEntry(typeof(BudgetPanel), "Budget Management", DockingStyle.Right),
            new PanelEntry(typeof(BudgetOverviewPanel), "Budget Overview", DockingStyle.Bottom, false),
            new PanelEntry(typeof(ChartPanel), "Budget Analytics", DockingStyle.Right, false),
            new PanelEntry(typeof(CustomersPanel), "Customers", DockingStyle.Right, false),
            new PanelEntry(typeof(DashboardPanel), "Dashboard", DockingStyle.Top, false),
            new PanelEntry(typeof(DepartmentSummaryPanel), "Department Summary", DockingStyle.Right),
            new PanelEntry(typeof(QuickBooksPanel), "QuickBooks", DockingStyle.Right, false),
            new PanelEntry(typeof(RecommendedMonthlyChargePanel), "Recommended Monthly Charge", DockingStyle.Right),
            new PanelEntry(typeof(ReportsPanel), "Reports", DockingStyle.Right, false),
            new PanelEntry(typeof(RevenueTrendsPanel), "Revenue Trends", DockingStyle.Right),
            new PanelEntry(typeof(SettingsPanel), "Settings", DockingStyle.Right, false),
            new PanelEntry(typeof(UtilityBillPanel), "Utility Bills", DockingStyle.Right),
            new PanelEntry(typeof(ChatPanel), "AI Chat", DockingStyle.Right, false)
        };
    }
}
