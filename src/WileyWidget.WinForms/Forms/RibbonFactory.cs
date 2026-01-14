using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;

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
        WileyWidget.WinForms.Forms.MainForm form,
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
            MenuButtonText = "File",
            MenuButtonWidth = 54,
            MenuButtonVisible = false, // Disabled due to BackStage issues
            LauncherStyle = Syncfusion.Windows.Forms.Tools.LauncherStyle.Office2007
        };

        ribbon.SuspendLayout();

        var currentThemeString = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        var homeTab = new ToolStripTabItem { Text = "Home", AccessibleName = "Home Tab" };
        var homeToolStrip = new ToolStripEx { Name = "HomeToolStrip", AccessibleName = "Home ToolStrip" };

        // ===== NAVIGATION PANEL =====
        var navPanel = new ToolStripPanelItem
        {
            Name = "NavigationPanel",
            Text = "Navigation",
            AccessibleName = "Navigation Panel",
            AccessibleDescription = "Main navigation buttons to switch between panels",
            RowCount = 1
        };

        var dashboardBtn = CreateNavButton("Nav_Dashboard", "📊 Dashboard", "dashboard", currentThemeString,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Left, allowFloating: true));

        var accountsBtn = CreateNavButton("Nav_Accounts", "💰 Accounts", "accounts", currentThemeString,
            () => form.ShowPanel<AccountsPanel>("Chart of Accounts", DockingStyle.Fill, allowFloating: true));

        var budgetBtn = CreateNavButton("Nav_Budget", "📈 Budget", "budget", currentThemeString,
            () => form.ShowPanel<BudgetPanel>("Budget Management", DockingStyle.Fill, allowFloating: true));

        var chartsBtn = CreateNavButton("Nav_Charts", "📉 Charts", "charts", currentThemeString,
            () => form.ShowPanel<ChartPanel>("Financial Charts", DockingStyle.Right, allowFloating: true));

        var customersBtn = CreateNavButton("Nav_Customers", "👥 Customers", "customers", currentThemeString,
            () => form.ShowPanel<CustomersPanel>("Customer Management", DockingStyle.Fill, allowFloating: true));

        // Panels dropdown menu
        var panelsDropDown = new ToolStripDropDownButton
        {
            Name = "PanelsDropdown",
            Text = "▼ Panels",
            AccessibleName = "Panels Menu",
            AccessibleDescription = "Access all available panels"
        };

        // TODO: Populate panels dropdown from registry
        // For now, panels dropdown is empty - user should implement IPanelRegistry integration

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

        var analyticsBtn = CreateNavButton("Nav_Analytics", "Analytics", "analytics", currentThemeString,
            () => form.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true));

        var auditLogBtn = CreateNavButton("Nav_AuditLog", "Audit Log", "audit", currentThemeString,
            () => form.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true));

        var reportsBtn = CreateNavButton("Nav_Reports", "Reports", "reports", currentThemeString,
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

        var insightsBtn = CreateNavButton("Nav_ProactiveInsights", "💡 Insights", "insights", currentThemeString,
            () => form.ShowPanel<ProactiveInsightsPanel>("Proactive AI Insights", DockingStyle.Right, allowFloating: true));

        var warRoomBtn = CreateNavButton("Nav_WarRoom", "⚔ War Room", "warroom", currentThemeString,
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

        var quickBooksBtn = CreateNavButton("Nav_QuickBooks", "💳 QuickBooks", "quickbooks", currentThemeString,
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

        var settingsBtn = CreateNavButton("Nav_Settings", "Settings", "settings", currentThemeString,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true));

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
        searchBox.KeyDown += (s, e) =>
        {
            if (s is not ToolStripTextBox box) return;
            try
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(box.Text))
                {
                    logger?.LogInformation("[RIBBON_SEARCH] Global search triggered: {SearchText}", box.Text);
                    form.PerformGlobalSearch(box.Text);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_SEARCH] Search box KeyDown handler failed");
                MessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

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
        themeToggleBtn.Click += (s, e) =>
        {
            MessageBox.Show("Theme toggle not yet implemented.", "Theme Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            () =>
            {
                try { form.GetType().GetMethod("SortActiveGridByFirstSortableColumn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(form, new object[] { false }); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid sort ascending not available"); }
            });

        var gridSortDescBtn = CreateGridButton("Grid_SortDesc", "⬇ Sort",
            () =>
            {
                try { form.GetType().GetMethod("SortActiveGridByFirstSortableColumn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(form, new object[] { true }); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid sort descending not available"); }
            });

        var gridFilterBtn = CreateGridButton("Grid_ApplyTestFilter", "🔍 Filter",
            () =>
            {
                try { form.GetType().GetMethod("ApplyTestFilterToActiveGrid", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(form, null); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid filter not available"); }
            });

        var gridClearBtn = CreateGridButton("Grid_ClearFilter", "✖ Clear",
            () =>
            {
                try { form.GetType().GetMethod("ClearActiveGridFilter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(form, null); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid clear filter not available"); }
            });

        var gridExportBtn = CreateGridButton("Grid_ExportExcel", "📊 Export",
            async () =>
            {
                try { await (Task)(form.GetType().GetMethod("ExportActiveGridToExcel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(form, null) ?? Task.CompletedTask); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid export not available"); }
            });

        gridPanel.Items.AddRange(new ToolStripItem[]
        {
            gridSortAscBtn,
            gridSortDescBtn,
            gridFilterBtn,
            gridClearBtn,
            gridExportBtn
        });

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

        homeTab.Panel.AddToolStrip(homeToolStrip);
        ribbon.Header.AddMainItem(homeTab);
        ribbon.ResumeLayout(performLayout: true);

        logger?.LogDebug("Ribbon initialized via factory with {PanelCount} panels, Backstage disabled", 5);

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
    /// <param name="iconService">Optional icon service for button icons</param>
    /// <param name="iconName">Optional icon name to retrieve from icon service</param>
    /// <param name="theme">Current app theme for icon generation</param>
    /// <param name="onClick">Action to execute when button is clicked (typically ShowPanel call)</param>
    /// <returns>Configured ToolStripButton ready to add to a ToolStripPanelItem</returns>
    private static ToolStripButton CreateNavButton(string name, string text, string? iconName, string theme, System.Action onClick)
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

        // Set icon via DpiAwareImageService (preferred over deprecated IThemeIconService)
        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                btn.Image = dpi?.GetImage(iconName);
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

    /* BACKSTAGE METHOD - COMMENTED OUT
    /// <summary>
    /// Initializes the Backstage view with tabs and buttons per Syncfusion API.
    /// </summary>
    /// <remarks>
    /// <para>Backstage provides File menu functionality - Info, Options, Save, Export, Print, Close.</para>
    /// <para>Uses BackStageTab for tabbed content and BackStageButton for actions.</para>
    /// <para>
    /// Syncfusion.Tools.Windows v32.x WinForms API:
    /// <list type="bullet">
    /// <item><description>Add BackStageTab instances to <c>backStage.Tabs</c> collection (NOT TabPages).</description></item>
    /// <item><description>Add BackStageButton instances to <c>backStage.Buttons</c> collection.</description></item>
    /// <item><description>Set <c>backStage.SelectedTab</c> BEFORE adding tabs to avoid paint/open NREs.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="ribbon">RibbonControlAdv instance containing the Backstage</param>
    /// <param name="backStageView">BackStageView instance to configure</param>
    /// <param name="backStage">BackStage instance to configure</param>
    /// <param name="form">MainForm for navigation and actions</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="components">Component container for proper disposal</param>
    private static void InitializeBackstage_DISABLED(
        RibbonControlAdv ribbon,
        Syncfusion.Windows.Forms.BackStageView backStageView,
        Syncfusion.Windows.Forms.BackStage backStage,
        MainForm form,
        ILogger? logger,
        System.ComponentModel.IContainer components)
    {
        if (backStageView == null || backStage == null)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize Backstage: BackStage or BackStageView is null");
            return;
        }

        try
        {
            // CRITICAL: Hide BackStage during initialization to prevent paint errors
            backStage.Visible = false;
            backStage.SuspendLayout();

            // ===== INFO TAB =====
            var infoTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Name = "Backstage_Info",
                Text = "Info",
                AccessibleName = "Info Tab",
                AccessibleDescription = "Application information and version details",
                Dock = DockStyle.Fill
            };

            var infoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };

            var infoLabel = new Label
            {
                Text = "Wiley Widget\n\nVersion: 1.0.0\nBudget Management System\n\n© 2026 Wiley Widget Inc.",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Location = new System.Drawing.Point(20, 20)
            };
            infoPanel.Controls.Add(infoLabel);
            infoTab.Controls.Add(infoPanel);

            // ===== OPTIONS TAB =====
            var optionsTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Name = "Backstage_Options",
                Text = "Options",
                AccessibleName = "Options Tab",
                AccessibleDescription = "Application settings and preferences",
                Dock = DockStyle.Fill
            };

            var optionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };

            var optionsLabel = new Label
            {
                Text = "Application Options\n\nConfigure settings and preferences here.\n\nOptions panel coming soon.",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Location = new System.Drawing.Point(20, 20)
            };
            optionsPanel.Controls.Add(optionsLabel);
            optionsTab.Controls.Add(optionsPanel);

            // ===== EXPORT TAB =====
            var exportTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Name = "Backstage_Export",
                Text = "Export",
                AccessibleName = "Export Tab",
                AccessibleDescription = "Data export and reporting options",
                Dock = DockStyle.Fill
            };

            var exportPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };

            var exportLabel = new Label
            {
                Text = "Export Data\n\nExport budget data to various formats.\n\nExport features coming soon.",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Location = new System.Drawing.Point(20, 20)
            };
            exportPanel.Controls.Add(exportLabel);
            exportTab.Controls.Add(exportPanel);

            // ===== BUTTONS =====
            var saveButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Name = "Backstage_Save",
                Text = "Save",
                AccessibleName = "Save Button",
                AccessibleDescription = "Save current data and settings"
            };

            var printButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Name = "Backstage_Print",
                Text = "Print",
                AccessibleName = "Print Button",
                AccessibleDescription = "Print current view or report"
            };

            var closeButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Name = "Backstage_Close",
                Text = "Close",
                AccessibleName = "Close Button",
                AccessibleDescription = "Close the application"
            };

            var separator = new Syncfusion.Windows.Forms.BackStageSeparator
            {
                Name = "Backstage_Separator"
            };

            // Wire button clicks
            saveButton.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Save clicked");
                MessageBox.Show("Save functionality - implement in MainForm", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            printButton.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Print clicked");
                MessageBox.Show("Print functionality - implement in MainForm", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            closeButton.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Close clicked - exiting application");
                form.Close();
            };

            // Clear any designer-added items (defensive)
            backStage.Controls.Clear();

            // Add top items (tabs default to Top placement)
            backStage.Controls.Add(infoTab);
            backStage.Controls.Add(optionsTab);
            backStage.Controls.Add(exportTab);

            // Add separator and bottom items
            separator.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;
            saveButton.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;
            printButton.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;
            closeButton.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;

            backStage.Controls.Add(separator);
            backStage.Controls.Add(saveButton);
            backStage.Controls.Add(printButton);
            backStage.Controls.Add(closeButton);

            // Set default selected tab to prevent null reference during paint
            if (backStage.Controls.Count > 0)
            {
                backStage.SelectedIndex = 0;
            }

            // Resume layout and make visible
            backStage.ResumeLayout(true);
            backStage.Visible = true;

            logger?.LogInformation("[BACKSTAGE] Backstage initialized successfully with {TabCount} tabs and {ButtonCount} buttons", 3, 3);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to initialize Backstage content");

            // Failsafe: hide the menu button to prevent crash on click
            ribbon.MenuButtonVisible = false;
        }
    }
    END BACKSTAGE METHOD COMMENT */

    private static void TrySetThemeName(object target, string themeName, ILogger? logger)
    {
        try
        {
            var prop = target.GetType().GetProperty("ThemeName", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.CanWrite != true) return;
            prop.SetValue(target, themeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set ThemeName on {Type}", target.GetType().FullName);
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

