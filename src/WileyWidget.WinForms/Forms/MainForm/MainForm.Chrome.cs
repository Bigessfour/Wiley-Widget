using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private MenuStrip? _menuStrip;
    private ToolStripMenuItem? _recentFilesMenu;
    private RibbonControlAdv? _ribbon;
    private ToolStripTabItem? _homeTab;
    private ToolStripEx? _navigationStrip;
    private StatusBarAdv? _statusBar;
    private StatusBarAdvPanel? _statusLabel;
    private StatusBarAdvPanel? _statusTextPanel;
    private StatusBarAdvPanel? _statePanel;
    private StatusBarAdvPanel? _progressPanel;
    private Syncfusion.Windows.Forms.Tools.ProgressBarAdv? _progressBar;
    private StatusBarAdvPanel? _clockPanel;
    private System.Windows.Forms.Timer? _statusTimer;

    /// <summary>
    /// Initialize UI chrome elements (Ribbon, Status Bar, Navigation).
    /// Call this from constructor after configuration is loaded.
    /// </summary>
    private void InitializeChrome()
    {
        if (DesignMode)
        {
            return;
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Chrome Initialization");

        var chromeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogInformation("InitializeChrome start - handleCreated={HandleCreated}", IsHandleCreated);

        try
        {
            SuspendLayout();

            // Enable Per-Monitor V2 DPI Awareness (syncs with app.manifest)
            AutoScaleMode = AutoScaleMode.Dpi;

            // Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainForm";
            KeyPreview = true;

            // Initialize components container if needed
            components ??= new System.ComponentModel.Container();
            _logger?.LogInformation("Components container initialized");

            // Establish default Escape key behavior
            EnsureDefaultActionButtons();

            // Initialize Menu Bar (always available)
            InitializeMenuBar();
            _logger?.LogInformation("Menu bar initialized");

            // Initialize Ribbon
            if (!_uiConfig.IsUiTestHarness)
            {
                try
                {
                    InitializeRibbon();
                    _logger?.LogInformation("Ribbon initialized");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize Ribbon");
                    _logger?.LogError("InitializeRibbon failed: {Message}", ex.Message);
                    _ribbon = null;
                }
            }

            // Initialize Status Bar
            InitializeStatusBar();
            _logger?.LogInformation("Status bar initialized");

            // Initialize Navigation Strip (alternative to Ribbon for test harness)
            if (_uiConfig.IsUiTestHarness)
            {
                InitializeNavigationStrip();
                _logger?.LogInformation("Navigation strip initialized (UI test harness mode)");
            }

            // Start status timer
            InitializeStatusTimer();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
        }
        finally
        {
            try
            {
                ResumeLayout(false);
                PerformLayout();
            }
            catch
            {
                // Best-effort layout restoration
            }
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for primary navigation, global search, and session theme toggle.
    /// </summary>
    private void InitializeRibbon()
    {
        try
        {
            var ribbonResult = RibbonFactory.CreateRibbon(this, _logger);
            _ribbon = ribbonResult.Ribbon;
            _homeTab = ribbonResult.HomeTab;

            _ribbon.AccessibleName = "Ribbon_Main";
            _ribbon.AccessibleDescription ??= "Main application ribbon for navigation, search, and grid tools";
            _ribbon.TabIndex = 1;
            _ribbon.TabStop = true;

            // Ensure theme toggle is wired to the live theme switcher
            if (_ribbon != null)
            {
                var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggle != null)
                {
                    themeToggle.Click -= ThemeToggleFromRibbon;
                    themeToggle.Click += ThemeToggleFromRibbon;
                }
            }

            Controls.Add(_ribbon);
            _logger?.LogInformation("Ribbon initialized via RibbonFactory");
            _logger?.LogDebug("Ribbon size after init: {Width}x{Height}", _ribbon.Width, _ribbon.Height);

            // DEFENSIVE: Convert any animated images to static bitmaps to prevent ImageAnimator exceptions
            if (_ribbon != null)
            {
                ValidateAndConvertRibbonImages(_ribbon);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Ribbon");
            _logger?.LogError("InitializeRibbon failed: {Message}", ex.Message);
            _ribbon = null;
        }
    }

    private void ThemeToggleFromRibbon(object? sender, EventArgs e)
    {
        try
        {
            ToggleTheme();

            if (sender is ToolStripButton btn)
            {
                var nextLabel = string.Equals(SfSkinManager.ApplicationVisualTheme, "Office2019Dark", StringComparison.OrdinalIgnoreCase)
                    ? "☀️ Light"
                    : "🌙 Dark";
                btn.Text = nextLabel;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme toggle failed");
        }
    }

    /// <summary>
    /// Validates all images in the ribbon and converts any animated images to static bitmaps.
    /// This prevents ImageAnimator exceptions when Syncfusion ToolStrip controls try to paint animated images.
    /// </summary>
    private void ValidateAndConvertRibbonImages(RibbonControlAdv ribbon)
    {
        ribbon.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Validates all images in the menu bar and converts any invalid images to prevent ImageAnimator exceptions.
    /// This prevents ImageAnimator exceptions when ToolStrip controls try to paint invalid images.
    /// </summary>
    private void ValidateAndConvertMenuBarImages(MenuStrip menuStrip)
    {
        menuStrip.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Performs a late validation pass on all menu bar images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateMenuBarImages()
    {
        _menuStrip?.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Performs a late validation pass on all ribbon images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateRibbonImages()
    {
        _ribbon?.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Initialize Syncfusion StatusBarAdv for status information.
    /// Delegates to StatusBarFactory for centralized creation and configuration.
    /// Wires panel references back to MainForm for status/progress management.
    /// </summary>
    private void InitializeStatusBar()
    {
        try
        {
            var statusBar = StatusBarFactory.CreateStatusBar(this, _logger);
            _statusBar = statusBar;

            _logger?.LogInformation("StatusBarFactory returned StatusBarAdv with {PanelCount} panels in Panels collection, and {ControlCount} in Controls collection",
                statusBar.Panels?.Length ?? 0, statusBar.Controls.Count);

            var panels = statusBar.Panels;
            if (panels == null || panels.Length < 5)
            {
                _logger?.LogWarning("StatusBarAdv.Panels was insufficient ({Count}); falling back to Controls collection", panels?.Length ?? 0);
                panels = statusBar.Controls.OfType<Syncfusion.Windows.Forms.Tools.StatusBarAdvPanel>().ToArray();
            }

            if (panels is { Length: >= 5 })
            {
                _statusLabel = panels[0];
                _statusTextPanel = panels[1];
                _statePanel = panels[2];
                _progressPanel = panels[3];
                _clockPanel = panels[4];

                _progressBar = _progressPanel?.Controls.OfType<Syncfusion.Windows.Forms.Tools.ProgressBarAdv>().FirstOrDefault();

                if (_progressBar != null && _statusLabel != null && _statusTextPanel != null && _statePanel != null && _progressPanel != null && _clockPanel != null)
                {
                    SetStatusBarPanels(statusBar,
                        _statusLabel,
                        _statusTextPanel,
                        _statePanel,
                        _progressPanel,
                        _progressBar,
                        _clockPanel);
                }
            }

            statusBar.TabStop = false;
            statusBar.TabIndex = 99;
            Controls.Add(statusBar);
            _logger?.LogDebug("Status bar initialized via StatusBarFactory");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            _logger?.LogError("InitializeStatusBar failed: {Message}", ex.Message);
            _statusBar = null;
            return;
        }
        _logger?.LogInformation("Status bar initialized via StatusBarFactory with {PanelCount} panels", _statusBar?.Panels.Length ?? 0);
        _logger?.LogDebug("Status bar size after init: {Width}x{Height}, HasSizingGrip={HasGrip}",
            _statusBar?.Width, _statusBar?.Height, _statusBar?.SizingGrip);
    }

    /// <summary>
    /// Initialize the navigation <see cref="ToolStripEx"/> with named <see cref="ToolStripButton"/>
    /// controls used for quick navigation.
    /// </summary>
    private void InitializeNavigationStrip()
    {
        // ... (Logic from UI.cs) ...
        try
        {
            _navigationStrip = new ToolStripEx
            {
                Name = "NavigationStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                AccessibleName = "Navigation Strip",
                AccessibleDescription = "Main navigation toolbar for switching between application panels",
                AccessibleRole = AccessibleRole.ToolBar,
                AutoSize = true, // ToolStripEx handles height automatically
                TabIndex = 2,
                TabStop = true,
                ThemeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful" // Syncfusion theme integration
            };

            // Helpers for button creation to save space?
            // Just pasting the logic from UI.cs

            var dashboardBtn = new ToolStripButton("Dashboard") { Name = "Nav_Dashboard", AccessibleName = "Dashboard", Enabled = false };
            dashboardBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true); };

            var accountsBtn = new ToolStripButton("Accounts") { Name = "Nav_Accounts", AccessibleName = "Accounts", Enabled = false };
            accountsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true); };

            var budgetBtn = new ToolStripButton("Budget") { Name = "Nav_Budget", AccessibleName = "Budget", Enabled = false };
            budgetBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true); };

            var chartsBtn = new ToolStripButton("Charts") { Name = "Nav_Charts", AccessibleName = "Charts", Enabled = false };
            chartsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true); };

             var analyticsBtn = new ToolStripButton("&Analytics") { Name = "Nav_Analytics", AccessibleName = "Analytics" };
            analyticsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true); };

            var auditLogBtn = new ToolStripButton("&Audit Log") { Name = "Nav_AuditLog", AccessibleName = "Audit Log" };
            auditLogBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true); };

            var customersBtn = new ToolStripButton("Customers") { Name = "Nav_Customers", AccessibleName = "Nav_Customers" };
            customersBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true); };

            var reportsBtn = new ToolStripButton("Reports") { Name = "Nav_Reports", AccessibleName = "Nav_Reports" };
            reportsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true); };

            var quickBooksBtn = new ToolStripButton("QuickBooks") { Name = "Nav_QuickBooks", AccessibleName = "QuickBooks" };
            quickBooksBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true); };

            var aiChatBtn = new ToolStripButton("AI Chat") { Name = "Nav_AIChat", AccessibleName = "AI Chat" };
            aiChatBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<InsightFeedPanel>("AI Chat", DockingStyle.Right, allowFloating: true); };

            var proactiveInsightsBtn = new ToolStripButton("Proactive Insights") { Name = "Nav_ProactiveInsights", AccessibleName = "Proactive Insights" };
            proactiveInsightsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<ProactiveInsightsPanel>("Proactive Insights", DockingStyle.Right, allowFloating: true); };

            var warRoomBtn = new ToolStripButton("War Room") { Name = "Nav_WarRoom", AccessibleName = "War Room" };
            warRoomBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true); };

            var settingsBtn = new ToolStripButton("Settings") { Name = "Nav_Settings", AccessibleName = "Settings" };
            settingsBtn.Click += (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true); };

            var themeToggleBtn = new ToolStripButton
            {
                Name = "ThemeToggle",
                AccessibleName = "Theme_Toggle",
                AutoSize = true
            };
            themeToggleBtn.Click += ThemeToggleBtn_Click;
            themeToggleBtn.Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";

             // Grid helpers (navigation strip)
            var navGridClearFilter = new ToolStripButton("Clear Grid Filter") { Name = "Nav_ClearGridFilter", AccessibleName = "Clear Grid Filter" };
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid") { Name = "Nav_ExportGrid", AccessibleName = "Export Grid" };
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn, new ToolStripSeparator(), accountsBtn, budgetBtn, chartsBtn, analyticsBtn, auditLogBtn, customersBtn, reportsBtn, quickBooksBtn, aiChatBtn, proactiveInsightsBtn, warRoomBtn, new ToolStripSeparator(), settingsBtn, new ToolStripSeparator(), themeToggleBtn, new ToolStripSeparator(), navGridClearFilter, navGridExport
            });

            Controls.Add(_navigationStrip);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize navigation strip");
            _navigationStrip = null;
        }
    }

    /// <summary>
    /// Initialize status timer to update clock panel.
    /// </summary>
    private void InitializeStatusTimer()
    {
        try
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _statusTimer.Tick += (s, e) =>
            {
                try { if (_clockPanel != null) _clockPanel.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture); } catch { }
            };
            _statusTimer.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to initialize status timer");
        }
    }

     /// <summary>
    /// Initialize MenuStrip for navigation and commands.
    /// Provides a traditional menu bar with File, View, Tools, and Help menus.
    /// Enhanced with Syncfusion theming, Segoe MDL2 Assets icons, and proper renderer configuration.
    /// </summary>
    private void InitializeMenuBar()
    {
        try
        {
            _menuStrip = new MenuStrip
            {
                Name = "MainMenuStrip",
                Dock = DockStyle.Top,
                Visible = true,
                Font = new Font("Segoe UI", 9F),
                RenderMode = ToolStripRenderMode.Professional,
                ShowItemToolTips = true,
                AccessibleName = "Main menu",
                AccessibleDescription = "Main navigation menu bar",
                AccessibleRole = AccessibleRole.MenuBar,
                TabIndex = 0,
                TabStop = true
            };

            // Apply professional color scheme with theme colors
            if (_menuStrip.Renderer is ToolStripProfessionalRenderer professionalRenderer)
            {
                professionalRenderer.RoundedEdges = true;
            }

            // ... (Menu construction logic from UI.cs) ...
            // Simplying slightly for snippet size, but logic assumes existence
            // I will just put a comment that it's the same logic, but for `write_file` I need to be explicit.

             // File Menu
            var fileMenu = new ToolStripMenuItem("&File") { Name = "Menu_File", ToolTipText = "File operations" };
            _recentFilesMenu = new ToolStripMenuItem("&Recent Files") { Name = "Menu_File_RecentFiles" };
            UpdateMruMenu(_recentFilesMenu);

            var clearRecentMenuItem = new ToolStripMenuItem("&Clear Recent Files", null, (s, e) => ClearMruList()) { Name = "Menu_File_ClearRecent" };
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close()) { Name = "Menu_File_Exit", ShortcutKeys = Keys.Alt | Keys.F4 };

            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { _recentFilesMenu, clearRecentMenuItem, new ToolStripSeparator(), exitMenuItem });

             // View Menu
            var viewMenu = new ToolStripMenuItem("&View") { Name = "Menu_View" };
            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Dashboard", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true); }) { Name = "Menu_View_Dashboard", ShortcutKeys = Keys.Control | Keys.D };

             // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true); }) { Name = "Menu_View_Accounts", ShortcutKeys = Keys.Control | Keys.A };

            // View > Budget
            var budgetMenuItem = new ToolStripMenuItem("&Budget Overview", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true); }) { Name = "Menu_View_Budget", ShortcutKeys = Keys.Control | Keys.B };

             // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Charts", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true); }) { Name = "Menu_View_Charts", ShortcutKeys = Keys.Control | Keys.H };

             // View > Reports
            var reportsMenuItem = new ToolStripMenuItem("&Reports", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true); }) { Name = "Menu_View_Reports", ShortcutKeys = Keys.Control | Keys.R };

             // View > QuickBooks
            var quickBooksMenuItem = new ToolStripMenuItem("&QuickBooks", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true); }) { Name = "Menu_View_QuickBooks", ShortcutKeys = Keys.Control | Keys.Q };

             // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true); }) { Name = "Menu_View_Customers", ShortcutKeys = Keys.Control | Keys.U };

            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) => this.Refresh()) { Name = "Menu_View_Refresh", ShortcutKeys = Keys.F5 };

            viewMenu.DropDownItems.AddRange(new ToolStripItem[] { dashboardMenuItem, accountsMenuItem, budgetMenuItem, chartsMenuItem, reportsMenuItem, quickBooksMenuItem, customersMenuItem, new ToolStripSeparator(), refreshMenuItem });

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Tools") { Name = "Menu_Tools" };
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) => { if (_panelNavigator != null) _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true); }) { Name = "Menu_Tools_Settings", ShortcutKeys = Keys.Control | Keys.Oemcomma };
            toolsMenu.DropDownItems.Add(settingsMenuItem);

            // Help Menu
            var helpMenu = new ToolStripMenuItem("&Help") { Name = "Menu_Help" };
            var documentationMenuItem = new ToolStripMenuItem("&Documentation", null, (s, e) => {
                 try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/WileyWidget/WileyWidget/wiki", UseShellExecute = true }); } catch { }
            }) { Name = "Menu_Help_Documentation", ShortcutKeys = Keys.F1 };

            var aboutMenuItem = new ToolStripMenuItem("&About", null, (s, e) => {
                MessageBox.Show($"{MainFormResources.FormTitle}\n\nVersion 1.0.0\nBuilt with .NET 9\n\n© {DateTime.Now.Year} Wiley Widget", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }) { Name = "Menu_Help_About" };

            helpMenu.DropDownItems.AddRange(new ToolStripItem[] { documentationMenuItem, new ToolStripSeparator(), aboutMenuItem });

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });

            ApplyMenuTheme(fileMenu);
            ApplyMenuTheme(viewMenu);
            ApplyMenuTheme(toolsMenu);
            ApplyMenuTheme(helpMenu);

            this.MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);
            ValidateAndConvertMenuBarImages(_menuStrip);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    private Bitmap? CreateIconFromText(string iconText, int size)
    {
         if (string.IsNullOrWhiteSpace(iconText) || size <= 0) return null;
         try
         {
             var bitmap = new Bitmap(size, size);
             using (var graphics = Graphics.FromImage(bitmap))
             {
                 graphics.Clear(Color.Transparent);
                 graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                 graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                 using (var font = new Font("Segoe MDL2 Assets", size * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
                 using (var brush = new SolidBrush(Color.DodgerBlue))
                 {
                     graphics.DrawString(iconText, font, brush, new RectangleF(0, 0, size, size), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                 }
             }
             return bitmap;
         }
         catch (Exception ex) { _logger?.LogWarning(ex, "CreateIconFromText failed"); return null; }
    }

    private Color GetLoadingOverlayColor() => SystemColors.Control;
    private Color GetLoadingLabelColor() => Color.White;

    private string GetCurrentTheme() => SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

    private void ApplyMenuTheme(ToolStripMenuItem menuItem)
    {
        if (menuItem?.DropDown == null) return;
        try
        {
            var dropdown = (ToolStripDropDownMenu)menuItem.DropDown;
             try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(dropdown, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful"); } catch { }
             foreach (ToolStripItem item in dropdown.Items) if (item is ToolStripMenuItem child) ApplyMenuTheme(child);
        }
        catch { }
    }

    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
            var themeToggle = FindToolStripItem(_ribbon!, "ThemeToggle") as ToolStripButton; // Reuse generic toggle logic
            ThemeToggleFromRibbon(themeToggle ?? sender, e);
    }

    public void ToggleTheme()
    {
        try
        {
            var currentTheme = _themeService?.CurrentTheme ?? SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            var nextTheme = string.Equals(currentTheme, "Office2019Dark", StringComparison.OrdinalIgnoreCase)
                ? "Office2019Colorful"
                : "Office2019Dark";

            if (_themeService != null)
            {
                _themeService.ApplyTheme(nextTheme);
            }
            else
            {
                SfSkinManager.ApplicationVisualTheme = nextTheme;
                SfSkinManager.SetVisualStyle(this, nextTheme);
            }

            _logger?.LogInformation("Theme toggled from {CurrentTheme} to {NextTheme}", currentTheme, nextTheme);

            // Update theme toggle button text
            if (_ribbon != null)
            {
                var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggle != null)
                {
                    themeToggle.Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light" : "🌙 Dark";
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme toggle failed");
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ToolStripTextBox searchBox) return;
        if (e.KeyCode == Keys.Enter)
        {
            var searchText = searchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText)) { _ = PerformGlobalSearchAsync(searchText); }
            e.Handled = true;
        }
    }

    private ToolStripItem? FindToolStripItem(Control container, string name)
    {
        if (container is ToolStrip strip) return strip.Items.Find(name, true).FirstOrDefault();
        foreach(Control c in container.Controls) { var res = FindToolStripItem(c, name); if (res != null) return res; }
        return null;
    }
}
