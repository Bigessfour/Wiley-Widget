using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring MainForm Ribbon with navigation, search, and theme toggle.
/// Follows Syncfusion RibbonControlAdv API - uses ToolStripPanelItem for proper button grouping.
/// </summary>
public static class RibbonFactory
{
    /// <summary>
    /// Create and configure RibbonControlAdv with Home tab (navigation, search, theme, grid tools).
    /// Uses ToolStripPanelItem containers to prevent button collapse into overflow dropdown.
    /// </summary>
    /// <param name="form">MainForm instance - receives navigation/event handler wiring</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>Fully configured RibbonControlAdv ready to add to Controls</returns>
    public static (RibbonControlAdv Ribbon, ToolStripTabItem HomeTab) CreateRibbon(
        MainForm form,
        ILogger? logger)
    {
        if (form == null)
        {
            throw new ArgumentNullException(nameof(form));
        }

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            AccessibleName = "Ribbon_Main",
            Dock = (DockStyleEx)DockStyle.Top,
            MinimumSize = new System.Drawing.Size(800, 120)
        };

        var homeTab = new ToolStripTabItem
        {
            Name = "RibbonTab_Home",
            Text = "Home"
        };

        // Create ToolStripEx for the Home tab
        var homeToolStrip = new ToolStripEx
        {
            Name = "HomeToolStrip",
            GripStyle = ToolStripGripStyle.Hidden
        };

        // ===== NAVIGATION PANEL =====
        var navPanel = new ToolStripPanelItem
        {
            Name = "NavigationPanel",
            Text = "Navigation"
        };

        var dashboardBtn = CreateNavButton("Nav_Dashboard", MainFormResources.Dashboard, false,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true));

        var accountsBtn = CreateNavButton("Nav_Accounts", MainFormResources.Accounts, false,
            () => form.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true));

        var budgetBtn = CreateNavButton("Nav_Budget", "Budget", false,
            () => form.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true));

        var chartsBtn = CreateNavButton("Nav_Charts", MainFormResources.Charts, false,
            () => form.ShowPanel<ChartPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true));

        navPanel.Items.AddRange(new ToolStripItem[]
        {
            dashboardBtn,
            accountsBtn,
            budgetBtn,
            chartsBtn
        });

        // ===== ADVANCED PANEL =====
        var advancedPanel = new ToolStripPanelItem
        {
            Name = "AdvancedPanel",
            Text = "Advanced"
        };

        var analyticsBtn = CreateNavButton("Nav_Analytics", "Analytics", false,
            () => form.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true));

        var auditLogBtn = CreateNavButton("Nav_AuditLog", "Audit Log", false,
            () => form.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true));

        var reportsBtn = CreateNavButton("Nav_Reports", MainFormResources.Reports, false,
            () =>
            {
                try
                {
                    form.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "RibbonFactory: Reports navigation button click failed");
                }
            });

        advancedPanel.Items.AddRange(new ToolStripItem[]
        {
            analyticsBtn,
            auditLogBtn,
            reportsBtn
        });

        // ===== INTEGRATION PANEL =====
        var integrationPanel = new ToolStripPanelItem
        {
            Name = "IntegrationPanel",
            Text = "Integration"
        };

        var aiChatBtn = CreateNavButton("Nav_AIChat", "AI Chat", false,
            () => form.ShowPanel<ChatPanel>("AI Chat", DockingStyle.Right, allowFloating: true));

        var quickBooksBtn = CreateNavButton("Nav_QuickBooks", "💳 QuickBooks", false,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true));

        integrationPanel.Items.AddRange(new ToolStripItem[]
        {
            aiChatBtn,
            quickBooksBtn
        });

        // ===== SETTINGS PANEL =====
        var settingsPanel = new ToolStripPanelItem
        {
            Name = "SettingsPanel",
            Text = "Settings"
        };

        var settingsBtn = CreateNavButton("Nav_Settings", MainFormResources.Settings, false,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true));

        // Search box
        var searchLabel = new ToolStripLabel { Text = "Search:", Name = "GlobalSearch_Label" };
        var searchBox = new ToolStripTextBox
        {
            Name = "GlobalSearch",
            AccessibleName = "GlobalSearch",
            AutoSize = false,
            Width = 180
        };
        searchBox.KeyDown += (s, e) =>
        {
            if (s is not ToolStripTextBox box) return;
            try
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(box.Text))
                {
                    logger?.LogInformation("Global search triggered: {SearchText}", box.Text);
                    form.PerformGlobalSearchInternal(box.Text);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Search box error");
            }
        };

        // Theme toggle
        var themeToggleBtn = new ToolStripButton
        {
            Name = "ThemeToggle",
            AccessibleName = "Theme_Toggle",
            AutoSize = true,
            Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light" : "🌙 Dark"
        };
        themeToggleBtn.Click += (s, e) => form.ThemeToggleBtnClickInternal(s, e);

        settingsPanel.Items.AddRange(new ToolStripItem[]
        {
            settingsBtn,
            new ToolStripSeparator(),
            searchLabel,
            searchBox,
            new ToolStripSeparator(),
            themeToggleBtn
        });

        // ===== GRID TOOLS PANEL =====
        var gridPanel = new ToolStripPanelItem
        {
            Name = "GridToolsPanel",
            Text = "Grid Tools"
        };

        var gridSortAscBtn = CreateGridButton("Grid_SortAsc", "⬆ Sort",
            () => form.SortActiveGridByFirstSortableColumnInternal(descending: false));

        var gridSortDescBtn = CreateGridButton("Grid_SortDesc", "⬇ Sort",
            () => form.SortActiveGridByFirstSortableColumnInternal(descending: true));

        var gridFilterBtn = CreateGridButton("Grid_ApplyTestFilter", "🔍 Filter",
            () => form.ApplyTestFilterToActiveGridInternal());

        var gridClearBtn = CreateGridButton("Grid_ClearFilter", "✖ Clear",
            () => form.ClearActiveGridFilterInternal());

        var gridExportBtn = CreateGridButton("Grid_ExportExcel", "📊 Export",
            async () => await form.ExportActiveGridToExcelInternal());

        gridPanel.Items.AddRange(new ToolStripItem[]
        {
            gridSortAscBtn,
            gridSortDescBtn,
            gridFilterBtn,
            gridClearBtn,
            gridExportBtn
        });

        // Add all panels to the ToolStripEx
        homeToolStrip.Items.AddRange(new ToolStripItem[]
        {
            navPanel,
            new ToolStripSeparator(),
            advancedPanel,
            new ToolStripSeparator(),
            integrationPanel,
            new ToolStripSeparator(),
            settingsPanel,
            new ToolStripSeparator(),
            gridPanel
        });

        // Add ToolStripEx to the tab
        homeTab.Panel.AddToolStrip(homeToolStrip);

        // Add tab to ribbon
        ribbon.Header.AddMainItem(homeTab);

        // QuickAccessToolbar (QAT)
        InitializeQuickAccessToolbar(ribbon, logger, dashboardBtn, accountsBtn, reportsBtn, settingsBtn);

        logger?.LogDebug("Ribbon initialized via factory with ToolStripPanelItem containers");

        return (ribbon, homeTab);
    }

    private static ToolStripButton CreateNavButton(string name, string text, bool enabled, System.Action onClick)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = name,
            Enabled = enabled,
            AutoSize = true
        };
        btn.Click += (s, e) =>
        {
            try
            {
                Serilog.Log.Information("[RIBBON_NAV] Button '{ButtonName}' clicked", name);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RIBBON_NAV] Button '{name}' ('{text}') clicked");
                onClick();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[RIBBON_NAV] Button '{ButtonName}' click handler failed", name);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RIBBON_NAV ERROR] Button '{name}' failed: {ex.Message}");
            }
        };
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RIBBON_FACTORY] Created button '{name}' ('{text}'), Enabled={enabled}");
        return btn;
    }

    private static ToolStripButton CreateGridButton(string name, string text, System.Action onClick)
    {
        var btn = new ToolStripButton
        {
            Name = name,
            Text = text,
            AutoSize = true
        };
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private static void InitializeQuickAccessToolbar(
        RibbonControlAdv ribbon,
        ILogger? logger,
        params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null || buttons == null || buttons.Length == 0)
        {
            logger?.LogWarning("Cannot initialize QuickAccessToolbar: Ribbon or buttons are null");
            return;
        }

        try
        {
            foreach (var button in buttons.Where(b => b != null))
            {
                ribbon.Header.AddQuickItem(new QuickButtonReflectable(button));
            }
            logger?.LogDebug("QuickAccessToolbar initialized with {Count} items", buttons.Length);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to initialize QuickAccessToolbar");
        }
    }
}
