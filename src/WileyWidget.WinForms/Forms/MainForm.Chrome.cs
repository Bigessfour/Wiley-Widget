using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// MainForm partial class for UI chrome initialization (Ribbon, Status Bar, Navigation).
/// Implements Syncfusion-based chrome elements with proper theming and AutomationId for testing.
/// </summary>
public partial class MainForm
{
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

        try
        {
            SuspendLayout();

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ThemeColors.Background;
            Name = "MainForm";

            // Initialize components container if needed
            components ??= new Container();

            // Initialize Ribbon
            if (!_isUiTestHarness)
            {
                InitializeRibbon();
            }

            // Initialize Status Bar
            InitializeStatusBar();

            // Initialize Navigation Strip (alternative to Ribbon for test harness)
            if (_isUiTestHarness)
            {
                InitializeNavigationStrip();
            }

            // Start status timer
            InitializeStatusTimer();

            ResumeLayout(false);
            PerformLayout();

            _logger?.LogDebug("UI chrome initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
            // Continue without chrome - application can still function
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for main navigation.
    /// </summary>
    private void InitializeRibbon()
    {
        try
        {
            _ribbon = new RibbonControlAdv
            {
                Name = "Ribbon_Main",
                AccessibleName = "Ribbon_Main",
                Dock = (DockStyleEx)DockStyle.Top,
                OfficeColorScheme = ToolStripEx.ColorScheme.Managed,
                RibbonStyle = RibbonStyle.Office2016
            };

            // Apply theme
            try
            {
                SfSkinManager.SetVisualStyle(_ribbon, ThemeColors.DefaultTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to apply theme to Ribbon");
            }

            // Create Home Tab
            _homeTab = new ToolStripTabItem
            {
                Name = "HomeTab",
                Text = "&Home"
            };

            // Create Home Tab Panel
            var homePanel = new ToolStripEx
            {
                Name = "HomePanel"
            };

            // Add navigation buttons
            var dashboardBtn = new ToolStripButton
            {
                Name = "Nav_Dashboard",
                Text = MainFormResources.Dashboard,
                ToolTipText = "Open Dashboard",
                AutoSize = true
            };
            dashboardBtn.Click += (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);

            var accountsBtn = new ToolStripButton
            {
                Name = "Nav_Accounts",
                Text = MainFormResources.Accounts,
                ToolTipText = "Open Accounts",
                AutoSize = true
            };
            accountsBtn.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false);

            var chartsBtn = new ToolStripButton
            {
                Name = "Nav_Charts",
                Text = MainFormResources.Charts,
                ToolTipText = "Open Charts",
                AutoSize = true
            };
            chartsBtn.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false);

            var reportsBtn = new ToolStripButton
            {
                Name = "Nav_Reports",
                Text = MainFormResources.Reports,
                ToolTipText = "Open Reports",
                AutoSize = true
            };
            reportsBtn.Click += (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false);

            var settingsBtn = new ToolStripButton
            {
                Name = "Nav_Settings",
                Text = MainFormResources.Settings,
                ToolTipText = "Open Settings",
                AutoSize = true
            };
            settingsBtn.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false);

            homePanel.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                chartsBtn,
                reportsBtn,
                new ToolStripSeparator(),
                settingsBtn
            });

            _homeTab.Panel.AddToolStrip(homePanel);
            _ribbon.Header.AddMainItem(_homeTab);

            Controls.Add(_ribbon);
            _logger?.LogDebug("Ribbon initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Ribbon");
            _ribbon = null;
        }
    }

    /// <summary>
    /// Initialize Syncfusion StatusBarAdv for status information.
    /// </summary>
    private void InitializeStatusBar()
    {
        try
        {
            _statusBar = new StatusBarAdv
            {
                Name = "StatusBar_Main",
                AccessibleName = "StatusBar_Main",
                Dock = DockStyle.Bottom,
                BeforeTouchSize = new Size(1400, 26)
            };

            // Apply theme
            try
            {
                SfSkinManager.SetVisualStyle(_statusBar, ThemeColors.DefaultTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to apply theme to StatusBar");
            }

            // Status label (left)
            _statusLabel = new StatusBarAdvPanel
            {
                Name = "StatusLabel",
                Text = "Ready",
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Left
            };

            // Status text panel (center)
            _statusTextPanel = new StatusBarAdvPanel
            {
                Name = "StatusTextPanel",
                Text = string.Empty,
                Size = new System.Drawing.Size(200, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Center
            };

            // State panel (MDI/Docking indicator)
            _statePanel = new StatusBarAdvPanel
            {
                Name = "StatePanel",
                Text = string.Empty,
                Size = new System.Drawing.Size(100, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Left
            };

            // Clock panel (right)
            _clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
                Size = new System.Drawing.Size(80, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Right
            };

            _statusBar.Controls.Add(_statusLabel);
            _statusBar.Controls.Add(_statusTextPanel);
            _statusBar.Controls.Add(_statePanel);
            _statusBar.Controls.Add(_clockPanel);

            Controls.Add(_statusBar);
            _logger?.LogDebug("Status bar initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            _statusBar = null;
        }
    }

    /// <summary>
    /// Initialize simple navigation strip for test harness mode.
    /// </summary>
    private void InitializeNavigationStrip()
    {
        try
        {
            _navigationStrip = new ToolStripEx
            {
                Name = "NavigationStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden
            };

            var dashboardBtn = new ToolStripButton("Dashboard") { Name = "Nav_Dashboard" };
            dashboardBtn.Click += (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);

            var accountsBtn = new ToolStripButton("Accounts") { Name = "Nav_Accounts" };
            accountsBtn.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false);

            var chartsBtn = new ToolStripButton("Charts") { Name = "Nav_Charts" };
            chartsBtn.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false);

            var reportsBtn = new ToolStripButton("Reports") { Name = "Nav_Reports" };
            reportsBtn.Click += (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false);

            var settingsBtn = new ToolStripButton("Settings") { Name = "Nav_Settings" };
            settingsBtn.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false);

            var dockingToggleBtn = new ToolStripButton("Docking") { Name = "Nav_DockingToggle" };
            dockingToggleBtn.Click += (s, e) => ToggleDocking();

            var mdiToggleBtn = new ToolStripButton("MDI") { Name = "Nav_MdiToggle" };
            mdiToggleBtn.Click += (s, e) => ToggleMdiMode();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                chartsBtn,
                reportsBtn,
                new ToolStripSeparator(),
                settingsBtn,
                new ToolStripSeparator(),
                dockingToggleBtn,
                mdiToggleBtn
            });

            Controls.Add(_navigationStrip);
            _logger?.LogDebug("Navigation strip initialized successfully");
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
            _statusTimer = new System.Windows.Forms.Timer
            {
                Interval = 60000 // Update every minute
            };
            _statusTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_clockPanel != null)
                    {
                        _clockPanel.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    // Suppress timer errors
                }
            };
            _statusTimer.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to initialize status timer");
        }
    }

    /// <summary>
    /// Toggle docking mode (for UI testing)
    /// </summary>
    private void ToggleDocking()
    {
        ToggleDockingMode();
        UpdateDockingStateText();
    }

    /// <summary>
    /// Toggle MDI mode (for UI testing)
    /// </summary>
    private void ToggleMdiMode()
    {
        // Simple toggle for testing - in real usage this would be more complex
        _useMdiMode = !_useMdiMode;
        UpdateDockingStateText();
    }

}
