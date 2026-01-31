using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms.Tools;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Analytics;
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
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel), "Analytics Hub", DockingStyle.Right),
            new PanelEntry(typeof(AuditLogPanel), "Audit Log & Activity", DockingStyle.Bottom, false),
            new PanelEntry(typeof(CustomersPanel), "Customers", DockingStyle.Right, false),
            new PanelEntry(typeof(DashboardPanel), "Dashboard", DockingStyle.Top, false),
            new PanelEntry(typeof(DepartmentSummaryPanel), "Department Summary", DockingStyle.Right),
            new PanelEntry(typeof(ProactiveInsightsPanel), "Proactive AI Insights", DockingStyle.Right, false),
            new PanelEntry(typeof(QuickBooksPanel), "QuickBooks", DockingStyle.Right, false),
            new PanelEntry(typeof(RecommendedMonthlyChargePanel), "Recommended Monthly Charge", DockingStyle.Right),
            new PanelEntry(typeof(SettingsPanel), "Settings", DockingStyle.Right, false),
            new PanelEntry(typeof(UtilityBillPanel), "Utility Bills", DockingStyle.Right, false), // orphaned panel: wired to Data Tools
            new PanelEntry(typeof(WarRoomPanel), "War Room", DockingStyle.Right),
        };
    }
}
