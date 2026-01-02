using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring MainForm Ribbon with navigation, search, and theme toggle.
/// Follows Syncfusion RibbonControlAdv API - uses ToolStripPanelItem for proper button grouping.
/// </summary>
public static class RibbonFactory
{
    /// <summary>
    /// Creates and configures a production-ready RibbonControlAdv with Home tab containing navigation, search, theme toggle, and grid tools.
    /// </summary>
    /// <remarks>
    /// <para><strong>Architecture:</strong> Uses ToolStripPanelItem containers to organize buttons into logical groups and prevent collapse into overflow dropdown.</para>
    /// <para><strong>Theming:</strong> Applies SfSkinManager theme explicitly (defensive coding) - theme cascades from ribbon to all child controls automatically.</para>
    /// <para><strong>Performance:</strong> Suspends layout during construction and resumes after all items added to minimize redraws.</para>
    /// <para><strong>Accessibility:</strong> All buttons include AccessibleName, AccessibleDescription, and ToolTipText for screen readers and keyboard navigation.</para>
    /// <para><strong>Panels Created:</strong>
    /// <list type="bullet">
    /// <item><description>Navigation: Dashboard, Accounts, Budget, Charts</description></item>
    /// <item><description>Advanced: Analytics, Audit Log, Reports</description></item>
    /// <item><description>Integration: AI Chat, QuickBooks</description></item>
    /// <item><description>Settings: Settings button, Global Search, Theme Toggle</description></item>
    /// <item><description>Grid Tools: Sort (Asc/Desc), Filter, Clear, Export to Excel</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="form">MainForm instance that receives navigation/event handler wiring for all button clicks</param>
    /// <param name="logger">Optional logger for diagnostics, theme application, and error tracking</param>
    /// <returns>Tuple containing the fully configured RibbonControlAdv and its Home tab reference</returns>
    /// <exception cref="ArgumentNullException">Thrown when form parameter is null</exception>
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
            AccessibleDescription = "Main application ribbon with navigation and tools",
            Dock = (DockStyleEx)DockStyle.Top,
            MinimumSize = new System.Drawing.Size(800, 120),
            // Menu button configuration (optional - enables application menu)
            MenuButtonText = "File",
            MenuButtonWidth = 54,
            MenuButtonVisible = false, // Set to true if implementing File menu
            // Launcher button style for panel launchers
            LauncherStyle = Syncfusion.Windows.Forms.Tools.LauncherStyle.Office2007
        };

        // Performance optimization: reduce redraws during layout changes
        ribbon.SuspendLayout();

        // CRITICAL: SfSkinManager is SOLE PROPRIETOR of all theme and color decisions (per approved workflow)
        // Explicit theme application (defensive coding - ensures theme applied even if cascade fails)
        // NO manual color assignments (BackColor, ForeColor) - theme cascade handles all child controls
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        try
        {
            SfSkinManager.SetVisualStyle(ribbon, currentTheme);
            // Match OfficeColorScheme to theme for consistent color tables
            ribbon.OfficeColorScheme = currentTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase)
                ? Syncfusion.Windows.Forms.Tools.ToolStripEx.ColorScheme.Managed
                : Syncfusion.Windows.Forms.Tools.ToolStripEx.ColorScheme.Managed;
            logger?.LogDebug("[RIBBON_FACTORY] Theme explicitly applied via SfSkinManager: {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to apply explicit theme to Ribbon - relying on cascade from parent");
        }

        var homeTab = new ToolStripTabItem
        {
            Name = "RibbonTab_Home",
            Text = "Home",
            AccessibleName = "Home Tab",
            AccessibleDescription = "Main navigation and tools"
        };

        // Create ToolStripEx for the Home tab
        var homeToolStrip = new ToolStripEx
        {
            Name = "HomeToolStrip",
            GripStyle = ToolStripGripStyle.Hidden,
            // Performance: reduce layout recalculations
            LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            // Improve rendering quality
            RenderMode = ToolStripRenderMode.Professional,
            // Auto-size for ribbon layout
            AutoSize = true
        };

        // ===== NAVIGATION PANEL =====
        var navPanel = new ToolStripPanelItem
        {
            Name = "NavigationPanel",
            Text = "Navigation",
            AccessibleName = "Navigation Panel",
            AccessibleDescription = "Primary navigation buttons",
            // Row configuration for multi-row layouts (1 = single row)
            RowCount = 1
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
            Text = "Advanced",
            AccessibleName = "Advanced Panel",
            AccessibleDescription = "Advanced analysis and reporting tools",
            RowCount = 1
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
            Text = "Integration",
            AccessibleName = "Integration Panel",
            AccessibleDescription = "External integrations and AI tools",
            RowCount = 1
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
            Text = "Settings",
            AccessibleName = "Settings Panel",
            AccessibleDescription = "Application settings and preferences",
            RowCount = 1
        };

        var settingsBtn = CreateNavButton("Nav_Settings", MainFormResources.Settings, false,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true));

        // Search box
        var searchLabel = new ToolStripLabel
        {
            Text = "Search:",
            Name = "GlobalSearch_Label",
            AccessibleName = "Search Label"
        };
        var searchBox = new ToolStripTextBox
        {
            Name = "GlobalSearch",
            AccessibleName = "Global Search",
            AccessibleDescription = "Search across all panels and data (press Enter to search)",
            AutoSize = false,
            Width = 180,
            ToolTipText = "Search panels and data (press Enter)"
        };
        // Wire up search box with Enter key handler and error handling
        searchBox.KeyDown += (s, e) =>
        {
            if (s is not ToolStripTextBox box) return;
            try
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(box.Text))
                {
                    logger?.LogInformation("[RIBBON_SEARCH] Global search triggered: {SearchText}", box.Text);
                    form.PerformGlobalSearchInternal(box.Text);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_SEARCH] Search box KeyDown handler failed");
                MessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        // Theme toggle button - delegates to MainForm for centralized theme switching via SfSkinManager
        var themeToggleBtn = new ToolStripButton
        {
            Name = "ThemeToggle",
            AccessibleName = "Theme Toggle",
            AccessibleDescription = "Switch between light and dark themes",
            AutoSize = true,
            Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light" : "🌙 Dark",
            ToolTipText = "Toggle between light and dark themes",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        // Wire click handler with error handling
        themeToggleBtn.Click += (s, e) =>
        {
            try
            {
                form.ThemeToggleBtnClickInternal(s, e);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_THEME] Theme toggle button click handler failed");
                MessageBox.Show($"Theme toggle failed: {ex.Message}", "Theme Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

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
            Text = "Grid Tools",
            AccessibleName = "Grid Tools Panel",
            AccessibleDescription = "Data grid manipulation and export tools",
            RowCount = 1
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

        // Resume layout updates after all items added
        ribbon.ResumeLayout(performLayout: true);

        // QuickAccessToolbar (QAT)
        InitializeQuickAccessToolbar(ribbon, logger, dashboardBtn, accountsBtn, reportsBtn, settingsBtn);

        logger?.LogDebug("Ribbon initialized via factory with ToolStripPanelItem containers, {PanelCount} panels, accessibility enabled", 5);

        return (ribbon, homeTab);
    }

    /// <summary>
    /// Creates a navigation button with robust error handling and accessibility support.
    /// </summary>
    /// <remarks>
    /// <para>Navigation buttons use SfSkinManager theme cascade - NO manual color assignments.</para>
    /// <para>Click handlers are wrapped in try-catch with logging for diagnostics.</para>
    /// </remarks>
    /// <param name="name">Unique button name (e.g., "Nav_Dashboard")</param>
    /// <param name="text">Display text shown on the button</param>
    /// <param name="enabled">Initial enabled state (typically false until panels are ready)</param>
    /// <param name="onClick">Action to execute when button is clicked (typically ShowPanel call)</param>
    /// <returns>Configured ToolStripButton ready to add to a ToolStripPanelItem</returns>
    private static ToolStripButton CreateNavButton(string name, string text, bool enabled, System.Action onClick)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = name.Replace("Nav_", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal),
            AccessibleDescription = $"Navigate to {text} panel",
            Enabled = enabled,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"Open {text} panel"
        };
        // Wire click handler with comprehensive error handling and logging
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
                MessageBox.Show($"Failed to navigate to {text}: {ex.Message}", "Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RIBBON_FACTORY] Created navigation button '{name}' ('{text}'), Enabled={enabled}");
        return btn;
    }

    /// <summary>
    /// Creates a grid operation button with error handling for data grid manipulation operations.
    /// </summary>
    /// <remarks>
    /// <para>Grid buttons operate on the currently active grid in the focused docking panel.</para>
    /// <para>Operations include sort, filter, clear filter, and Excel export.</para>
    /// <para>Uses SfSkinManager theme cascade - NO manual color assignments.</para>
    /// </remarks>
    /// <param name="name">Unique button name (e.g., "Grid_SortAsc")</param>
    /// <param name="text">Display text with optional emoji (e.g., "⬆ Sort")</param>
    /// <param name="onClick">Action to execute when button is clicked (may be async for Excel export)</param>
    /// <returns>Configured ToolStripButton ready to add to Grid Tools panel</returns>
    private static ToolStripButton CreateGridButton(string name, string text, System.Action onClick)
    {
        var cleanName = name.Replace("Grid_", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);
        var btn = new ToolStripButton
        {
            Name = name,
            Text = text,
            AccessibleName = cleanName,
            AccessibleDescription = $"Grid operation: {cleanName}",
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"{cleanName} active grid"
        };

        // Wire click handler with error handling
        btn.Click += (s, e) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[RIBBON_GRID] Grid button '{ButtonName}' click handler failed", name);
                MessageBox.Show($"Grid operation '{cleanName}' failed: {ex.Message}", "Grid Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        return btn;
    }

    /// <summary>
    /// Initializes the Quick Access Toolbar (QAT) with frequently used navigation buttons.
    /// </summary>
    /// <remarks>
    /// <para>The QAT provides one-click access to key navigation buttons above the ribbon tabs.</para>
    /// <para>Buttons are added as QuickButtonReflectable references to maintain click handler wiring.</para>
    /// <para>Per Syncfusion API: QAT items inherit theme from ribbon automatically.</para>
    /// </remarks>
    /// <param name="ribbon">RibbonControlAdv instance to add QAT items to</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="buttons">Navigation buttons to add to QAT (typically Dashboard, Accounts, Reports, Settings)</param>
    private static void InitializeQuickAccessToolbar(
        RibbonControlAdv ribbon,
        ILogger? logger,
        params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize QuickAccessToolbar: Ribbon or Header is null");
            return;
        }

        if (buttons == null || buttons.Length == 0)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize QuickAccessToolbar: No buttons provided");
            return;
        }

        try
        {
            var addedCount = 0;
            foreach (var button in buttons.Where(b => b != null))
            {
                ribbon.Header.AddQuickItem(new QuickButtonReflectable(button));
                addedCount++;
            }
            logger?.LogDebug("[RIBBON_FACTORY] QuickAccessToolbar initialized with {Count} items", addedCount);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to initialize QuickAccessToolbar");
        }
    }
}
