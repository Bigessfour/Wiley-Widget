using Microsoft.Extensions.Configuration;
using Syncfusion.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms;

[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
/// <summary>
/// Partial UI implementation for <see cref="MainForm"/> containing UI element initialization
/// helpers (chrome, ribbons, navigation strip, status bar) and visual theming helpers.
/// </summary>
public partial class MainForm
{
    #region UI Fields
    private DockingManager? _dockingManager;
    private DockingLayoutManager? _dockingLayoutManager;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _activityGrid;
    private System.Windows.Forms.Timer? _activityRefreshTimer;
    private Panel? _loadingOverlay; // Full-screen loading overlay for async operations
    private Label? _loadingLabel; // Loading message label

    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";

#if DEBUG
    // Diagnostic constants - only compiled in debug builds
    private const int LayoutLoadTimeoutMs = 1000; // Auto-reset if load takes > 1 second
    private const int LayoutLoadWarningMs = 500;  // Log warning if load takes > 500ms
#endif

    // Font family constant for UI fonts
    private const string SegoeUiFontName = "Segoe UI";
    #endregion

    #region Chrome

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

        var chromeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogInformation("InitializeChrome start - handleCreated={HandleCreated}", IsHandleCreated);
        Console.WriteLine($"[DIAGNOSTIC] InitializeChrome: started, handleCreated={IsHandleCreated}");

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
            Console.WriteLine($"[DIAGNOSTIC] Form properties set: Size={Width}x{Height}, MinSize={MinimumSize.Width}x{MinimumSize.Height}");

            // Initialize components container if needed
            components ??= new Container();
            Console.WriteLine("[DIAGNOSTIC] Components container initialized");

            // Initialize Menu Bar (always available)
            InitializeMenuBar();
            Console.WriteLine("[DIAGNOSTIC] Menu bar initialized");

            // Initialize Ribbon
            if (!_uiConfig.IsUiTestHarness)
            {
                try
                {
                    InitializeRibbon();
                    Console.WriteLine("[DIAGNOSTIC] Ribbon initialized");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize Ribbon");
                    Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeRibbon failed: {ex.Message}");
                    _ribbon = null;
                }
            }

            // Initialize Status Bar
            InitializeStatusBar();
            Console.WriteLine("[DIAGNOSTIC] Status bar initialized");

            // Initialize Navigation Strip (alternative to Ribbon for test harness)
            if (_uiConfig.IsUiTestHarness)
            {
                InitializeNavigationStrip();
                Console.WriteLine("[DIAGNOSTIC] Navigation strip initialized (UI test harness mode)");
            }

            // Start status timer
            InitializeStatusTimer();
            Console.WriteLine("[DIAGNOSTIC] Status timer initialized");

            // Initialize loading overlay (always last to cover everything)
            InitializeLoadingOverlay();
            Console.WriteLine("[DIAGNOSTIC] Loading overlay initialized");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeChrome failed: {ex.Message}");
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
            chromeStopwatch.Stop();
            _logger?.LogInformation("InitializeChrome complete in {ElapsedMs}ms - Size={Width}x{Height}, MinSize={MinWidth}x{MinHeight}",
                chromeStopwatch.ElapsedMilliseconds,
                Width,
                Height,
                MinimumSize.Width,
                MinimumSize.Height);
            Console.WriteLine($"[DIAGNOSTIC] InitializeChrome complete in {chromeStopwatch.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for primary navigation, global search, and session theme toggle.
    /// Delegates to RibbonFactory for centralized creation logic.
    /// </summary>
    private void InitializeRibbon()
    {
        try
        {
            var iconService = ServiceProvider.GetService(typeof(IThemeIconService)) as IThemeIconService;
            var (ribbon, homeTab) = RibbonFactory.CreateRibbon(this, _logger, iconService);
            _ribbon = ribbon;
            _homeTab = homeTab;
            Controls.Add(_ribbon);
            _logger?.LogInformation("Ribbon initialized via factory");
            _logger?.LogDebug("Ribbon size after init: {Width}x{Height}", _ribbon.Width, _ribbon.Height);
            Console.WriteLine($"[DIAGNOSTIC] Ribbon created: Size={_ribbon.Width}x{_ribbon.Height}, HomeTab={_homeTab?.Text}");
            if (_ribbon != null)
            {
                foreach (var tab in _ribbon.Header.MainItems)
                {
                    Console.WriteLine($"[DIAGNOSTIC] Ribbon tab: {((ToolStripTabItem)tab).Text}");
                }
            }

            // DEFENSIVE: Convert any animated images to static bitmaps to prevent ImageAnimator exceptions
            // Syncfusion WinForms ToolStrip controls do not support animated images
            if (_ribbon != null)
            {
                ValidateAndConvertRibbonImages(_ribbon);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Ribbon");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeRibbon failed: {ex.Message}");
            _ribbon = null;
        }
    }

    /// <summary>
    /// Validates all images in the ribbon and converts any animated images to static bitmaps.
    /// This prevents ImageAnimator exceptions when Syncfusion ToolStrip controls try to paint animated images.
    /// </summary>
    private void ValidateAndConvertRibbonImages(RibbonControlAdv ribbon)
    {
        try
        {
            int convertedCount = 0;

            // Iterate through all ribbon tabs
            foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
            {
                if (tab.Panel != null)
                {
                    // Iterate through all toolstrips in the tab panel
                    foreach (Control control in tab.Panel.Controls)
                    {
                        if (control is ToolStripEx toolStrip)
                        {
                            // Check each item in the toolstrip
                            foreach (ToolStripItem item in toolStrip.Items)
                            {
                                if (item.Image != null)
                                {
                                    // Check if image is animated or invalid/disposed
                                    bool needsConversion = false;
                                    try
                                    {
                                        needsConversion = ImageAnimator.CanAnimate(item.Image) || !IsImageValid(item.Image);
                                    }
                                    catch (Exception ex)
                                    {
                                        // Image is invalid/corrupted - needs conversion/removal
                                        _logger?.LogWarning(ex, "Image validation failed for ribbon item {ItemName} - treating as invalid", item.Name);
                                        needsConversion = true;
                                    }

                                    if (needsConversion)
                                    {
                                        // Convert animated/invalid image to static bitmap or remove it
                                        var staticBitmap = ConvertToStaticBitmap(item.Image);
                                        if (staticBitmap != null)
                                        {
                                            item.Image = staticBitmap;
                                            convertedCount++;
                                            _logger?.LogDebug("Converted/validated image for ribbon item: {ItemName}", item.Name);
                                        }
                                        else
                                        {
                                            // If conversion failed, remove the invalid image
                                            item.Image = null;
                                            _logger?.LogWarning("Removed invalid image from ribbon item: {ItemName}", item.Name);
                                        }
                                    }
                                }

                                // Check nested items in ToolStripPanelItem containers
                                if (item is ToolStripPanelItem panelItem)
                                {
                                    foreach (ToolStripItem panelSubItem in panelItem.Items)
                                    {
                                        if (panelSubItem.Image != null)
                                        {
                                            // Check if image is animated or invalid/disposed
                                            bool needsConversion = false;
                                            try
                                            {
                                                needsConversion = ImageAnimator.CanAnimate(panelSubItem.Image) || !IsImageValid(panelSubItem.Image);
                                            }
                                            catch (Exception ex)
                                            {
                                                // Image is invalid/corrupted - needs conversion/removal
                                                _logger?.LogWarning(ex, "Image validation failed for panel item {ItemName} - treating as invalid", panelSubItem.Name);
                                                needsConversion = true;
                                            }

                                            if (needsConversion)
                                            {
                                                var staticBitmap = ConvertToStaticBitmap(panelSubItem.Image);
                                                if (staticBitmap != null)
                                                {
                                                    panelSubItem.Image = staticBitmap;
                                                    convertedCount++;
                                                    _logger?.LogDebug("Converted/validated image for panel item: {ItemName}", panelSubItem.Name);
                                                }
                                                else
                                                {
                                                    // If conversion failed, remove the invalid image
                                                    panelSubItem.Image = null;
                                                    _logger?.LogWarning("Removed invalid image from panel item: {ItemName}", panelSubItem.Name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (convertedCount > 0)
            {
                _logger?.LogInformation("Converted {Count} animated images to static bitmaps in ribbon to prevent ImageAnimator exceptions", convertedCount);
                Console.WriteLine($"[DIAGNOSTIC] Converted {convertedCount} animated images to static bitmaps in ribbon");
            }
            else
            {
                _logger?.LogDebug("No animated images found in ribbon - all images are static");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate and convert ribbon images");
            Console.WriteLine($"[DIAGNOSTIC ERROR] ValidateAndConvertRibbonImages failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates all images in the menu bar and converts any invalid images to prevent ImageAnimator exceptions.
    /// This prevents ImageAnimator exceptions when ToolStrip controls try to paint invalid images.
    /// </summary>
    private void ValidateAndConvertMenuBarImages(MenuStrip menuStrip)
    {
        try
        {
            int convertedCount = 0;

            // Iterate through all menu items in the menu strip
            foreach (ToolStripItem menuItem in menuStrip.Items)
            {
                if (menuItem is ToolStripMenuItem toolStripMenuItem)
                {
                    // Check the main menu item
                    if (menuItem.Image != null)
                    {
                        bool needsConversion = false;
                        try
                        {
                            needsConversion = ImageAnimator.CanAnimate(menuItem.Image) || !IsImageValid(menuItem.Image);
                        }
                        catch (Exception ex)
                        {
                            // Image is invalid/corrupted - needs conversion/removal
                            _logger?.LogWarning(ex, "Image validation failed for menu item {ItemName} - treating as invalid", menuItem.Name);
                            needsConversion = true;
                        }

                        if (needsConversion)
                        {
                            var staticBitmap = ConvertToStaticBitmap(menuItem.Image);
                            if (staticBitmap != null)
                            {
                                menuItem.Image = staticBitmap;
                                convertedCount++;
                                _logger?.LogDebug("Converted/validated image for menu item: {ItemName}", menuItem.Name);
                            }
                            else
                            {
                                // If conversion failed, remove the invalid image
                                menuItem.Image = null;
                                _logger?.LogWarning("Removed invalid image from menu item: {ItemName}", menuItem.Name);
                            }
                        }
                    }

                    // Recursively check all dropdown items
                    ValidateMenuItemImages(toolStripMenuItem.DropDownItems, ref convertedCount);
                }
            }

            if (convertedCount > 0)
            {
                _logger?.LogInformation("Converted/validated {Count} images in menu bar to prevent ImageAnimator exceptions", convertedCount);
                Console.WriteLine($"[DIAGNOSTIC] Converted/validated {convertedCount} images in menu bar");
            }
            else
            {
                _logger?.LogDebug("No invalid images found in menu bar - all images are valid");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate and convert menu bar images");
            Console.WriteLine($"[DIAGNOSTIC ERROR] ValidateAndConvertMenuBarImages failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively validates images in menu item dropdown collections.
    /// </summary>
    private void ValidateMenuItemImages(ToolStripItemCollection items, ref int convertedCount)
    {
        foreach (ToolStripItem item in items)
        {
            if (item.Image != null)
            {
                bool needsConversion = false;
                try
                {
                    needsConversion = ImageAnimator.CanAnimate(item.Image) || !IsImageValid(item.Image);
                }
                catch (Exception ex)
                {
                    // Image is invalid/corrupted - needs conversion/removal
                    _logger?.LogWarning(ex, "Image validation failed for dropdown item {ItemName} - treating as invalid", item.Name);
                    needsConversion = true;
                }

                if (needsConversion)
                {
                    var staticBitmap = ConvertToStaticBitmap(item.Image);
                    if (staticBitmap != null)
                    {
                        item.Image = staticBitmap;
                        convertedCount++;
                        _logger?.LogDebug("Converted/validated image for dropdown item: {ItemName}", item.Name);
                    }
                    else
                    {
                        // If conversion failed, remove the invalid image
                        item.Image = null;
                        _logger?.LogWarning("Removed invalid image from dropdown item: {ItemName}", item.Name);
                    }
                }
            }

            // Recursively check nested dropdown items
            if (item is ToolStripMenuItem subMenuItem)
            {
                ValidateMenuItemImages(subMenuItem.DropDownItems, ref convertedCount);
            }
        }
    }

    /// <summary>
    /// Checks if an image is valid and not disposed.
    /// </summary>
    private static bool IsImageValid(Image image)
    {
        if (image == null)
        {
            return false;
        }

        try
        {
            // Try to access image properties to check if it's valid
            _ = image.Width;
            _ = image.Height;
            _ = image.PixelFormat;

            // CRITICAL: Safely check if image can be animated without crashing
            // This can throw "Parameter is not valid" if image is disposed/corrupted
            try
            {
                _ = ImageAnimator.CanAnimate(image);
            }
            catch (ArgumentException)
            {
                // Image is corrupted and will crash ImageAnimator
                return false;
            }

            return true;
        }
        catch
        {
            // Image is disposed or corrupted
            return false;
        }
    }

    /// <summary>
    /// Converts an animated image to a static bitmap by drawing the first frame.
    /// </summary>
    private static Image? ConvertToStaticBitmap(Image animatedImage)
    {
        if (animatedImage == null || !ImageAnimator.CanAnimate(animatedImage))
        {
            return animatedImage;
        }

        try
        {
            // Create a new bitmap with the same dimensions
            var staticBitmap = new Bitmap(animatedImage.Width, animatedImage.Height);

            // Draw the animated image onto the static bitmap (this captures the current/first frame)
            using (var g = Graphics.FromImage(staticBitmap))
            {
                g.DrawImage(animatedImage, 0, 0, animatedImage.Width, animatedImage.Height);
            }

            return staticBitmap;
        }
        catch (Exception ex)
        {
            // If conversion fails, dispose the animated image and return null to prevent ImageAnimator exceptions
            System.Diagnostics.Debug.WriteLine($"Failed to convert animated image to static bitmap: {ex.Message}");
            try
            {
                animatedImage.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            return null;
        }
    }

    /// <summary>
    /// Performs a late validation pass on all menu bar images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateMenuBarImages()
    {
        if (_menuStrip == null)
        {
            return;
        }

        try
        {
            int invalidCount = 0;
            int totalChecked = 0;

            foreach (ToolStripItem topLevelItem in _menuStrip.Items)
            {
                if (topLevelItem is ToolStripMenuItem topLevelMenu)
                {
                    // Check top-level menu item image
                    if (topLevelMenu.Image != null)
                    {
                        totalChecked++;
                        if (!IsImageValid(topLevelMenu.Image))
                        {
                            _logger?.LogWarning("Late validation: Removing invalid image from top-level menu item: {ItemName}", topLevelMenu.Name);
                            topLevelMenu.Image = null;
                            invalidCount++;
                        }
                    }

                    // Check all sub-menu items
                    foreach (ToolStripItem subItem in topLevelMenu.DropDownItems)
                    {
                        if (subItem.Image != null)
                        {
                            totalChecked++;
                            if (!IsImageValid(subItem.Image))
                            {
                                _logger?.LogWarning("Late validation: Removing invalid image from menu item: {ItemName}", subItem.Name);
                                subItem.Image = null;
                                invalidCount++;
                            }
                        }
                    }
                }
            }

            if (invalidCount > 0)
            {
                _logger?.LogWarning("Late validation: Removed {InvalidCount} invalid images from menu bar (checked {TotalCount} images)", invalidCount, totalChecked);
                _menuStrip.Refresh(); // Force repaint with valid images only
            }
            else
            {
                _logger?.LogDebug("Late validation: All {TotalCount} menu bar images are valid", totalChecked);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Late validation: Failed to validate menu bar images");
        }
    }

    /// <summary>
    /// Performs a late validation pass on all ribbon images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateRibbonImages()
    {
        if (_ribbon == null)
        {
            return;
        }

        try
        {
            int invalidCount = 0;
            int totalChecked = 0;

            foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
            {
                if (tab.Panel != null)
                {
                    foreach (Control control in tab.Panel.Controls)
                    {
                        if (control is ToolStripEx toolStrip)
                        {
                            foreach (ToolStripItem item in toolStrip.Items)
                            {
                                if (item.Image != null)
                                {
                                    totalChecked++;
                                    if (!IsImageValid(item.Image))
                                    {
                                        _logger?.LogWarning("Late validation: Removing invalid image from ribbon item: {ItemName}", item.Name);
                                        item.Image = null;
                                        invalidCount++;
                                    }
                                }

                                // Check nested items in panels
                                if (item is ToolStripPanelItem panelItem)
                                {
                                    foreach (ToolStripItem panelSubItem in panelItem.Items)
                                    {
                                        if (panelSubItem.Image != null)
                                        {
                                            totalChecked++;
                                            if (!IsImageValid(panelSubItem.Image))
                                            {
                                                _logger?.LogWarning("Late validation: Removing invalid image from ribbon panel item: {ItemName}", panelSubItem.Name);
                                                panelSubItem.Image = null;
                                                invalidCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (invalidCount > 0)
            {
                _logger?.LogWarning("Late validation: Removed {InvalidCount} invalid images from ribbon (checked {TotalCount} images)", invalidCount, totalChecked);
                _ribbon.Refresh(); // Force repaint with valid images only
            }
            else
            {
                _logger?.LogDebug("Late validation: All {TotalCount} ribbon images are valid", totalChecked);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Late validation: Failed to validate ribbon images");
        }
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
            _statusBar = StatusBarFactory.CreateStatusBar(this, _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeStatusBar failed: {ex.Message}");
            _statusBar = null;
            return;
        }

        // _statusBar is guaranteed to be non-null here
        Controls.Add(_statusBar);
        _logger?.LogInformation("Status bar initialized via factory with {PanelCount} panels", _statusBar.Controls.Count);
        _logger?.LogDebug("Status bar size after init: {Width}x{Height}, HasSizingGrip={HasGrip}",
            _statusBar.Width, _statusBar.Height, _statusBar.SizingGrip);
        Console.WriteLine($"[DIAGNOSTIC] Status bar created: Size={_statusBar.Width}x{_statusBar.Height}, Panels={_statusBar.Controls.Count}");
    }

    /// <summary>
    /// Initialize simple navigation strip for test harness mode.
    /// </summary>
    /// <summary>
    /// Initialize the navigation <see cref="ToolStripEx"/> with named <see cref="ToolStripButton"/>
    /// controls used for quick navigation. Buttons are assigned deterministic <see cref="Control.Name"/>
    /// and <see cref="Control.AccessibleName"/> values for automation and testing.
    /// All buttons include AccessibleDescription for screen reader support.
    /// ToolStripEx automatically handles button sizing and alignment via layout.
    /// </summary>
    private void InitializeNavigationStrip()
    {
        try
        {
            _navigationStrip = new ToolStripEx
            {
                Name = "NavigationStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                AccessibleName = "Navigation Strip",
                AccessibleDescription = "Main navigation toolbar for switching between application panels",
                AutoSize = true // ToolStripEx handles height automatically
            };

            var dashboardBtn = new ToolStripButton("Dashboard")
            {
                Name = "Nav_Dashboard",
                AccessibleName = "Dashboard",
                AccessibleDescription = "Navigate to Dashboard panel with summary metrics and activity",
                Enabled = false
            };
            dashboardBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Dashboard clicked");
            };

            var accountsBtn = new ToolStripButton("Accounts")
            {
                Name = "Nav_Accounts",
                AccessibleName = "Accounts",
                AccessibleDescription = "Navigate to Municipal Accounts panel with account management",
                Enabled = false
            };
            accountsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Accounts clicked");
            };

            var budgetBtn = new ToolStripButton("Budget")
            {
                Name = "Nav_Budget",
                AccessibleName = "Budget",
                AccessibleDescription = "Navigate to Budget Overview panel with budget allocation and tracking",
                Enabled = false
            };
            budgetBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Budget clicked");
            };

            var chartsBtn = new ToolStripButton("Charts")
            {
                Name = "Nav_Charts",
                AccessibleName = "Charts",
                AccessibleDescription = "Navigate to Budget Analytics panel with charts and visualizations",
                Enabled = false
            };
            chartsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ChartPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Charts clicked");
            };

            var analyticsBtn = new ToolStripButton("&Analytics")
            {
                Name = "Nav_Analytics",
                AccessibleName = "Analytics",
                AccessibleDescription = "Navigate to Analytics and Insights panel with advanced analytics"
            };
            analyticsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Analytics clicked");
            };

            var auditLogBtn = new ToolStripButton("&Audit Log")
            {
                Name = "Nav_AuditLog",
                AccessibleName = "Audit Log",
                AccessibleDescription = "Navigate to Audit Log panel with activity history and compliance tracking"
            };
            auditLogBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_AuditLog clicked");
            };

            var customersBtn = new ToolStripButton("Customers")
            {
                Name = "Nav_Customers",
                AccessibleName = "Customers",
                AccessibleDescription = "Navigate to Customers panel with customer/account information"
            };
            customersBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Customers clicked");
            };

            var reportsBtn = new ToolStripButton("Reports")
            {
                Name = "Nav_Reports",
                AccessibleName = "Reports",
                AccessibleDescription = "Navigate to Reports panel with generated reports and exports"
            };
            reportsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Reports clicked");
            };

            var aiChatBtn = new ToolStripButton("AI Chat")
            {
                Name = "Nav_AIChat",
                AccessibleName = "AI Chat",
                AccessibleDescription = "Navigate to AI Chat panel with intelligent assistant"
            };
            aiChatBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ChatPanel>("AI Chat", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_AIChat clicked");
            };

            var quickBooksBtn = new ToolStripButton("QuickBooks")
            {
                Name = "Nav_QuickBooks",
                AccessibleName = "QuickBooks",
                AccessibleDescription = "Navigate to QuickBooks Integration panel for accounting operations"
            };
            quickBooksBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_QuickBooks clicked");
            };

            var settingsBtn = new ToolStripButton("Settings")
            {
                Name = "Nav_Settings",
                AccessibleName = "Settings",
                AccessibleDescription = "Navigate to Settings panel to configure application preferences"
            };
            settingsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Settings clicked");
            };

            // Theme toggle (session-only)
            var themeToggleBtn = new ToolStripButton
            {
                Name = "ThemeToggle",
                AccessibleName = "Theme Toggle",
                AccessibleDescription = "Toggle between light and dark themes for current session",
                AutoSize = true
            };
            themeToggleBtn.Click += ThemeToggleBtn_Click;
            themeToggleBtn.Text = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";

            // Grid test helpers (navigation strip)
            var navGridApplyFilter = new ToolStripButton("Apply Grid Filter")
            {
                Name = "Nav_ApplyGridFilter",
                AccessibleName = "Apply Grid Filter",
                AccessibleDescription = "Apply filter to active grid control"
            };
            navGridApplyFilter.Click += (s, e) => ApplyTestFilterToActiveGrid();

            var navGridClearFilter = new ToolStripButton("Clear Grid Filter")
            {
                Name = "Nav_ClearGridFilter",
                AccessibleName = "Clear Grid Filter",
                AccessibleDescription = "Clear all filters from active grid control"
            };
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid")
            {
                Name = "Nav_ExportGrid",
                AccessibleName = "Export Grid",
                AccessibleDescription = "Export active grid data to Excel spreadsheet"
            };
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                budgetBtn,
                chartsBtn,
                analyticsBtn,
                auditLogBtn,
                customersBtn,
                reportsBtn,
                aiChatBtn,
                quickBooksBtn,
                new ToolStripSeparator(),
                settingsBtn,
                new ToolStripSeparator(),
                themeToggleBtn,
                new ToolStripSeparator(),
                navGridApplyFilter,
                navGridClearFilter,
                navGridExport
            });

            Controls.Add(_navigationStrip);
            _logger?.LogInformation("Navigation strip initialized with {ButtonCount} buttons", _navigationStrip.Items.Count);
            _logger?.LogDebug("Navigation strip size after init: {Width}x{Height}", _navigationStrip.Width, _navigationStrip.Height);
            Console.WriteLine($"[DIAGNOSTIC] Navigation strip created: Size={_navigationStrip.Width}x{_navigationStrip.Height}, AutoSize={_navigationStrip.AutoSize}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize navigation strip");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeNavigationStrip failed: {ex.Message}");
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
    /// Initialize full-screen loading overlay for async operations.
    /// Provides visual feedback during long-running tasks like initialization or data loading.
    /// Overlay is positioned full-screen (DockStyle.Fill) and label is dynamically centered.
    /// </summary>
    private void InitializeLoadingOverlay()
    {
        try
        {
            _loadingOverlay = new Panel
            {
                Name = "LoadingOverlay",
                Dock = DockStyle.Fill,
                BackColor = GetLoadingOverlayColor(), // Semantic semi-transparent overlay color
                Visible = false,
                AccessibleName = "Loading Overlay",
                AccessibleDescription = "Displays during long-running async operations"
            };

            _loadingLabel = new Label
            {
                Name = "LoadingLabel",
                Text = MainFormResources.LoadingText,
                ForeColor = GetLoadingLabelColor(), // Semantic white text color
                Font = new Font(SegoeUiFontName, 12F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                AccessibleName = "Loading Message",
                AccessibleDescription = "Current loading status message"
            };

            // Add label to overlay
            _loadingOverlay.Controls.Add(_loadingLabel);

            // Center label after it's added to container and size is known
            // Use a deferred action to ensure label measurements are available
            _loadingOverlay.Layout += (s, e) =>
            {
                if (_loadingLabel != null && !_loadingLabel.IsDisposed)
                {
                    // Center label both horizontally and vertically on first layout
                    _loadingLabel.Location = new Point(
                        Math.Max(0, (_loadingOverlay.Width - _loadingLabel.Width) / 2),
                        Math.Max(0, (_loadingOverlay.Height - _loadingLabel.Height) / 2));
                }
            };

            // Ensure form is added before label can be positioned correctly
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            _logger?.LogDebug("Loading overlay initialized - centered label with dynamic repositioning on resize");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize loading overlay");
        }
    }

    /// <summary>
    /// Show the loading overlay with optional message.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="message">Optional loading message to display.</param>
    public void ShowLoadingOverlay(string? message = null)
    {
        if (_loadingOverlay == null) return;

        try
        {
            if (InvokeRequired)
            {
                Invoke(() => ShowLoadingOverlay(message));
                return;
            }

            if (_loadingLabel != null)
            {
                _loadingLabel.Text = message ?? MainFormResources.LoadingText;
            }

            _loadingOverlay.Visible = true;
            _loadingOverlay.BringToFront();
            Application.DoEvents(); // Allow UI to update
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show loading overlay");
        }
    }

    /// <summary>
    /// Hide the loading overlay.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    public void HideLoadingOverlay()
    {
        if (_loadingOverlay == null) return;

        try
        {
            if (InvokeRequired)
            {
                Invoke(HideLoadingOverlay);
                return;
            }

            _loadingOverlay.Visible = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to hide loading overlay");
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

            // File > Recent Files (MRU) - submenu
            var recentFilesMenu = new ToolStripMenuItem("&Recent Files")
            {
                Name = "Menu_File_RecentFiles",
                ToolTipText = "Recently opened files"
            };
            UpdateMruMenu(recentFilesMenu);

            // File > Clear Recent Files
            var clearRecentMenuItem = new ToolStripMenuItem("&Clear Recent Files", null, (s, e) => ClearMruList())
            {
                Name = "Menu_File_ClearRecent",
                ToolTipText = "Clear recent files list"
            };

            // File > Exit
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close())
            {
                Name = "Menu_File_Exit",
                ShortcutKeys = Keys.Alt | Keys.F4,
                ToolTipText = "Exit the application (Alt+F4)"
            };
            exitMenuItem.Image = CreateIconFromText("\uE8BB", 16); // Exit icon (Segoe MDL2)
            exitMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            fileMenu.DropDownItems.Add(recentFilesMenu);
            fileMenu.DropDownItems.Add(clearRecentMenuItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitMenuItem);

            // View Menu - All child forms accessible here
            var viewMenu = new ToolStripMenuItem("&View")
            {
                Name = "Menu_View",
                ToolTipText = "Open application views"
            };

            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Dashboard", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Dashboard' failed");
                    MessageBox.Show(this, $"Failed to open Dashboard: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Dashboard",
                ShortcutKeys = Keys.Control | Keys.D,
                ToolTipText = "Open Dashboard view (Ctrl+D)"
            };
            dashboardMenuItem.Image = CreateIconFromText("\uE10F", 16); // Dashboard icon (Segoe MDL2)
            dashboardMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Accounts' failed");
                    MessageBox.Show(this, $"Failed to open Accounts: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Accounts",
                ShortcutKeys = Keys.Control | Keys.A,
                ToolTipText = "Open Accounts view (Ctrl+A)"
            };
            accountsMenuItem.Image = CreateIconFromText("\uE8F4", 16); // AccountActivity icon (Segoe MDL2)
            accountsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Budget Overview
            var budgetMenuItem = new ToolStripMenuItem("&Budget Overview", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Budget Overview' failed");
                    MessageBox.Show(this, $"Failed to open Budget Overview: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Budget",
                ShortcutKeys = Keys.Control | Keys.B,
                ToolTipText = "Open Budget Overview (Ctrl+B)"
            };
            budgetMenuItem.Image = CreateIconFromText("\uE7C8", 16); // Money icon (Segoe MDL2)
            budgetMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Charts", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<ChartPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Charts' failed");
                    MessageBox.Show(this, $"Failed to open Charts: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Charts",
                ShortcutKeys = Keys.Control | Keys.H,
                ToolTipText = "Open Charts view (Ctrl+H)"
            };
            chartsMenuItem.Image = CreateIconFromText("\uE9D2", 16); // BarChart icon (Segoe MDL2)
            chartsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Reports
            var reportsMenuItem = new ToolStripMenuItem("&Reports", null, (s, e) =>
            {
                try
                {
                    _panelNavigator?.ShowPanel<ReportsPanel>("Reports", DockingStyle.Fill, allowFloating: true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "MainForm: Failed to open Reports panel from menu");
                    _logger?.LogError(ex, "Menu action 'Reports' failed");
                    MessageBox.Show(this, $"Failed to open Reports: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Reports",
                ShortcutKeys = Keys.Control | Keys.R,
                ToolTipText = "Open Reports view (Ctrl+R)"
            };
            reportsMenuItem.Image = CreateIconFromText("\uE8A5", 16); // Document icon (Segoe MDL2)
            reportsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > AI Chat
            var aiChatMenuItem = new ToolStripMenuItem("AI &Chat", null, (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ChatPanel>("AI Chat", DockingStyle.Right, allowFloating: true);
            })
            {
                Name = "Menu_View_AIChat",
                ShortcutKeys = Keys.Control | Keys.I,
                ToolTipText = "Open AI Chat Assistant (Ctrl+I)"
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    var currentTheme = GetAppThemeFromString(GetCurrentTheme());
                    aiChatMenuItem.Image = iconService.GetIcon("chat", currentTheme, 16);
                    aiChatMenuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
            catch { /* Icon loading is optional */ }

            // View > QuickBooks
            var quickBooksMenuItem = new ToolStripMenuItem("&QuickBooks", null, (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);
            })
            {
                Name = "Menu_View_QuickBooks",
                ShortcutKeys = Keys.Control | Keys.Q,
                ToolTipText = "Open QuickBooks Integration (Ctrl+Q)"
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    var currentTheme = GetAppThemeFromString(GetCurrentTheme());
                    quickBooksMenuItem.Image = iconService.GetIcon("quickbooks", currentTheme, 16);
                    quickBooksMenuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
            catch { /* Icon loading is optional */ }

            // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);
            })
            {
                Name = "Menu_View_Customers",
                ShortcutKeys = Keys.Control | Keys.U,
                ToolTipText = "Open Customers view (Ctrl+U)"
            };
            customersMenuItem.Image = CreateIconFromText("\uE716", 16); // Contact icon (Segoe MDL2)
            customersMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // Add separator for visual grouping
            var viewSeparator = new ToolStripSeparator
            {
                Name = "Menu_View_Separator"
            };

            // View > Refresh
            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) =>
            {
                // Refresh all open panels via PanelNavigationService
                this.Refresh();
            })
            {
                Name = "Menu_View_Refresh",
                ShortcutKeys = Keys.F5,
                ToolTipText = "Refresh active view (F5)"
            };
            refreshMenuItem.Image = CreateIconFromText("\uE72C", 16); // Refresh icon (Segoe MDL2)
            refreshMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            viewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                dashboardMenuItem,
                accountsMenuItem,
                budgetMenuItem,
                chartsMenuItem,
                reportsMenuItem,
                aiChatMenuItem,
                quickBooksMenuItem,
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
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Settings' failed");
                    MessageBox.Show(this, $"Failed to open Settings: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_Tools_Settings",
                ShortcutKeys = Keys.Control | Keys.Oemcomma,
                ToolTipText = "Open Settings (Ctrl+,)",
                ImageScaling = ToolStripItemImageScaling.None
            };
            settingsMenuItem.Image = CreateIconFromText("\uE713", 16); // Settings icon (Segoe MDL2)

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
                ToolTipText = "Open online documentation (F1)"
            };
            documentationMenuItem.Image = CreateIconFromText("\uE897", 16); // Help icon (Segoe MDL2)
            documentationMenuItem.ImageScaling = ToolStripItemImageScaling.None;

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
                ToolTipText = "About this application"
            };
            aboutMenuItem.Image = CreateIconFromText("\uE946", 16); // Info icon (Segoe MDL2)
            aboutMenuItem.ImageScaling = ToolStripItemImageScaling.None;

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

            // DEFENSIVE: Validate and convert any invalid images in the menu bar
            // This prevents ImageAnimator exceptions when ToolStrip controls try to paint invalid images
            ValidateAndConvertMenuBarImages(_menuStrip);

            _logger?.LogInformation("Menu bar initialized with icons and theming");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    /// <summary>
    /// Create a Bitmap icon from Segoe MDL2 Assets text (Unicode character).
    /// Icons use DodgerBlue as a semantic accent color for visual UI emphasis in menus/ribbons.
    /// SEMANTIC COLOR JUSTIFICATION: DodgerBlue provides visual accent for icon emphasis in UI chrome,
    /// independent of theme. All UI chrome (ribbons, menus) should have consistent accent color.
    /// </summary>
    /// <param name="iconText">Unicode character from Segoe MDL2 Assets font</param>
    /// <param name="size">Icon size in pixels</param>
    /// <returns>Bitmap containing the rendered icon, or null if creation fails</returns>
    private Bitmap? CreateIconFromText(string iconText, int size)
    {
        if (string.IsNullOrWhiteSpace(iconText) || size <= 0)
        {
            _logger?.LogWarning("CreateIconFromText: Invalid parameters - iconText='{IconText}', size={Size}",
                iconText ?? "(null)", size);
            return null;
        }

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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create icon from text '{IconText}' with size {Size}", iconText, size);
            return null;
        }
    }

    /// <summary>
    /// Gets the semantic overlay color for the loading overlay background.
    /// Returns a semi-transparent dark overlay to dim the background during loading.
    /// Uses system colors with theme awareness where available.
    /// Modal overlay dimming is a semantic UI behavior pattern and exception to theme-only rule.
    /// </summary>
    /// <returns>Semi-transparent overlay color (128 alpha) for semantic modal dimming effect</returns>
    private Color GetLoadingOverlayColor()
    {
        try
        {
            // SEMANTIC COLOR: Semi-transparent overlay for modal dimming effect
            // Use system ControlDarkDark as base (theme-aware on modern Windows)
            var overlayBaseColor = SystemColors.ControlDarkDark;
            // Apply 50% opacity for modal dimming
            return Color.FromArgb(128, overlayBaseColor.R, overlayBaseColor.G, overlayBaseColor.B);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get overlay color - using fallback");
            // Fallback: semi-transparent black
            return Color.FromArgb(128, 0, 0, 0);
        }
    }

    /// <summary>
    /// Gets the semantic text color for the loading label.
    /// Returns high-contrast text color for accessibility on the modal overlay.
    /// Uses system colors for theme-aware text rendering.
    /// Text contrast is a semantic UI behavior pattern and exception to theme-only rule.
    /// </summary>
    /// <returns>High-contrast text color for semantic loading indicator visibility</returns>
    private Color GetLoadingLabelColor()
    {
        try
        {
            // SEMANTIC COLOR: High-contrast text on modal overlay
            // Use WindowText for theme-aware text color (respects light/dark mode on Windows)
            var windowTextColor = SystemColors.WindowText;
            // Ensure we have adequate contrast (invert to white if dark overlay is too dark)
            return windowTextColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get text color - using fallback");
            // Fallback: white for contrast against dark overlay
            return Color.White;
        }
    }

    /// <summary>
    /// Get the current theme name from SkinManager.
    /// </summary>
    /// <returns>Current theme name</returns>
    private string GetCurrentTheme()
    {
        return SkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
    }

    /// <summary>
    /// Convert string theme name to AppTheme enum.
    /// </summary>
    /// <param name="themeName">Theme name from SkinManager</param>
    /// <returns>Corresponding AppTheme enum value</returns>
    private AppTheme GetAppThemeFromString(string themeName)
    {
        return themeName switch
        {
            "Office2019Colorful" => Theming.AppTheme.Office2019Colorful,
            "Office2019Dark" => Theming.AppTheme.Office2019Dark,
            "Office2019Black" => Theming.AppTheme.Office2019Black,
            "Office2019DarkGray" => Theming.AppTheme.Office2019DarkGray,
            "Office2019White" => Theming.AppTheme.Office2019White,
            "HighContrastBlack" => Theming.AppTheme.HighContrastBlack,
            _ => Theming.AppTheme.Office2019Colorful // Default fallback
        };
    }

    /// <summary>
    /// Apply theme colors to menu dropdown items.
    /// </summary>
    /// <param name="menuItem">Parent menu item to theme</param>
    /// <summary>
    /// Apply Syncfusion visual styling to a menu dropdown and recursively to its child items.
    /// Ensures the dropdown receives the current <see cref="Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme"/>.
    /// </summary>
    /// <param name="menuItem">Parent menu item whose dropdown should be themed.</param>
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

            // Explicitly ensure the dropdown inherits the current Syncfusion visual style
            try
            {
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(dropdown, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
            }
            catch (Exception setEx)
            {
                _logger?.LogDebug(setEx, "Failed to set visual style on menu dropdown {MenuName}", menuItem.Name);
            }

            // Apply theme to child items (structure only; visual style applied at dropdown level)
            foreach (ToolStripItem item in dropdown.Items)
            {
                if (item is ToolStripMenuItem childMenuItem)
                {
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
    /// Handle theme toggle - switches between Dark and Light themes (session-only, no config persistence).
    /// Can be invoked via keyboard shortcut or programmatically.
    /// </summary>
    /// <summary>
    /// Handles user-initiated theme toggling for the current session.
    /// Switches between <c>Office2019Colorful</c> and <c>Office2019Dark</c> and reapplies
    /// the visual style to open forms. This preference is session-only and not persisted.
    /// </summary>
    /// <param name="sender">The event sender (typically a ToolStripButton).</param>
    /// <param name="e">Event arguments.</param>
    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            var currentTheme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme;
            var newTheme = currentTheme == "Office2019Dark" ? "Office2019Colorful" : "Office2019Dark";
            var isLightMode = newTheme == "Office2019Colorful";

            // Apply new theme globally via SfSkinManager
            Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = newTheme;

            // Update button text
            if (sender is ToolStripButton btn)
            {
                btn.Text = isLightMode ? "🌙 Dark Mode" : "☀️ Light Mode";
            }

            _logger?.LogInformation("Theme switched to {NewTheme} (session only - no config persistence)", newTheme);

            // Ensure open forms explicitly refresh their visual style
            foreach (Form form in Application.OpenForms)
            {
                try
                {
                    // Also explicitly set visual style on the form to avoid renderer race conditions
                    try
                    {
                        Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(form, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
                    }
                    catch (Exception innerEx)
                    {
                        _logger?.LogDebug(innerEx, "Failed to SetVisualStyle on form {FormName}", form.Name);
                    }

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
    /// Performs a global search across all visible docked panels containing SfDataGrid controls.
    /// Searches through DataSource properties via reflection and displays aggregated results.
    /// </summary>
    /// <param name="searchText">The text to search for (case-insensitive)</param>
    private void PerformGlobalSearch(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            MessageBox.Show("Please enter a search term.", "Global Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _logger?.LogInformation("Performing global search for: {SearchText}", searchText);
            var results = new System.Text.StringBuilder();
            var totalMatches = 0;

            // DockingManager hosts panels as Controls inside MainForm, so scan the MainForm control tree.
            var grids = FindControlsOfType<Syncfusion.WinForms.DataGrid.SfDataGrid>(this);

            foreach (var grid in grids)
            {
                if (grid.DataSource == null)
                    continue;

                try
                {
                    var gridMatches = SearchGridData(grid, searchText);
                    if (gridMatches <= 0)
                        continue;

                    var containerName = grid.Parent?.Name ?? "(unknown)";
                    results.AppendLine(CultureInfo.InvariantCulture, $"{containerName} - {grid.Name}: {gridMatches} match(es)");
                    totalMatches += gridMatches;
                }
                catch (Exception gridEx)
                {
                    _logger?.LogWarning(gridEx, "Failed to search grid {GridName}", grid.Name);
                }
            }

            // Display results
            if (totalMatches > 0)
            {
                var message = $"Found {totalMatches} match(es) for '{searchText}':\n\n{results}";
                MessageBox.Show(message, "Global Search Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _logger?.LogInformation("Global search completed: {TotalMatches} match(es) found", totalMatches);
            }
            else
            {
                MessageBox.Show($"No matches found for '{searchText}'.", "Global Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _logger?.LogInformation("Global search completed: No matches found");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform global search");
            MessageBox.Show("An error occurred while performing the search. Please check the logs for details.",
                "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Recursively finds all controls of a specific type within a parent control.
    /// </summary>
    /// <typeparam name="T">Type of control to find</typeparam>
    /// <param name="parent">Parent control to search</param>
    /// <returns>Collection of matching controls</returns>
    private IEnumerable<T> FindControlsOfType<T>(Control parent) where T : Control
    {
        var results = new List<T>();

        if (parent == null)
            return results;

        foreach (Control control in parent.Controls)
        {
            if (control is T matchingControl)
            {
                results.Add(matchingControl);
            }

            // Recursively search child controls
            results.AddRange(FindControlsOfType<T>(control));
        }

        return results;
    }

    /// <summary>
    /// Searches a SfDataGrid's DataSource for matching text via reflection.
    /// </summary>
    /// <param name="grid">Grid to search</param>
    /// <param name="searchText">Text to search for (case-insensitive)</param>
    /// <returns>Number of matches found</returns>
    private int SearchGridData(Syncfusion.WinForms.DataGrid.SfDataGrid grid, string searchText)
    {
        if (grid?.DataSource == null)
            return 0;

        var matches = 0;
        var dataSource = grid.DataSource;

        try
        {
            // Handle IEnumerable DataSource (most common)
            if (dataSource is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null)
                        continue;

                    // Use reflection to get all public properties
                    var properties = item.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    foreach (var property in properties)
                    {
                        try
                        {
                            var value = property.GetValue(item);
                            if (value == null)
                                continue;

                            // Convert to string and perform case-insensitive search
                            var stringValue = value.ToString();
                            if (!string.IsNullOrEmpty(stringValue) &&
                                stringValue.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            {
                                matches++;
                                break; // Count each row only once
                            }
                        }
                        catch (Exception propEx)
                        {
                            _logger?.LogDebug(propEx, "Failed to read property {PropertyName} during search", property.Name);
                            // Continue with next property
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to search grid data for grid {GridName}", grid.Name);
        }

        return matches;
    }

    #endregion

    #region Docking

    /// <summary>
    /// Initializes Syncfusion DockingManager with layout management.
    /// Delegates to DockingHostFactory for centralized docking creation logic.
    /// Loads saved layout from AppData if available.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        try
        {
            var dockingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger?.LogInformation("InitializeSyncfusionDocking start - handleCreated={HandleCreated}", IsHandleCreated);
            Console.WriteLine($"[DIAGNOSTIC] InitializeSyncfusionDocking: started, handleCreated={IsHandleCreated}");

            var (dockingManager, leftPanel, rightPanel, activityGrid, activityTimer) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);

            _dockingManager = dockingManager;
            _activityGrid = activityGrid;
            _activityRefreshTimer = activityTimer;

            Console.WriteLine($"[DIAGNOSTIC] DockingManager created: HostControl={_dockingManager?.HostControl?.Name}");
            Console.WriteLine($"[DIAGNOSTIC] LeftPanel: {leftPanel?.Name}, RightPanel: {rightPanel?.Name} (no central panel - pure docking)");

            // Ensure panel navigation is available before layout load so dynamic panels recreate with real controls
            EnsurePanelNavigatorInitialized();

            // Create and attach layout manager for state management
            _dockingLayoutManager = new DockingLayoutManager(_serviceProvider, _panelNavigator, _logger);

            // Transfer ownership of panels and fonts to the layout manager
            var dockAutoHideTabFont = new Font(SegoeUiFontName, 9F);
            var dockTabFont = new Font(SegoeUiFontName, 9F);
            _dockingLayoutManager.SetManagedResources(leftPanel, rightPanel, dockAutoHideTabFont, dockTabFont);

            _dockingLayoutManager.AttachTo(_dockingManager);

            HideStandardPanelsForDocking();

            // CRITICAL FIX: Load layout on the UI thread to prevent ArgumentOutOfRangeException in DockHost.GetPaintInfo
            // The exception occurs when paint events fire before DockingManager's internal control collections are populated
            // LoadLayoutAsync must complete (or fail fast) before the form is shown and painted
            try
            {
                _dockingLayoutManager.LoadLayoutAsync(_dockingManager, this, GetDockingLayoutPath()).GetAwaiter().GetResult();
                _logger?.LogDebug("Docking layout loaded successfully (synchronous wait)");
                Console.WriteLine("[DIAGNOSTIC] Docking layout loaded synchronously - panels ready for paint");
            }
            catch (Exception layoutEx)
            {
                _logger?.LogWarning(layoutEx, "Failed to load docking layout from {LayoutPath} - using default programmatic docking", GetDockingLayoutPath());
                Console.WriteLine($"[DIAGNOSTIC] Layout load failed: {layoutEx.Message} - default docking will be used");
                // Layout load failure is non-critical - docking will use default layout from DockingHostFactory
            }

            _dockingLayoutManager.ApplyThemeToDockingPanels(_dockingManager, leftPanel, rightPanel);

            // CRITICAL: Apply SkinManager theme AFTER DockingManager is fully initialized and panels are docked
            // This ensures theme cascade works correctly and prevents ArgumentOutOfRangeException in paint events
            try
            {
                var themeName = SkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
                SfSkinManager.SetVisualStyle(this, themeName);
                _logger?.LogInformation("Applied SfSkinManager theme to MainForm after DockingManager setup: {Theme}", themeName);
                Console.WriteLine($"[DIAGNOSTIC] Applied SfSkinManager theme to MainForm: {themeName}");
            }
            catch (Exception themeEx)
            {
                _logger?.LogWarning(themeEx, "Failed to apply SkinManager theme to MainForm after DockingManager setup");
            }

            dockingStopwatch.Stop();
            _logger?.LogInformation(
                "InitializeSyncfusionDocking complete in {ElapsedMs}ms - ActivityTimerRunning={TimerRunning}",
                dockingStopwatch.ElapsedMilliseconds,
                _activityRefreshTimer?.Enabled ?? false);
            Console.WriteLine($"[DIAGNOSTIC] InitializeSyncfusionDocking complete in {dockingStopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Syncfusion DockingManager");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeSyncfusionDocking failed: {ex.Message}");
            // Docking initialization failure is non-critical - system can still function
            // but without docking capabilities
        }
    }

    /// <summary>
    /// Handles theme changes at runtime and reapplies theme to all docking panels.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="theme">New theme to apply.</param>
    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        if (!IsHandleCreated)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to marshal OnThemeChanged to UI thread");
            }
            return;
        }

        try
        {
            _logger?.LogInformation("Applying theme change to docking panels: {Theme}", theme);

            // Reapply theme to all docking panels via layout manager
            if (_dockingLayoutManager != null && _dockingManager != null)
            {
                // Note: We don't have direct access to the panels anymore, but the layout manager handles theme application
                _logger.LogDebug("Theme application delegated to DockingLayoutManager");
            }

            // Refresh activity grid with new theme
            if (_activityGrid != null && !_activityGrid.IsDisposed)
            {
                try
                {
                    // REMOVED: Per-control SetVisualStyle - grid inherits theme from ApplicationVisualTheme
                    // Theme cascade ensures consistent styling across all controls
                    _activityGrid.Refresh();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to refresh activity grid");
                }
            }

            this.Refresh();
            _logger.LogInformation("Theme successfully applied to docking panels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme change to docking panels");
        }
    }

    /// <summary>
    /// Hide standard panels when switching to Syncfusion docking
    /// </summary>
    private void HideStandardPanelsForDocking()
    {
        foreach (Control control in Controls)
        {
            if (control is SplitContainer)
            {
                try
                {
                    control.Visible = false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
                    // Non-critical - continue with other panels
                }
            }
        }
        _logger.LogDebug("Standard panels hidden for Syncfusion docking");
    }

    // Phase 1 Simplification: ToggleDockingMode removed - docking permanently enabled

    /// <summary>
    /// Ensure docking panels and manager are correctly ordered in the Z axis (Phase 1: always enabled)
    /// </summary>
    private void EnsureDockingZOrder()
    {
        try
        {
            // Phase 1 Simplification: Docking always enabled - delegate to layout manager
            if (_dockingManager == null) return;

            try { if (_dockingManager.HostControl is Control host) { host.BringToFront(); } } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to BringToFront on DockingManager host control during EnsureDockingZOrder"); }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to ensure docking z-order");
        }
    }

    /// <summary>
    /// Returns the path to the docking layout file under AppData\WileyWidget.
    /// </summary>
    private static string GetDockingLayoutPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "WileyWidget");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, DockingLayoutFileName);
        }
        catch
        {
            // Fall back to local temp directory on failure
            try { return Path.Combine(Path.GetTempPath(), DockingLayoutFileName); } catch { return DockingLayoutFileName; }
        }
    }

    /// <summary>
    /// Helper: invoke an action on the UI thread and return a Task that completes when the action finishes.
    /// This provides an awaitable wrapper over BeginInvoke with a threadpool fallback.
    /// </summary>
    private Task SafeInvokeAsync(System.Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (!this.IsHandleCreated)
        {
            // If the handle isn't created, run inline to avoid cross-thread failures
            action();
            return Task.CompletedTask;
        }

        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<object?>();
            try
            {
                BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
            }
            catch
            {
                // BeginInvoke failed - fall back to threadpool execution
                try
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            action();
                            tcs.SetResult(null);
                        }
                        catch (Exception innerEx)
                        {
                            tcs.SetException(innerEx);
                        }
                    });
                }
                catch (Exception fallbackEx)
                {
                    tcs.SetException(fallbackEx);
                }
            }
            return tcs.Task;
        }

        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    #region Docking Event Handlers

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        // Log docking state changes
        _logger.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Ensure central panels remain visible after state changes (delegate to layout manager)
        if (_dockingLayoutManager != null)
        {
            // Note: Central panel visibility is now managed by DockingLayoutManager
            _logger.LogDebug("Central panel visibility delegated to DockingLayoutManager");
        }

        // Auto-save layout on state changes with debouncing to prevent I/O spam
        if (_uiConfig.UseSyncfusionDocking && _dockingLayoutManager != null && _dockingManager != null)
        {
            _dockingLayoutManager.StartDebouncedSave(_dockingManager, GetDockingLayoutPath());
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        // Log visibility changes
        _logger.LogDebug("Dock visibility changed");

        // Ensure central panels remain visible after visibility changes (delegate to layout manager)
        if (_dockingLayoutManager != null)
        {
            // Note: Central panel visibility is now managed by DockingLayoutManager
            _logger.LogDebug("Central panel visibility delegated to DockingLayoutManager");
        }
    }

    #endregion

    #region Dynamic Panel Management

    /// <summary>
    /// Adds a custom panel to the docking manager at runtime.
    /// Enables plugin architecture and dynamic content areas.
    /// </summary>
    /// <param name="panelName">Unique identifier for the panel.</param>
    /// <param name="displayLabel">User-facing label for the dock tab.</param>
    /// <param name="content">Control to host in the panel.</param>
    /// <param name="dockStyle">Docking position (Left, Right, Top, Bottom).</param>
    /// <param name="width">Panel width (for Left/Right docking).</param>
    /// <param name="height">Panel height (for Top/Bottom docking).</param>
    /// <returns>True if panel was added successfully, false if docking manager is not available.</returns>
    /// <exception cref="ArgumentException">Thrown when panelName is null or empty.</exception>
    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        if (string.IsNullOrWhiteSpace(panelName))
        {
            throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot add dynamic dock panel - DockingManager not initialized");
            return false;
        }

        GradientPanelExt? panel = null;
        try
        {
            panel = new GradientPanelExt
            {
                Name = panelName,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(panel, "Office2019Colorful");

            // Add content to panel
            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            // Configure docking behavior
            _dockingManager.SetEnableDocking(panel, true);

            // Dock based on style
            if (dockStyle == DockingStyle.Left || dockStyle == DockingStyle.Right)
            {
                _dockingManager.DockControl(panel, this, dockStyle, width);
            }
            else
            {
                _dockingManager.DockControl(panel, this, dockStyle, height);
            }

            _dockingManager.SetAutoHideMode(panel, true);
            _dockingManager.SetDockLabel(panel, displayLabel);

            // Try to set floating mode using layout manager helper
            if (_dockingLayoutManager != null)
            {
                _dockingLayoutManager.TrySetFloatingMode(_dockingManager, panel, true);
            }

            // Note: Dynamic panel tracking is now handled by DockingLayoutManager
            panel = null; // ownership transferred to DockingManager

            _logger.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
        finally
        {
            panel?.Dispose();
        }
    }

    /// <summary>
    /// Removes a dynamically added panel from the docking manager.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <param name="panelName">Name of the panel to remove.</param>
    /// <returns>Always returns false (legacy API).</returns>
    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            return false;

        _logger?.LogDebug("RemoveDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return false;
    }

    /// <summary>
    /// Gets a dynamically added panel by name.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <param name="panelName">Name of the panel to retrieve.</param>
    /// <returns>Always returns null (legacy API).</returns>
    public Control? GetDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            return null;

        _logger?.LogDebug("GetDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return null;
    }

    /// <summary>
    /// Gets all dynamically added panel names.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <returns>Empty collection (legacy API).</returns>
    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return new List<string>().AsReadOnly();
    }

    #endregion

    /// <summary>
    /// Dispose resources owned by the docking implementation
    /// Delegated to DockingLayoutManager for centralized resource management
    /// </summary>
    private void DisposeSyncfusionDockingResources()
    {
        _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - delegating to DockingLayoutManager");

        // Theme subscription removed - SfSkinManager handles theme cascade automatically

        // Unsubscribe from other events

        // Delegate all docking-related disposal to the layout manager
        if (_dockingLayoutManager != null)
        {
            try
            {
                // Best-effort: save layout before disposing
                if (_dockingManager != null && this.IsHandleCreated)
                {
                    try
                    {
                        _dockingLayoutManager.SaveLayout(_dockingManager, GetDockingLayoutPath());
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to save layout during disposal");
                    }
                }

                // Detach from DockingManager
                if (_dockingManager != null)
                {
                    _dockingLayoutManager.DetachFrom(_dockingManager);
                }

                // Dispose the layout manager (handles panels, fonts, timers, dynamic panels)
                _dockingLayoutManager.Dispose();
                _dockingLayoutManager = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose DockingLayoutManager");
            }
        }

        // Dispose the DockingManager itself
        if (_dockingManager != null)
        {
            var mgr = _dockingManager;
            _dockingManager = null;

            try
            {
                mgr.PersistState = false;
                mgr.HostControl = null;
                mgr.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Exception while disposing DockingManager");
            }
        }

        _logger?.LogDebug("DisposeSyncfusionDockingResources completed - all resources delegated to DockingLayoutManager");
    }

    /// <summary>
    /// Updates the docking state text in the status bar.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// </summary>
    private void UpdateDockingStateText()
    {
        try
        {
            if (!IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new System.Action(UpdateDockingStateText));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to marshal UpdateDockingStateText to UI thread");
                }
                return;
            }

            if (_statePanel == null || _statePanel.IsDisposed)
                return;

            var stateInfo = new System.Text.StringBuilder();

            var controls = _dockingManager?.Controls as Control.ControlCollection;
            var childCount = controls?.Count ?? 0;
            stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Panels: {childCount} panel{(childCount != 1 ? "s" : "")}");

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);

            // DIAGNOSTIC: Log control count for troubleshooting docking issues
            _logger?.LogDebug("UpdateDockingStateText: DockingManager control count = {ControlCount}, MainForm control count = {FormControlCount}",
                childCount, this.Controls.Count);
            Console.WriteLine($"[DIAGNOSTIC] UpdateDockingStateText: DockingManager controls={childCount}, MainForm controls={this.Controls.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    #endregion

    #region Panels

    /// <summary>
    /// Closes the settings panel if it's currently visible.
    /// Legacy method: SettingsForm replaced by SettingsPanel.
    /// </summary>
    public void CloseSettingsPanel()
    {
        _panelNavigator?.HidePanel("Settings");
    }

    /// <summary>
    /// Closes a panel with the specified name.
    /// </summary>
    /// <param name="panelName">Name of the panel to close.</param>
    public void ClosePanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
        {
            _logger?.LogWarning("ClosePanel called with null or empty panelName");
            return;
        }

        _panelNavigator?.HidePanel(panelName);
    }

    #endregion
}
