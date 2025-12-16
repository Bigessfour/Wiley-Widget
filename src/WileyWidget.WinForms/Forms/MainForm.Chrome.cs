using Microsoft.Extensions.Configuration;
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
using WileyWidget.ViewModels;

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

            // Apply theme from configuration before initializing controls
            var configuredTheme = _configuration?.GetValue<string>("UI:Theme", ThemeColors.DefaultTheme) ?? ThemeColors.DefaultTheme;
            try
            {
                SfSkinManager.SetVisualStyle(this, configuredTheme);
                SfSkinManager.ApplicationVisualTheme = configuredTheme;
                _logger?.LogInformation("Applied theme from configuration: {Theme}", configuredTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to apply configured theme '{Theme}'; using default", configuredTheme);
            }

            // Validate Syncfusion license status
            ValidateSyncfusionLicense();

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ThemeColors.Background;
            Name = "MainForm";

            // Initialize components container if needed
            components ??= new Container();

            // Initialize Menu Bar (always available)
            InitializeMenuBar();

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

            // Ensure Panel is initialized
            if (_homeTab.Panel == null)
            {
                _homeTab.Panel = new RibbonPanel();
                // Note: Theme is inherited from parent ribbon, no need to apply separately
            }

            // Create Home Tab Panel
            var homePanel = new ToolStripEx
            {
                Name = "HomePanel"
            };

            // Add navigation buttons
            var dashboardBtn = new ToolStripButton
            {
                Name = "Nav_Dashboard",
                Text = "📊 " + MainFormResources.Dashboard,
                ToolTipText = "Open Dashboard",
                AutoSize = true
            };
            dashboardBtn.Click += (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);

            var accountsBtn = new ToolStripButton
            {
                Name = "Nav_Accounts",
                Text = "💼 " + MainFormResources.Accounts,
                ToolTipText = "Open Accounts",
                AutoSize = true
            };
            accountsBtn.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false);

            var chartsBtn = new ToolStripButton
            {
                Name = "Nav_Charts",
                Text = "📈 " + MainFormResources.Charts,
                ToolTipText = "Open Charts",
                AutoSize = true
            };
            chartsBtn.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false);

            var reportsBtn = new ToolStripButton
            {
                Name = "Nav_Reports",
                Text = "📄 " + MainFormResources.Reports,
                ToolTipText = "Open Reports",
                AutoSize = true
            };
            reportsBtn.Click += (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false);

            var settingsBtn = new ToolStripButton
            {
                Name = "Nav_Settings",
                Text = "⚙️ " + MainFormResources.Settings,
                ToolTipText = "Open Settings",
                AutoSize = true
            };
            settingsBtn.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false);

            // Theme toggle button - set initial text based on current theme
            var currentTheme = SfSkinManager.ApplicationVisualTheme;
            var themeToggleBtn = new ToolStripButton
            {
                Name = "Theme_Toggle",
                Text = currentTheme == "Office2019Dark" ? "☀ Light Theme" : "🌙 Dark Theme",
                ToolTipText = $"Switch to {(currentTheme == "Office2019Dark" ? "Light" : "Dark")} theme",
                AutoSize = true
            };
            themeToggleBtn.Click += ThemeToggleBtn_Click;

            homePanel.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                chartsBtn,
                reportsBtn,
                new ToolStripSeparator(),
                settingsBtn,
                themeToggleBtn,
                new ToolStripSeparator(),
                // Grid commands
                new ToolStripLabel { Text = "Grid:" , Name = "Grid_Label" },
                new ToolStripButton { Name = "Grid_SortAsc", Text = "Sort Asc", AutoSize = true },
                new ToolStripButton { Name = "Grid_SortDesc", Text = "Sort Desc", AutoSize = true },
                new ToolStripButton { Name = "Grid_ApplyTestFilter", Text = "Apply Filter", AutoSize = true },
                new ToolStripButton { Name = "Grid_ClearFilter", Text = "Clear Filter", AutoSize = true },
                new ToolStripButton { Name = "Grid_ExportExcel", Text = "Export Grid", AutoSize = true }
            });

            // Wire grid command events
            var sortAscBtn = homePanel.Items.Find("Grid_SortAsc", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var sortDescBtn = homePanel.Items.Find("Grid_SortDesc", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var applyFilterBtn = homePanel.Items.Find("Grid_ApplyTestFilter", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var clearFilterBtn = homePanel.Items.Find("Grid_ClearFilter", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var exportGridBtn = homePanel.Items.Find("Grid_ExportExcel", searchAllChildren: true).FirstOrDefault() as ToolStripButton;

            if (sortAscBtn != null)
                sortAscBtn.Click += (s, e) => SortActiveGridByFirstSortableColumn(descending: false);
            if (sortDescBtn != null)
                sortDescBtn.Click += (s, e) => SortActiveGridByFirstSortableColumn(descending: true);
            if (applyFilterBtn != null)
                applyFilterBtn.Click += (s, e) => ApplyTestFilterToActiveGrid();
            if (clearFilterBtn != null)
                clearFilterBtn.Click += (s, e) => ClearActiveGridFilter();
            if (exportGridBtn != null)
                exportGridBtn.Click += async (s, e) => await ExportActiveGridToExcel();

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

            // Theme toggle button - add emoji to match ribbon version
            var currentTheme = SfSkinManager.ApplicationVisualTheme;
            var themeToggleText = currentTheme == "Office2019Dark" ? "☀ Light Theme" : "🌙 Dark Theme";
            var themeToggleBtn = new ToolStripButton(themeToggleText) { Name = "Theme_Toggle" };
            themeToggleBtn.Click += ThemeToggleBtn_Click;

            // Grid test helpers (navigation strip)
            var navGridApplyFilter = new ToolStripButton("Apply Grid Filter") { Name = "Nav_ApplyGridFilter" };
            navGridApplyFilter.Click += (s, e) => ApplyTestFilterToActiveGrid();

            var navGridClearFilter = new ToolStripButton("Clear Grid Filter") { Name = "Nav_ClearGridFilter" };
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid") { Name = "Nav_ExportGrid" };
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                chartsBtn,
                reportsBtn,
                new ToolStripSeparator(),
                settingsBtn,
                themeToggleBtn,
                new ToolStripSeparator(),
                dockingToggleBtn,
                mdiToggleBtn,
                new ToolStripSeparator(),
                navGridApplyFilter,
                navGridClearFilter,
                navGridExport
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
        // Simple toggle for testing - use the public property so ApplyMdiMode runs
        UseMdiMode = !UseMdiMode;
        UpdateDockingStateText();
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
                AllowMerge = true,
                Font = new Font("Segoe UI", 9F),
                RenderMode = ToolStripRenderMode.Professional,
                ShowItemToolTips = true,
                AccessibleName = "MainMenuStrip",
                AccessibleDescription = "Main navigation menu bar"
            };

            // Apply professional color scheme with theme colors
            if (_menuStrip.Renderer is ToolStripProfessionalRenderer professionalRenderer)
            {
                professionalRenderer.RoundedEdges = true;
            }

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File")
            {
                Name = "Menu_File",
                ToolTipText = "File operations"
            };

            // File > Exit
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close())
            {
                Name = "Menu_File_Exit",
                ShortcutKeys = Keys.Alt | Keys.F4,
                ToolTipText = "Exit the application (Alt+F4)",
                Image = CreateIconFromText("\uE8BB", 16), // Exit icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            fileMenu.DropDownItems.Add(exitMenuItem);

            // View Menu - All child forms accessible here
            var viewMenu = new ToolStripMenuItem("&View")
            {
                Name = "Menu_View",
                ToolTipText = "Open application views"
            };

            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Dashboard", null, (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Dashboard",
                ShortcutKeys = Keys.Control | Keys.D,
                ToolTipText = "Open Dashboard view (Ctrl+D)",
                Image = CreateIconFromText("\uE10F", 16), // Dashboard icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Accounts",
                ShortcutKeys = Keys.Control | Keys.A,
                ToolTipText = "Open Accounts view (Ctrl+A)",
                Image = CreateIconFromText("\uE8F4", 16), // AccountActivity icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Budget Overview
            var budgetMenuItem = new ToolStripMenuItem("&Budget Overview", null, (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Budget",
                ShortcutKeys = Keys.Control | Keys.B,
                ToolTipText = "Open Budget Overview (Ctrl+B)",
                Image = CreateIconFromText("\uE7C8", 16), // Money icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Charts", null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Charts",
                ShortcutKeys = Keys.Control | Keys.H,
                ToolTipText = "Open Charts view (Ctrl+H)",
                Image = CreateIconFromText("\uE9D2", 16), // BarChart icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Reports
            var reportsMenuItem = new ToolStripMenuItem("&Reports", null, (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Reports",
                ShortcutKeys = Keys.Control | Keys.R,
                ToolTipText = "Open Reports view (Ctrl+R)",
                Image = CreateIconFromText("\uE8A5", 16), // Document icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) => ShowChildForm<CustomersForm, CustomersViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Customers",
                ShortcutKeys = Keys.Control | Keys.U,
                ToolTipText = "Open Customers view (Ctrl+U)",
                Image = CreateIconFromText("\uE716", 16), // Contact icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // Add separator for visual grouping
            var viewSeparator = new ToolStripSeparator
            {
                Name = "Menu_View_Separator"
            };

            // View > Refresh
            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) =>
            {
                // Refresh active child form if any
                if (ActiveMdiChild is Form activeChild)
                {
                    activeChild.Refresh();
                }
            })
            {
                Name = "Menu_View_Refresh",
                ShortcutKeys = Keys.F5,
                ToolTipText = "Refresh active view (F5)",
                Image = CreateIconFromText("\uE72C", 16), // Refresh icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            viewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                dashboardMenuItem,
                accountsMenuItem,
                budgetMenuItem,
                chartsMenuItem,
                reportsMenuItem,
                customersMenuItem,
                viewSeparator,
                refreshMenuItem
            });

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Tools")
            {
                Name = "Menu_Tools",
                ToolTipText = "Application tools and settings"
            };

            // Tools > Settings
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false))
            {
                Name = "Menu_Tools_Settings",
                ShortcutKeys = Keys.Control | Keys.Oemcomma,
                ToolTipText = "Open Settings (Ctrl+,)",
                Image = CreateIconFromText("\uE713", 16), // Settings icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            toolsMenu.DropDownItems.Add(settingsMenuItem);

            // Help Menu
            var helpMenu = new ToolStripMenuItem("&Help")
            {
                Name = "Menu_Help",
                ToolTipText = "Help and application information"
            };

            // Help > Documentation
            var documentationMenuItem = new ToolStripMenuItem("&Documentation", null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/WileyWidget/WileyWidget/wiki",
                        UseShellExecute = true
                    });
                }
                catch (Exception docEx)
                {
                    _logger?.LogWarning(docEx, "Failed to open documentation");
                }
            })
            {
                Name = "Menu_Help_Documentation",
                ShortcutKeys = Keys.F1,
                ToolTipText = "Open online documentation (F1)",
                Image = CreateIconFromText("\uE897", 16), // Help icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            var helpSeparator = new ToolStripSeparator
            {
                Name = "Menu_Help_Separator"
            };

            // Help > About
            var aboutMenuItem = new ToolStripMenuItem("&About", null, (s, e) =>
            {
                MessageBox.Show(
                    $"{MainFormResources.FormTitle}\n\n" +
                    "Version 1.0.0\n" +
                    "Built with .NET 9 and Syncfusion WinForms\n\n" +
                    $"© {DateTime.Now.Year} Wiley Widget. All rights reserved.",
                    $"About {MainFormResources.FormTitle}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            })
            {
                Name = "Menu_Help_About",
                ToolTipText = "About this application",
                Image = CreateIconFromText("\uE946", 16), // Info icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            helpMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                documentationMenuItem,
                helpSeparator,
                aboutMenuItem
            });

            // Add all menus to the menu strip
            _menuStrip.Items.AddRange(new ToolStripItem[]
            {
                fileMenu,
                viewMenu,
                toolsMenu,
                helpMenu
            });

            // Apply theme colors to dropdown menus
            ApplyMenuTheme(fileMenu);
            ApplyMenuTheme(viewMenu);
            ApplyMenuTheme(toolsMenu);
            ApplyMenuTheme(helpMenu);

            // Set as the form's main menu
            this.MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);

            _logger?.LogDebug("Enhanced menu bar initialized successfully with icons and theming");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    /// <summary>
    /// Create a Bitmap icon from Segoe MDL2 Assets text (Unicode character).
    /// </summary>
    /// <param name="iconText">Unicode character from Segoe MDL2 Assets font</param>
    /// <param name="size">Icon size in pixels</param>
    /// <returns>Bitmap containing the rendered icon</returns>
    private Bitmap CreateIconFromText(string iconText, int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            using (var font = new Font("Segoe MDL2 Assets", size * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(ThemeColors.PrimaryAccent))
            {
                var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                graphics.DrawString(iconText, font, brush, new RectangleF(0, 0, size, size), stringFormat);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Apply theme colors to menu dropdown items.
    /// </summary>
    /// <param name="menuItem">Parent menu item to theme</param>
    private void ApplyMenuTheme(ToolStripMenuItem menuItem)
    {
        if (menuItem?.DropDown == null)
        {
            return;
        }

        try
        {
            var dropdown = (ToolStripDropDownMenu)menuItem.DropDown;
            dropdown.ShowImageMargin = true;
            dropdown.ShowCheckMargin = false;
            dropdown.Font = new Font("Segoe UI", 9F);
            dropdown.BackColor = Color.White;

            // Apply theme to child items
            foreach (ToolStripItem item in dropdown.Items)
            {
                if (item is ToolStripMenuItem childMenuItem)
                {
                    childMenuItem.BackColor = Color.White;
                    childMenuItem.ForeColor = Color.FromArgb(32, 32, 32);

                    // Recursively apply to sub-items
                    ApplyMenuTheme(childMenuItem);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to apply theme to menu item {MenuName}", menuItem.Name);
        }
    }

    /// <summary>
    /// Handle theme toggle button click - switches between Dark and Light themes.
    /// </summary>
    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            var currentTheme = SfSkinManager.ApplicationVisualTheme;
            var newTheme = currentTheme == "Office2019Dark" ? "Office2019Colorful" : "Office2019Dark";

            // Apply new theme globally
            SfSkinManager.ApplicationVisualTheme = newTheme;

            // Note: Configuration is read-only at runtime. Theme preference persists for session only.
            // For permanent persistence, implement user settings file or registry storage.
            _logger?.LogInformation("Theme switched to {NewTheme} (session only)", newTheme);

            // Update theme toggle button text
            var themeBtn = sender as ToolStripButton;
            if (themeBtn != null)
            {
                themeBtn.Text = newTheme == "Office2019Dark" ? "☀ Light Theme" : "🌙 Dark Theme";
                themeBtn.ToolTipText = $"Switch to {(newTheme == "Office2019Dark" ? "Light" : "Dark")} theme";
            }

            // Refresh all open forms to apply new theme
            foreach (Form form in Application.OpenForms)
            {
                try
                {
                    SfSkinManager.SetVisualStyle(form, newTheme);
                    form.Refresh();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to apply theme to form {FormName}", form.Name);
                }
            }

            _logger?.LogInformation("Theme switched from {OldTheme} to {NewTheme}", currentTheme, newTheme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to toggle theme");
        }
    }

    /// <summary>
    /// Validates Syncfusion license status and logs warning if trial/unlicensed.
    /// </summary>
    private void ValidateSyncfusionLicense()
    {
        try
        {
            // Check if Syncfusion license is registered
            var licenseProvider = typeof(Syncfusion.Licensing.SyncfusionLicenseProvider);
            var versionField = licenseProvider.GetField("version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (versionField?.GetValue(null) == null)
            {
                _logger?.LogWarning("Syncfusion license not detected. Application may display trial watermarks. " +
                    "Register license key in Program.cs or secrets/syncfusion_license_key.txt");
            }
            else
            {
                _logger?.LogInformation("Syncfusion license validated successfully");
            }
        }
        catch (Exception ex)
        {
            // Non-critical - log and continue
            _logger?.LogDebug(ex, "Could not validate Syncfusion license status");
        }
    }

}
