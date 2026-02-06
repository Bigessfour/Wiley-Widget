using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms.Tools;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Forms;


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
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountEditPanel), "Account Editor", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ActivityLogPanel), "Activity Log", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel), "Analytics Hub", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AuditLogPanel), "Audit Log & Activity", DockingStyle.Bottom, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.BudgetPanel), "Budget Management & Analysis", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.BudgetOverviewPanel), "Budget Overview", DockingStyle.Bottom),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.CustomersPanel), "Customers", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.DashboardPanel), "Dashboard", DockingStyle.Top, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel), "Data Mapper", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Analytics.DepartmentSummaryPanel), "Department Summary", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountsPanel), "Municipal Accounts", DockingStyle.Left, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Analytics.ProactiveInsightsPanel), "Proactive AI Insights", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.QuickBooksPanel), "QuickBooks", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RecommendedMonthlyChargePanel), "Recommended Monthly Charge", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ReportsPanel), "Reports", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RevenueTrendsPanel), "Revenue Trends", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.SettingsPanel), "Settings", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.UtilityBillPanel), "Utility Bills", DockingStyle.Right, false), // orphaned panel: wired to Data Tools
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.WarRoomPanel), "War Room", DockingStyle.Right),
        };
    }
}
