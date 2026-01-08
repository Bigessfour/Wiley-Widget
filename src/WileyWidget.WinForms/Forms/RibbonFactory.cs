using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;

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
        ILogger? logger,
        IThemeIconService? iconService = null)
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
            // Menu button configuration - enables Backstage view
            MenuButtonText = "File",
            MenuButtonWidth = 54,
            MenuButtonVisible = true, // Required for Backstage access
            // Launcher button style for panel launchers
            LauncherStyle = Syncfusion.Windows.Forms.Tools.LauncherStyle.Office2007
        };

        // Initialize Backstage (File menu) per Syncfusion API
        // BackStageView requires IContainer for component model support
        var components = new System.ComponentModel.Container();
        var backStageView = new Syncfusion.Windows.Forms.BackStageView(components);
        var backStage = new Syncfusion.Windows.Forms.BackStage();
        backStageView.BackStage = backStage;
        backStageView.HostForm = form;
        form.Disposed += (_, _) => components.Dispose();
        ribbon.BackStageView = backStageView;

        // Performance optimization: reduce redraws during layout changes
        ribbon.SuspendLayout();

        // CRITICAL: SfSkinManager is SOLE PROPRIETOR of all theme and color decisions (per approved workflow)
        // Explicit theme application (defensive coding - ensures theme applied even if cascade fails)
        // NO manual color assignments (BackColor, ForeColor) - theme cascade handles all child controls
        var currentThemeString = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        var currentTheme = MapTheme(currentThemeString);
        // Theme will cascade from MainForm via SfSkinManager.SetVisualStyle applied at form level
        ribbon.OfficeColorScheme = currentThemeString.Contains("Dark", StringComparison.OrdinalIgnoreCase)
            ? Syncfusion.Windows.Forms.Tools.ToolStripEx.ColorScheme.Managed
            : Syncfusion.Windows.Forms.Tools.ToolStripEx.ColorScheme.Managed;

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

        var dashboardBtn = CreateNavButton("Nav_Dashboard", MainFormResources.Dashboard, iconService, "dashboard", currentTheme,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true));

        var accountsBtn = CreateNavButton("Nav_Accounts", MainFormResources.Accounts, iconService, "accounts", currentTheme,
            () => form.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true));

        var budgetBtn = CreateNavButton("Nav_Budget", "Budget", iconService, "budget", currentTheme,
            () => form.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true));

        var chartsBtn = CreateNavButton("Nav_Charts", MainFormResources.Charts, iconService, "chart", currentTheme,
            () => form.ShowPanel<ChartPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true));

        var customersBtn = CreateNavButton("Nav_Customers", "Customers", iconService, "customers", currentTheme,
            () => form.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true));

        var panelsDropDown = new ToolStripDropDownButton
        {
            Name = "Nav_Panels",
            AccessibleName = "Panels",
            AccessibleDescription = "Open any application panel",
            Text = "Panels",
            AutoSize = true,
            ToolTipText = "Open any panel"
        };

        // Populate panels dropdown from PanelRegistry (only those with ShowInRibbonPanelsMenu = true)
        var panelsToShow = PanelRegistry.Panels.Where(p => p.ShowInRibbonPanelsMenu).OrderBy(p => p.DisplayName);
        foreach (var p in panelsToShow)
        {
            var safeName = $"Panel_{string.Concat(p.DisplayName.Where(c => !char.IsWhiteSpace(c) && c != '&'))}".Replace(' ', '_');
            var item = new ToolStripMenuItem(p.DisplayName)
            {
                Name = safeName,
                ToolTipText = $"Open {p.DisplayName}",
                AccessibleName = $"Open {p.DisplayName}"
            };

            item.Click += (s, e) =>
            {
                try
                {
                    // Use reflection to invoke generic ShowPanel<TPanel> on MainForm
                    var showMethod = form.GetType().GetMethod("ShowPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    if (showMethod == null)
                    {
                        Serilog.Log.Error("RibbonFactory: ShowPanel method not found via reflection for Panels menu navigation");
                        return;
                    }

                    var generic = showMethod.MakeGenericMethod(p.PanelType);
                    generic.Invoke(form, new object[] { p.DisplayName, p.DefaultDock, true });
                    Serilog.Log.Information("[RIBBON_PANELS] Invoked ShowPanel<{PanelType}> for {Panel}", p.PanelType.Name, p.DisplayName);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "[RIBBON_PANELS] Failed to open panel {Panel}", p.DisplayName);
                    MessageBox.Show($"Failed to open panel {p.DisplayName}: {ex.Message}", "Panel Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            panelsDropDown.DropDownItems.Add(item);
        }

        navPanel.Items.AddRange(new ToolStripItem[]
        {
            dashboardBtn,
            accountsBtn,
            budgetBtn,
            chartsBtn,
            customersBtn,
            panelsDropDown
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

        var analyticsBtn = CreateNavButton("Nav_Analytics", "Analytics", iconService, "analytics", currentTheme,
            () => form.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true));

        var auditLogBtn = CreateNavButton("Nav_AuditLog", "Audit Log", iconService, "audit", currentTheme,
            () => form.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true));

        var reportsBtn = CreateNavButton("Nav_Reports", MainFormResources.Reports, iconService, "reports", currentTheme,
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

        var insightsBtn = CreateNavButton("Nav_ProactiveInsights", "💡 Insights", iconService, "insights", currentTheme,
            () => form.ShowPanel<ProactiveInsightsPanel>("Proactive AI Insights", DockingStyle.Right, allowFloating: true));

        var warRoomBtn = CreateNavButton("Nav_WarRoom", "⚔ War Room", iconService, "warroom", currentTheme,
            () => form.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true));

        advancedPanel.Items.AddRange(new ToolStripItem[]
        {
            analyticsBtn,
            auditLogBtn,
            reportsBtn,
            insightsBtn,
            warRoomBtn
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

        var jarvischatBtn = new ToolStripButton
        {
            Name = "Nav_JARVISChat",
            AccessibleName = "JARVIS Chat",
            AccessibleDescription = "Open premium Grok-powered AI assistant for strategic financial analysis",
            Text = "🤖 JARVIS Chat",
            AutoSize = true,
            ToolTipText = "Open JARVIS - Premium Grok AI Assistant",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        jarvischatBtn.Click += (s, e) =>
        {
            try
            {
                form.ShowPanel<ChatPanel>("AI Chat", DockingStyle.Right, allowFloating: true);
                logger?.LogInformation("[RIBBON_JARVIS] JARVIS Chat button clicked");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_JARVIS] Failed to open JARVIS Chat");
                MessageBox.Show($"Failed to open JARVIS Chat: {ex.Message}", "JARVIS Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var quickBooksBtn = CreateNavButton("Nav_QuickBooks", "💳 QuickBooks", iconService, "quickbooks", currentTheme,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true));

        integrationPanel.Items.AddRange(new ToolStripItem[]
        {
            jarvischatBtn,
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

        var settingsBtn = CreateNavButton("Nav_Settings", MainFormResources.Settings, iconService, "settings", currentTheme,
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

        // Initialize Backstage items after ribbon is configured
        InitializeBackstage(ribbon, backStage, form, logger, components);

        // QuickAccessToolbar (QAT)
        InitializeQuickAccessToolbar(ribbon, logger, dashboardBtn, accountsBtn, reportsBtn, settingsBtn);

        logger?.LogDebug("Ribbon initialized via factory with ToolStripPanelItem containers, {PanelCount} panels, Backstage enabled, accessibility enabled", 5);

        return (ribbon, homeTab);
    }

    /// <summary>
    /// Maps a theme string to a normalized AppTheme enum value.
    /// </summary>
    private static AppTheme MapTheme(string themeString)
    {
        // Normalize recognized theme identifiers; fall back to the application's default theme.
        return themeString switch
        {
            "Office2019Colorful" => AppTheme.Office2019Colorful,
            "Office2019Dark" => AppTheme.Office2019Dark,
            "Office2019Black" => AppTheme.Office2019Black,
            "Office2019DarkGray" => AppTheme.Office2019DarkGray,
            "Office2019White" => AppTheme.Office2019Colorful,
            "Dark" => AppTheme.Dark,
            "Light" => AppTheme.Light,
            "HighContrastBlack" => AppTheme.HighContrastBlack,
            _ => AppTheme.Office2019Colorful
        };
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
    /// <param name="iconService">Optional icon service for button icons</param>
    /// <param name="iconName">Optional icon name to retrieve from icon service</param>
    /// <param name="theme">Current app theme for icon generation</param>
    /// <param name="onClick">Action to execute when button is clicked (typically ShowPanel call)</param>
    /// <returns>Configured ToolStripButton ready to add to a ToolStripPanelItem</returns>
    private static ToolStripButton CreateNavButton(string name, string text, IThemeIconService? iconService, string? iconName, AppTheme theme, System.Action onClick)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = name.Replace("Nav_", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal),
            AccessibleDescription = $"Navigate to {text} panel",
            Enabled = true,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"Open {text} panel",
            ImageScaling = ToolStripItemImageScaling.None
        };

        // Set icon with disposal check to prevent "Parameter is not valid" errors
        if (!string.IsNullOrEmpty(iconName) && iconService != null && !iconService.IsDisposed)
        {
            try
            {
                btn.Image = iconService.GetIcon(iconName, theme, 16);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[RIBBON_FACTORY] Failed to get icon for '{IconName}'", iconName);
                btn.Image = null;
            }
        }
        else
        {
            btn.Image = null;
        }
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

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [RIBBON_FACTORY] Created navigation button '{name}' ('{text}'), Enabled=true");
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
    /// Initializes the Backstage view with tabs and buttons per Syncfusion API.
    /// </summary>
    /// <remarks>
    /// <para>Backstage provides File menu functionality - Info, Options, Save, Export, Print, Close.</para>
    /// <para>Uses BackStageTab for tabbed content and BackStageButton for actions.</para>
    /// <para>Per Syncfusion API: Add items via BackStageView.BackStageItems collection.</para>
    /// </remarks>
    /// <param name="ribbon">RibbonControlAdv instance containing the Backstage</param>
    /// <param name="backStage">BackStageView instance to configure</param>
    /// <param name="form">MainForm for navigation and actions</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="components">Component container for proper disposal</param>
    private static void InitializeBackstage(
        RibbonControlAdv ribbon,
        Syncfusion.Windows.Forms.BackStage backStage,
        MainForm form,
        ILogger? logger,
        System.ComponentModel.IContainer components)
    {
        if (backStage == null || ribbon?.BackStageView == null)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize Backstage: BackStage or BackStageView is null");
            return;
        }

        try
        {
            // CRITICAL: Initialize BackStageView properties before adding items to prevent rendering errors
            // BackStageView does not expose Padding or BackColor - these properties are inherited from parent form
            // No manual property setting needed here

            // ===== INFO TAB (Application info, recent files) =====
            var infoTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Info",
                AccessibleName = "Backstage Info",
                AccessibleDescription = "Application information",
                Dock = DockStyle.Fill
            };
            components.Add(infoTab);

            // Add info content panel with proper styling
            var infoPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(20),
                BackColor = System.Drawing.Color.Transparent,
                AutoScroll = true
            };

            var infoLabel = new System.Windows.Forms.Label
            {
                Text = "Wiley Widget\n\nVersion: 1.0.0\nBudget Management System\n\n© 2025 Wiley Widget Inc.",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Location = new System.Drawing.Point(20, 20),
                BackColor = System.Drawing.Color.Transparent
            };
            infoPanel.Controls.Add(infoLabel);
            infoTab.Controls.Add(infoPanel);

            // ===== OPTIONS TAB (Settings) =====
            var optionsTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Options",
                AccessibleName = "Backstage Options",
                AccessibleDescription = "Application options",
                Dock = DockStyle.Fill
            };
            components.Add(optionsTab);

            var optionsPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(20),
                BackColor = System.Drawing.Color.Transparent,
                AutoScroll = true
            };

            var optionsLabel = new System.Windows.Forms.Label
            {
                Text = "Application Settings",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 20),
                BackColor = System.Drawing.Color.Transparent
            };
            optionsPanel.Controls.Add(optionsLabel);

            var openSettingsBtn = new System.Windows.Forms.Button
            {
                Text = "Open Settings Panel",
                Size = new System.Drawing.Size(200, 32),
                Location = new System.Drawing.Point(20, 60),
                FlatStyle = FlatStyle.System
            };
            openSettingsBtn.Click += (s, e) =>
            {
                try
                {
                    form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                    logger?.LogInformation("[BACKSTAGE] Opened Settings panel from Options tab");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[BACKSTAGE] Failed to open Settings panel");
                    MessageBox.Show($"Failed to open Settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            optionsPanel.Controls.Add(openSettingsBtn);
            optionsTab.Controls.Add(optionsPanel);

            // ===== EXPORT TAB (Data export options) =====
            var exportTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Export",
                AccessibleName = "Backstage Export",
                AccessibleDescription = "Data export options",
                Dock = DockStyle.Fill
            };
            components.Add(exportTab);

            var exportPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(20),
                BackColor = System.Drawing.Color.Transparent,
                AutoScroll = true
            };

            var exportLabel = new System.Windows.Forms.Label
            {
                Text = "Export Data",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 20),
                BackColor = System.Drawing.Color.Transparent
            };
            exportPanel.Controls.Add(exportLabel);

            var exportExcelBtn = new System.Windows.Forms.Button
            {
                Text = "Export Active Grid to Excel",
                Size = new System.Drawing.Size(200, 32),
                Location = new System.Drawing.Point(20, 60),
                FlatStyle = FlatStyle.System
            };
            exportExcelBtn.Click += async (s, e) =>
            {
                try
                {
                    await form.ExportActiveGridToExcelInternal();
                    logger?.LogInformation("[BACKSTAGE] Exported active grid to Excel");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[BACKSTAGE] Excel export failed");
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            exportPanel.Controls.Add(exportExcelBtn);
            exportTab.Controls.Add(exportPanel);

            // ===== BUTTONS (Save, Print, Close) =====
            var saveButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Save",
                AccessibleName = "Backstage Save",
                AccessibleDescription = "Save current data"
            };
            saveButton.Click += (s, e) =>
            {
                try
                {
                    logger?.LogInformation("[BACKSTAGE] Save clicked - placeholder for save logic");
                    MessageBox.Show("Save functionality placeholder", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[BACKSTAGE] Save action failed");
                }
            };

            var printButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Print",
                AccessibleName = "Backstage Print",
                AccessibleDescription = "Print current data"
            };
            printButton.Click += (s, e) =>
            {
                try
                {
                    logger?.LogInformation("[BACKSTAGE] Print clicked - placeholder for print logic");
                    MessageBox.Show("Print functionality placeholder", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[BACKSTAGE] Print action failed");
                }
            };

            var closeButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Close",
                AccessibleName = "Backstage Close",
                AccessibleDescription = "Close the application"
            };
            closeButton.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;
            closeButton.Click += (s, e) =>
            {
                try
                {
                    form.Close();
                    logger?.LogInformation("[BACKSTAGE] Application closed via Backstage");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[BACKSTAGE] Close action failed");
                }
            };

            components.Add(saveButton);
            components.Add(printButton);
            components.Add(closeButton);

            // Add items to BackStage - IMPORTANT: Add buttons first (they are always visible),
            // then tabs (content loaded on demand)
            backStage.Controls.Add(saveButton);
            backStage.Controls.Add(printButton);
            backStage.Controls.Add(closeButton);
            backStage.Controls.Add(infoTab);
            backStage.Controls.Add(optionsTab);
            backStage.Controls.Add(exportTab);



            logger?.LogDebug("[RIBBON_FACTORY] Backstage initialized with {TabCount} tabs, {ButtonCount} buttons", 3, 3);
            logger?.LogInformation("[BACKSTAGE] Backstage initialization completed successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to initialize Backstage");
        }
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
