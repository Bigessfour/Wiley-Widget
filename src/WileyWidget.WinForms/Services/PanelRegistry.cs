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
    ///
    /// GROUP NAMES map to ribbon tabs as follows:
    ///   "Core Navigation" → Home tab
    ///   "Financials"      → Financials tab  (Budget, Rates, Municipal Accounts)
    ///   "Payments"        → Financials tab  (Payments check register)
    ///   "Integration"     → Financials tab  (QuickBooks)
    ///   "Analytics"       → Analytics &amp; Reports tab
    ///   "Reporting"       → Analytics &amp; Reports tab
    ///   "Operations"      → Analytics &amp; Reports tab
    ///   "Utilities"       → Utilities tab
    ///   "Administration"  → Administration tab
    ///   "AuditLogs"       → Administration tab
    /// </summary>
    public static class PanelRegistry
    {
        public sealed record PanelEntry(Type PanelType, string DisplayName, string DefaultGroup = "Views", DockingStyle DefaultDock = DockingStyle.Right, bool ShowInRibbonPanelsMenu = true);

        // NOTE: Keep this list alphabetized by DisplayName for readability.
        public static readonly IReadOnlyList<PanelEntry> Panels = new List<PanelEntry>
        {
            // ── Administration tab ──────────────────────────────────────────────
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountEditPanel),  "Account Editor",          "Administration", DockingStyle.Right,  false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ActivityLogPanel),  "Activity Log",            "AuditLogs",      DockingStyle.Bottom, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AuditLogPanel),     "Audit Log & Activity",    "AuditLogs",      DockingStyle.Bottom, false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel), "Data Mapper",     "Administration", DockingStyle.Right,  false),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.SettingsPanel),     "Settings",                "Administration", DockingStyle.Right,  false),

            // ── Analytics & Reports tab ─────────────────────────────────────────
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AnalyticsHubPanel),          "Analytics Hub",             "Analytics",  DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.DepartmentSummaryPanel),     "Department Summary",        "Operations", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ProactiveInsightsPanel),     "Proactive AI Insights",     "Operations", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.ReportsPanel),               "Reports",                   "Reporting",  DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RevenueTrendsPanel),         "Revenue Trends",            "Analytics",  DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.WarRoomPanel),               "War Room",                  "Operations", DockingStyle.Right),

            // ── Financials tab ──────────────────────────────────────────────────
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.BudgetPanel),       "Budget Management & Analysis", "Financials",  DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.AccountsPanel),     "Municipal Accounts",           "Financials",  DockingStyle.Left),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.PaymentsPanel),     "Payments",                     "Payments",    DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.FormHostPanel),     "Rates",                        "Financials",  DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.QuickBooksPanel),   "QuickBooks",                   "Integration", DockingStyle.Right, false),

            // ── Home tab ────────────────────────────────────────────────────────
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel), "Enterprise Vital Signs", "Core Navigation", DockingStyle.Fill, true),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.JARVISChatUserControl),     "JARVIS Chat",            "Core Navigation", DockingStyle.Right, true),

            // ── Utilities tab ───────────────────────────────────────────────────
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.CustomersPanel),                "Customers",                  "Utilities", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.RecommendedMonthlyChargePanel), "Recommended Monthly Charge", "Utilities", DockingStyle.Right),
            new PanelEntry(typeof(WileyWidget.WinForms.Controls.Panels.UtilityBillPanel),              "Utility Bills",              "Utilities", DockingStyle.Right),
        };
    }
}
