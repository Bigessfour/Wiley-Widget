using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms.Tools;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;

using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Forms;


namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Central registry of application panels and their preferred docking styles and ribbon groups.
    /// Used by MainForm ribbon helpers (and other UI surfaces) to expose navigation for all panels.
    /// Keep this list as the single source of truth for panel display names and defaults.
    /// </summary>
    public static class PanelRegistry
    {
        public sealed record PanelEntry(Type PanelType, string DisplayName, string DefaultGroup = "Views", DockingStyle DefaultDock = DockingStyle.Right, bool ShowInRibbonPanelsMenu = true);

        // NOTE: Keep this list alphabetized by DisplayName for readability.
        public static readonly IReadOnlyList<PanelEntry> Panels = new List<PanelEntry>
        {
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountEditPanel), "Account Editor", "Views", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ActivityLogPanel), "Activity Log", "Views", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AnalyticsHubPanel), "Analytics Hub", "Reporting", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AuditLogPanel), "Audit Log & Activity", "Views", DockingStyle.Bottom, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.BudgetPanel), "Budget Management & Analysis", "Financials", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.CustomersPanel), "Customers", "Views", DockingStyle.Right, false),
            // Dashboard is now provided as a form hosted in a FormHostPanel
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.FormHostPanel), "Dashboard", "Core Navigation", DockingStyle.Top, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel), "Data Mapper", "Views", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.DepartmentSummaryPanel), "Department Summary", "Views", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountsPanel), "Municipal Accounts", "Financials", DockingStyle.Left, true),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ProactiveInsightsPanel), "Proactive AI Insights", "Views", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.QuickBooksPanel), "QuickBooks", "Tools", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RecommendedMonthlyChargePanel), "Recommended Monthly Charge", "Views", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.FormHostPanel), "Rates", "Financials", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ReportsPanel), "Reports", "Reporting", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RevenueTrendsPanel), "Revenue Trends", "Views", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.SettingsPanel), "Settings", "Tools", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.UtilityBillPanel), "Utility Bills", "Views", DockingStyle.Right, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.WarRoomPanel), "War Room", "Views", DockingStyle.Right),
        };
    }
}
