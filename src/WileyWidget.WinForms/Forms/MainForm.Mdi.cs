using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Themes;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// MainForm partial class for Multiple Document Interface (MDI) support.
/// Implements full MDI functionality including:
/// - Standard MDI container setup
/// - Syncfusion TabbedMDIManager for enhanced tabbed MDI interface
/// - Child form management (Show as MDI children vs modal dialogs)
/// - Window menu with Cascade, Tile Horizontal, Tile Vertical
/// - Automatic MDI window list
/// - Menu merging between parent and child forms
/// - Tabbed MDI with visual styles and tab management
///
/// Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/multiple-document-interface-mdi-applications
/// Syncfusion TabbedMDIManager: https://help.syncfusion.com/windowsforms/tabbed-mdi
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class MainForm
{
    // MDI configuration flags (can be set via appsettings.json)
    private bool _useMdiMode = true;
    private bool _useTabbedMdi = true;

    // Syncfusion TabbedMDIManager for enhanced tabbed MDI interface
    private TabbedMDIManager? _tabbedMdiManager;

    // Preserve original MinimumSize for MDI children when we temporarily override
    private readonly Dictionary<Form, Size> _mdiChildOriginalMinimumSizes = new();

    // Track active MDI child forms to prevent duplicates
    private readonly Dictionary<Type, Form> _activeMdiChildren = new();

    /// <summary>
    /// Gets or sets whether MDI mode is enabled.
    /// When true, child forms are shown as MDI children instead of modal dialogs.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool UseMdiMode
    {
        get => _useMdiMode;
        set
        {
            if (_useMdiMode != value)
            {
                _useMdiMode = value;
                ApplyMdiMode();
            }
        }
    }

    /// <summary>
    /// Initialize MDI support during form construction.
    /// Call this from MainForm constructor after InitializeComponent().
    /// </summary>
    private void InitializeMdiSupport()
    {
        if (_isUiTestHarness)
        {
            _useMdiMode = false;
            _useTabbedMdi = false;
            _logger.LogInformation("UI test harness detected; skipping MDI initialization");
            return;
        }

        try
        {
            // Read MDI configuration from appsettings.json (don't override existing value)
            var configMdiMode = _configuration.GetValue<bool?>("UI:UseMdiMode");
            if (configMdiMode.HasValue)
            {
                _useMdiMode = configMdiMode.Value;
            }
            // Otherwise keep the default set in MainForm constructor

            _useTabbedMdi = _configuration.GetValue<bool>("UI:UseTabbedMdi", true);

            if (_useMdiMode)
            {
                _logger.LogInformation("Initializing MDI container mode (TabbedMDI: {UseTabbedMdi})", _useTabbedMdi);
                ApplyMdiMode();
            }
            else
            {
                _logger.LogDebug("MDI mode disabled (using modal dialogs)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize MDI support, falling back to modal dialogs");
            _useMdiMode = false;
            _useTabbedMdi = false;

            // User-friendly fallback: Show message and continue with modal dialogs
            try
            {
                MessageBox.Show(
                    "MDI initialization failed. The application will continue using modal dialog windows instead of MDI.",
                    "MDI Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception msgEx)
            {
                _logger.LogError(msgEx, "Failed to show MDI warning message");
            }
        }
    }

    /// <summary>
    /// Apply MDI mode settings to the form.
    /// </summary>
    private void ApplyMdiMode()
    {
        if (_useMdiMode)
        {
            // Set this form as an MDI container
            IsMdiContainer = true;

            // Customize the MDI client area background color (theme-aware)
            SetMdiClientBackColor(ThemeColors.Background);

            // Initialize Syncfusion TabbedMDIManager if enabled
            if (_useTabbedMdi)
            {
                InitializeTabbedMdiManager();
            }

            // Add Window menu for MDI management
            AddMdiWindowMenu();

            _logger.LogInformation("MDI container mode enabled (TabbedMDI: {UseTabbedMdi})", _useTabbedMdi);
        }
        else
        {
            // Disable MDI container
            IsMdiContainer = false;

            // Dispose TabbedMDIManager if it exists (use safe disposal helper)
            if (_tabbedMdiManager != null)
            {
                SafeDisposeExistingTabbedMdiManager();
            }

            _logger.LogInformation("MDI container mode disabled");
        }
    }

    /// <summary>
    /// Initialize Syncfusion TabbedMDIManager for enhanced tabbed MDI interface.
    /// Provides Visual Studio-style tabbed MDI with tab groups, drag-drop, and more.
    /// </summary>
    private void InitializeTabbedMdiManager()
    {
        try
        {
            _logger.LogInformation("Initializing Syncfusion TabbedMDIManager");

            if (_tabbedMdiManager != null)
            {
                _logger.LogDebug("Existing TabbedMDIManager found during initialization - detaching/disposing to ensure clean state");
                SafeDisposeExistingTabbedMdiManager();
            }

            ClearTabbedTabControlContextMenu();

            _tabbedMdiManager = ServiceProviderExtensions.GetService<TabbedMDIManager>(_serviceProvider)
                ?? new TabbedMDIManager();

            var themeName = _configuration.GetValue<string>("UI:SyncfusionTheme", "Office2019Colorful");

            try
            {
                SfSkinManager.SetVisualStyle(_tabbedMdiManager, themeName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply {Theme} theme to TabbedMDIManager", themeName);
            }

            if (!IsMdiContainer)
            {
                IsMdiContainer = true;
            }

            try
            {
                _tabbedMdiManager.AttachToMdiContainer(this);

                var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
                if (mdiClient != null)
                {
                    mdiClient.Dock = DockStyle.Fill;
                    mdiClient.SendToBack();
                    _logger.LogDebug("TabbedMDI attached: MdiClient ({Name}) sent to back", mdiClient.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to attach TabbedMDIManager to container");
            }

            try
            {
                var dmType = _tabbedMdiManager.GetType();

                var tabsOrientProp = dmType.GetProperty("TabsTextOrientation");
                if (tabsOrientProp != null && tabsOrientProp.CanWrite)
                    tabsOrientProp.SetValue(_tabbedMdiManager, System.Windows.Forms.Orientation.Horizontal);

                var showCloseProp = dmType.GetProperty("ShowCloseButton");
                if (showCloseProp != null && showCloseProp.CanWrite)
                    showCloseProp.SetValue(_tabbedMdiManager, true);

                var tabControlProp = dmType.GetProperty("TabControlAdv");
                if (tabControlProp != null)
                {
                    var tabControl = tabControlProp.GetValue(_tabbedMdiManager);
                    if (tabControl != null)
                    {
                        var p = tabControl.GetType().GetProperty("ShowTabCloseButton");
                        if (p != null && p.CanWrite) p.SetValue(tabControl, true);
                        var p2 = tabControl.GetType().GetProperty("ShowScroll");
                        if (p2 != null && p2.CanWrite) p2.SetValue(tabControl, true);
                        var p3 = tabControl.GetType().GetProperty("SizeMode");
                        if (p3 != null && p3.CanWrite)
                        {
                            try
                            {
                                var enumVal = Enum.Parse(p3.PropertyType, "Normal");
                                p3.SetValue(tabControl, enumVal);
                            }
                            catch { }
                        }
                        var p4 = tabControl.GetType().GetProperty("TabGap");
                        if (p4 != null && p4.CanWrite) p4.SetValue(tabControl, 2);

                        try
                        {
                            SfSkinManager.SetVisualStyle(tabControl as Control, themeName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to apply {Theme} theme to TabControlAdv", themeName);
                        }

                        var themeProp = tabControl.GetType().GetProperty("ThemeName");
                        if (themeProp != null && themeProp.CanWrite)
                        {
                            try { themeProp.SetValue(tabControl, themeName); } catch { }
                        }
                    }
                }

                var listPopupProp = dmType.GetProperty("ShowTabListPopup");
                if (listPopupProp != null && listPopupProp.CanWrite) listPopupProp.SetValue(_tabbedMdiManager, true);

                var newBtnProp = dmType.GetProperty("ShowNewButton");
                if (newBtnProp != null && newBtnProp.CanWrite) newBtnProp.SetValue(_tabbedMdiManager, false);

                var ev1 = dmType.GetEvent("TabControlAdded");
                if (ev1 != null)
                {
                    var eventType1 = ev1.EventHandlerType;
                    if (eventType1 != null)
                    {
                        try { var handler = Delegate.CreateDelegate(eventType1, this, nameof(OnTabbedMdiTabControlAdded)); ev1.AddEventHandler(_tabbedMdiManager, handler); } catch { }
                    }
                }

                var ev2 = dmType.GetEvent("BeforeDropDownPopup");
                if (ev2 != null)
                {
                    var eventType2 = ev2.EventHandlerType;
                    if (eventType2 != null)
                    {
                        try { var handler2 = Delegate.CreateDelegate(eventType2!, this, nameof(OnBeforeDropDownPopup)); ev2.AddEventHandler(_tabbedMdiManager, handler2); } catch { }
                    }
                }

                var ev3 = dmType.GetEvent("MdiChildActivate");
                if (ev3 != null)
                {
                    var eventType3 = ev3.EventHandlerType;
                    if (eventType3 != null)
                    {
                        try { var handler3 = Delegate.CreateDelegate(eventType3!, this, nameof(OnMdiChildActivate)); ev3.AddEventHandler(_tabbedMdiManager, handler3); } catch { }
                    }
                }

                var ev4 = dmType.GetEvent("MdiChildRemoved");
                if (ev4 != null)
                {
                    var eventType4 = ev4.EventHandlerType;
                    if (eventType4 != null)
                    {
                        try { var handler4 = Delegate.CreateDelegate(eventType4!, this, nameof(OnMdiChildRemoved)); ev4.AddEventHandler(_tabbedMdiManager, handler4); } catch { }
                    }
                }

                ConfigureTabbedMdiFeatures();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TabbedMDI initialization encountered a reflection error");
                ConfigureTabbedMdiFeatures();
            }

            _tabbedMdiAttached = true;
            _logger.LogInformation("TabbedMDIManager initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TabbedMDIManager, falling back to standard MDI");
            _useTabbedMdi = false;
            _tabbedMdiManager?.Dispose();
            _tabbedMdiManager = null;
        }
    }

    /// <summary>
    /// Clear any ContextMenuStrip/Holders on the TabControlAdv that may be tied to a previous TabbedMDIManager.
    /// This prevents Syncfusion exceptions about reusing a ContextMenuPlaceHolder for different managers.
    /// </summary>
    private void ClearTabbedTabControlContextMenu()
    {
        try
        {
            var tabCtrl = GetTabbedTabControl();
            if (tabCtrl == null) return;

            ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuStrip");
            ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuPlaceHolder");
            ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuStripPlaceHolder");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ClearTabbedTabControlContextMenu encountered an error");
        }
    }

    private void ResetTabControlContextMenuProperty(object tabControl, string propertyName)
    {
        if (tabControl == null) return;

        var prop = tabControl.GetType().GetProperty(propertyName);
        if (prop == null || !prop.CanWrite) return;

        try
        {
            var existing = prop.GetValue(tabControl);
            prop.SetValue(tabControl, null);
            if (existing is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to reset {PropertyName} on TabControlAdv", propertyName);
        }
    }

    /// <summary>
    /// Attempt to safely detach and dispose of the existing TabbedMDIManager.
    /// This will try to clear events, detach from container, and dispose the object.
    /// </summary>
    private void SafeDisposeExistingTabbedMdiManager()
    {
        if (_tabbedMdiManager == null) return;

        try
        {
            // Clear ContextMenu placeholder first
            ClearTabbedTabControlContextMenu();

            var dmType = _tabbedMdiManager.GetType();

            // Remove event handlers where possible
            try
            {
                var ev1 = dmType.GetEvent("TabControlAdded");
                if (ev1 != null && ev1.EventHandlerType != null)
                {
                    try { var handler = Delegate.CreateDelegate(ev1.EventHandlerType, this, nameof(OnTabbedMdiTabControlAdded)); ev1.RemoveEventHandler(_tabbedMdiManager, handler); } catch { }
                }

                var ev2 = dmType.GetEvent("BeforeDropDownPopup");
                if (ev2 != null && ev2.EventHandlerType != null)
                {
                    try { var handler2 = Delegate.CreateDelegate(ev2.EventHandlerType, this, nameof(OnBeforeDropDownPopup)); ev2.RemoveEventHandler(_tabbedMdiManager, handler2); } catch { }
                }

                var ev3 = dmType.GetEvent("MdiChildActivate");
                if (ev3 != null && ev3.EventHandlerType != null)
                {
                    try { var handler3 = Delegate.CreateDelegate(ev3.EventHandlerType, this, nameof(OnMdiChildActivate)); ev3.RemoveEventHandler(_tabbedMdiManager, handler3); } catch { }
                }

                var ev4 = dmType.GetEvent("MdiChildRemoved");
                if (ev4 != null && ev4.EventHandlerType != null)
                {
                    try { var handler4 = Delegate.CreateDelegate(ev4.EventHandlerType, this, nameof(OnMdiChildRemoved)); ev4.RemoveEventHandler(_tabbedMdiManager, handler4); } catch { }
                }
            }
            catch { }

            // Try to detach from MDI container using either signature
            try
            {
                var noArg = dmType.GetMethod("DetachFromMdiContainer", Type.EmptyTypes);
                if (noArg != null) noArg.Invoke(_tabbedMdiManager, null);
                else
                {
                    var oneBool = dmType.GetMethods().FirstOrDefault(m => m.Name == "DetachFromMdiContainer" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));
                    oneBool?.Invoke(_tabbedMdiManager, new object[] { false });
                }
            }
            catch { }

            // Dispose instance
            try { _tabbedMdiManager.Dispose(); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing existing TabbedMDIManager safely");
        }
        finally
        {
            _tabbedMdiManager = null;
            _tabbedMdiAttached = false;
        }
    }

    /// <summary>
    /// Handle TabbedMDI tab control added event.
    /// </summary>
    private void OnTabbedMdiTabControlAdded(object? sender, TabbedMDITabControlEventArgs e)
    {
        try
        {
            // Apply theme to newly added tab control (reflection-safe)
            var newTabControl = e?.GetType().GetProperty("TabControlAdv")?.GetValue(e);
            if (newTabControl != null)
            {
                var themeName = _configuration.GetValue<string>("UI:SyncfusionTheme", "Office2019Colorful");
                var themeProp = newTabControl.GetType().GetProperty("ThemeName");
                if (themeProp != null && themeProp.CanWrite) themeProp.SetValue(newTabControl, themeName);

                // Set tab style for proper rendering in docking context
                var tabStyleProp = newTabControl.GetType().GetProperty("TabStyle");
                if (tabStyleProp != null && tabStyleProp.CanWrite)
                {
                    try { tabStyleProp.SetValue(newTabControl, typeof(Syncfusion.Windows.Forms.Tools.TabRendererOffice2016Colorful)); }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to set TabStyle to TabRendererOffice2016Colorful");
                    }
                }

                _logger.LogDebug("Applied {ThemeName} theme and docking tab style to TabbedMDI tab control", themeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying theme and tab style to TabbedMDI tab control");
        }
    }

    /// <summary>
    /// Handle before dropdown popup event for tab list.
    /// </summary>
    private void OnBeforeDropDownPopup(object? sender, EventArgs e)
    {
        _logger.LogDebug("TabbedMDI dropdown popup requested");
    }

    /// <summary>
    /// Handle MDI child activate event.
    /// </summary>
    private void OnMdiChildActivate(object? sender, EventArgs e)
    {
        try
        {
            if (ActiveMdiChild != null)
            {
                _logger.LogDebug("MDI child activated: {FormType}", ActiveMdiChild.GetType().Name);

                // Update status bar if available
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Active: {ActiveMdiChild.Text}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling MDI child activate");
        }
    }

    /// <summary>
    /// Handle MDI child removed event.
    /// </summary>
    private void OnMdiChildRemoved(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("MDI child removed");
            // Check if form is DockingWrapperForm before any cast
            // Assuming e has the form, but since reflection, check avoided to prevent issues
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling MDI child removed");
        }
    }

    /// <summary>
    /// Configure additional TabbedMDI features.
    /// </summary>
    private void ConfigureTabbedMdiFeatures()
    {
        if (_tabbedMdiManager == null) return;

        try
        {
            // Enable drag-and-drop for tab reordering (default is usually enabled)
            // This allows users to drag tabs to reorder them or create new tab groups

            // Configure tab appearance
            var tabControl = GetTabbedTabControl();
            if (tabControl != null)
            {
                try
                {
                    var showTTProp = tabControl.GetType().GetProperty("ShowToolTips");
                    if (showTTProp != null && showTTProp.CanWrite) showTTProp.SetValue(tabControl, true);

                    var fixedBorderProp = tabControl.GetType().GetProperty("FixedSingleBorderColor");
                    if (fixedBorderProp != null && fixedBorderProp.CanWrite) fixedBorderProp.SetValue(tabControl, ThemeColors.Background);

                    var tabStyleProp = tabControl.GetType().GetProperty("TabStyle");
                    if (tabStyleProp != null && tabStyleProp.CanWrite)
                    {
                        try { tabStyleProp.SetValue(tabControl, typeof(Syncfusion.Windows.Forms.Tools.TabRendererOffice2016Colorful)); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to apply TabControlAdv features via reflection");
                }
            }

            // Configure context menu for tabs
            ConfigureTabContextMenu();

            _logger.LogDebug("TabbedMDI advanced features configured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error configuring TabbedMDI advanced features");
        }
    }

    /// <summary>
    /// Configure context menu for MDI tabs.
    /// </summary>
    private void ConfigureTabContextMenu()
    {
        var tabControl = GetTabbedTabControl();
            if (tabControl == null) return;

        try
        {
            // Create context menu for tabs
            var tabContextMenu = new ContextMenuStrip();

            // Close tab
            var closeItem = new ToolStripMenuItem("&Close", null, (s, e) =>
            {
                ActiveMdiChild?.Close();
            })
            {
                ShortcutKeyDisplayString = "Ctrl+F4"
            };

            // Close all but this
            var closeAllButThisItem = new ToolStripMenuItem("Close All &But This", null, (s, e) =>
            {
                var activeForm = ActiveMdiChild;
                if (activeForm == null) return;

                var childrenToClose = MdiChildren.Where(f => f != activeForm).ToArray();
                foreach (var child in childrenToClose)
                {
                    try { child.Close(); } catch { }
                }
            });

            // Close all
            var closeAllItem = new ToolStripMenuItem("Close &All", null, (s, e) => CloseAllMdiChildren())
            {
                ShortcutKeyDisplayString = "Ctrl+Shift+W"
            };

            // Separator
            var separator1 = new ToolStripSeparator();

            // New window (if applicable)
            var newWindowItem = new ToolStripMenuItem("&New Window", null, (s, e) =>
            {
                // This would duplicate the current window - implement if needed
                _logger.LogInformation("New window requested (not implemented)");
            })
            {
                Enabled = false
            };

            // Add items to context menu
            tabContextMenu.Items.Add(closeItem);
            tabContextMenu.Items.Add(closeAllButThisItem);
            tabContextMenu.Items.Add(closeAllItem);
            tabContextMenu.Items.Add(separator1);
            tabContextMenu.Items.Add(newWindowItem);

            // Assign context menu to TabControlAdv (clear any placeholder first)
            var tabCtrl = GetTabbedTabControl();
            if (tabCtrl != null)
            {
                var ctxProp = tabCtrl.GetType().GetProperty("ContextMenuStrip");
                if (ctxProp != null && ctxProp.CanWrite)
                {
                    try
                    {
                        // Clear existing/placeholder first (may be from previous manager)
                        ctxProp.SetValue(tabCtrl, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to clear existing TabControlAdv ContextMenuStrip before setting new one");
                    }

                    try
                    {
                        ctxProp.SetValue(tabCtrl, tabContextMenu);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set ContextMenuStrip on TabControlAdv");
                    }
                }
            }

            _logger.LogDebug("Tab context menu configured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error configuring tab context menu");
        }
    }

    /// <summary>
    /// Set the background color of the MDI client area.
    /// The MDI client area is the container for MDI child forms.
    /// </summary>
    private void SetMdiClientBackColor(Color color)
    {
        foreach (Control control in Controls)
        {
            if (control is MdiClient mdiClient)
            {
                mdiClient.BackColor = color;
                _logger.LogDebug("Set MDI client background color to {Color}", color);
                break;
            }
        }
    }

    /// <summary>
    /// Add Window menu to the main menu strip with MDI management commands.
    /// </summary>
    private void AddMdiWindowMenu()
    {
        // Find the main MenuStrip
        var menuStrip = Controls.OfType<MenuStrip>().FirstOrDefault();
        if (menuStrip == null)
        {
            // try the MainForm field
            menuStrip = _menuStrip;
        }
        if (menuStrip == null)
        {
            // create a hidden MenuStrip for MDI window list
            try
            {
                menuStrip = new MenuStrip { Name = "MainMenuStrip", Dock = DockStyle.Top, Visible = false, AllowMerge = true };
                Controls.Add(menuStrip);
                this.MainMenuStrip = menuStrip;
                _logger.LogInformation("Created hidden MenuStrip for MDI support");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MenuStrip not found and creation failed; cannot add Window menu");
                return;
            }
        }

        // Check if Window menu already exists
        var existingWindowMenu = menuStrip.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text == "&Window");

        if (existingWindowMenu != null)
        {
            _logger.LogDebug("Window menu already exists, updating it");
            menuStrip.Items.Remove(existingWindowMenu);
        }

        // Create Window menu
        var windowMenu = new ToolStripMenuItem("&Window");

        // Add window arrangement commands
        var cascadeMenuItem = new ToolStripMenuItem("&Cascade", null, (s, e) => LayoutMdi(MdiLayout.Cascade))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.C,
            ToolTipText = "Arrange windows in a cascade"
        };

        var tileHorizontalMenuItem = new ToolStripMenuItem("Tile &Horizontal", null, (s, e) => LayoutMdi(MdiLayout.TileHorizontal))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.H,
            ToolTipText = "Arrange windows in horizontal tiles"
        };

        var tileVerticalMenuItem = new ToolStripMenuItem("Tile &Vertical", null, (s, e) => LayoutMdi(MdiLayout.TileVertical))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.V,
            ToolTipText = "Arrange windows in vertical tiles"
        };

        var arrangeIconsMenuItem = new ToolStripMenuItem("Arrange &Icons", null, (s, e) => LayoutMdi(MdiLayout.ArrangeIcons))
        {
            ToolTipText = "Arrange minimized window icons"
        };

        var closeAllMenuItem = new ToolStripMenuItem("Close &All", null, (s, e) => CloseAllMdiChildren())
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.W,
            ToolTipText = "Close all open windows"
        };

        // Add separator
        var separator = new ToolStripSeparator();

        // Add items to Window menu
        windowMenu.DropDownItems.Add(cascadeMenuItem);
        windowMenu.DropDownItems.Add(tileHorizontalMenuItem);
        windowMenu.DropDownItems.Add(tileVerticalMenuItem);
        windowMenu.DropDownItems.Add(arrangeIconsMenuItem);
        windowMenu.DropDownItems.Add(separator);
        windowMenu.DropDownItems.Add(closeAllMenuItem);
        windowMenu.DropDownItems.Add(new ToolStripSeparator());

        // Configure automatic MDI window list
        // The MenuStrip will automatically add a list of open MDI child windows
        menuStrip.MdiWindowListItem = windowMenu;

        // Insert Window menu before Help menu (if exists) or at the end
        var helpMenuIndex = -1;
        for (int i = 0; i < menuStrip.Items.Count; i++)
        {
            var menuItem = menuStrip.Items[i] as ToolStripMenuItem;
            if (menuItem?.Text != null && menuItem.Text.Contains("Help", StringComparison.OrdinalIgnoreCase))
            {
                helpMenuIndex = i;
                break;
            }
        }

        if (helpMenuIndex >= 0)
        {
            menuStrip.Items.Insert(helpMenuIndex, windowMenu);
        }
        else
        {
            menuStrip.Items.Add(windowMenu);
        }

        _logger.LogInformation("Added Window menu with MDI management commands");
        }

        private object? GetTabbedTabControl()
        {
            try
            {
                if (_tabbedMdiManager == null) return null;
                var prop = _tabbedMdiManager.GetType().GetProperty("TabControlAdv");
                return prop?.GetValue(_tabbedMdiManager);
            }
            catch { return null; }
        }

    /// <summary>
    /// Close all MDI child forms.
    /// </summary>
    private void CloseAllMdiChildren()
    {
        try
        {
            _logger.LogInformation("Closing all MDI child forms");

            // Create a copy of the array since closing forms modifies the collection
            var children = MdiChildren.ToArray();

            foreach (var child in children)
            {
                try
                {
                    child.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to close MDI child form {FormType}", child.GetType().Name);
                }
            }

            // Clear tracking dictionary
            _activeMdiChildren.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close all MDI children");
        }
    }

    /// <summary>
    /// Shows a child form as either an MDI child or modal dialog based on UseMdiMode setting.
    /// Creates a new service scope for fresh instances of ViewModels and DbContexts.
    /// </summary>
    /// <typeparam name="TForm">The type of form to show.</typeparam>
    /// <typeparam name="TViewModel">The type of ViewModel associated with the form.</typeparam>
    /// <param name="allowMultiple">If false, reuses existing MDI child window of same type instead of creating new one.</param>
    private void ShowChildFormMdi<TForm, TViewModel>(bool allowMultiple = false)
        where TForm : Form
        where TViewModel : class
    {
        try
        {
            _logger.LogInformation("Showing child form {FormType} (MDI mode: {MdiMode}, AllowMultiple: {AllowMultiple})",
                typeof(TForm).Name, _useMdiMode, allowMultiple);

            if (!_useMdiMode)
            {
                ShowNonMdiChildForm<TForm, TViewModel>(allowMultiple);
                return;
            }

            if (!IsMdiContainer)
            {
                IsMdiContainer = true;
            }

            // In MDI mode, check if we should reuse an existing window
            if (!allowMultiple && _activeMdiChildren.TryGetValue(typeof(TForm), out var existingForm))
            {
                    if (existingForm != null && !existingForm.IsDisposed)
                    {
                        try
                        {
                            existingForm.BringToFront();
                            existingForm.Activate();
                        }
                        catch { }
                        _logger.LogDebug("Activated existing MDI child {FormType}", typeof(TForm).Name);
                        return;
                    }

                    _activeMdiChildren.Remove(typeof(TForm));
            }

            // Create a new scope to get fresh DbContext + ViewModels for each child window
            var scope = _serviceProvider.CreateScope();
            var form = CreateFormInstance<TForm, TViewModel>(scope);

            // Ensure DockingManager and TabbedMDIManager are configured to coexist safely.
            // (Docking panels integrate into the MDI container; Form documents remain regular MDI children.)
            RegisterMdiChildWithDocking(form);

            // Enforce TabbedMDI constraints: child forms hosted in a TabbedMDIManager must have default MinimumSize (0,0)
            try
            {
                if (_tabbedMdiManager != null)
                {
                    if (form.MinimumSize.Width != 0 || form.MinimumSize.Height != 0)
                    {
                        try
                        {
                            _mdiChildOriginalMinimumSizes[form] = form.MinimumSize;
                        }
                        catch { }

                        try { form.MinimumSize = Size.Empty; } catch { }
                        _logger.LogDebug("Overrode non-default MinimumSize for TabbedMDI child {FormType}", typeof(TForm).Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enforcing MinimumSize for TabbedMDI child {FormType}", typeof(TForm).Name);
            }

            try
            {
                // CRITICAL FIX: For TabbedMDIManager, use standard MDI pattern (form.MdiParent = this; form.Show()).
                // TabbedMDIManager automatically wraps the form in its internal document container.
                // DO NOT call SetAsMDIChild on Form instances - that method expects UserControls/Panels
                // and causes InvalidCastException when TabbedMDIManager tries to cast to DockingWrapperForm.
                //
                // Syncfusion integration pattern:
                // - TabbedMDIManager: Handles Form-based MDI documents via standard MDI APIs
                // - DockingManager.SetAsMDIChild: Only for UserControl/Panel-based dockable documents

                if (!form.IsMdiChild)
                {
                    form.MdiParent = this;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set MdiParent for {FormType}, attempting recovery", typeof(TForm).Name);
                // Recovery: ensure form is at least shown as owned window
                try
                {
                    form.Owner = this;
                }
                catch (Exception recoverEx)
                {
                    _logger.LogError(recoverEx, "Failed to recover from MdiParent assignment failure for {FormType}", typeof(TForm).Name);
                }
            }

            // Handle form closing to clean up scope and tracking
            form.FormClosed += (s, e) =>
            {
                try
                {
                    _activeMdiChildren.Remove(typeof(TForm));
                    // Restore original MinimumSize if we changed it earlier
                    try
                    {
                        if (_mdiChildOriginalMinimumSizes.TryGetValue(form, out var original))
                        {
                            try { if (!form.IsDisposed) form.MinimumSize = original; } catch { }
                            _mdiChildOriginalMinimumSizes.Remove(form);
                        }
                    }
                    catch { }
                    scope.Dispose();
                    _logger.LogDebug("MDI child {FormType} closed and cleaned up", typeof(TForm).Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up MDI child {FormType}", typeof(TForm).Name);
                }
            };

            // Track the form
            if (!allowMultiple)
            {
                _activeMdiChildren[typeof(TForm)] = form;
            }

            // Show the form (non-modal, as MDI child)
            form.Show();

            _logger.LogInformation("MDI child form {FormType} shown", typeof(TForm).Name);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogDebug(oce, "Showing child form {FormType} was canceled", typeof(TForm).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show child form {FormType}", typeof(TForm).Name);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
#endif
            throw;
        }
    }

    internal void RegisterMdiChildWithDocking(Form child)
    {
        if (child == null)
        {
            return;
        }

        if (_dockingManager == null || !_useSyncfusionDocking)
        {
            return;
        }

        // Syncfusion integration guidance:
        // - Use TabbedMDIManager for Form-based MDI documents
        // - Use DockingManager for dockable panels (integrated into the MDI container via SetAsMDIChild)
        // - Keep DockingManager document mode disabled when TabbedMDI is active to avoid wrapper-form assumptions

        // CRITICAL: Force EnableDocumentMode to false if TabbedMDI is active, regardless of initialization state
        if (_useMdiMode && _useTabbedMdi)
        {
            if (_dockingManager.EnableDocumentMode)
            {
                try
                {
                    _dockingManager.EnableDocumentMode = false;
                    _logger.LogWarning("DEFENSIVE FIX: Disabled DockingManager.EnableDocumentMode because TabbedMDI is active (was unexpectedly true)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CRITICAL: Failed to disable DockingManager.EnableDocumentMode - InvalidCastException likely");
                }
            }
            else
            {
                _logger.LogDebug("RegisterMdiChildWithDocking: EnableDocumentMode already disabled (correct state for TabbedMDI)");
            }
        }

        // Ensure MDI child Forms are not treated as dockable windows.
        try
        {
            _dockingManager.SetEnableDocking(child, false);
        }
        catch
        {
            // Some Syncfusion builds may throw when calling SetEnableDocking on top-level Forms.
        }
    }

    /// <summary>
    /// Registers an MDI child form with the docking system (if applicable).
    /// This is a compatibility method for child forms that use the older API.
    /// The bool parameter is ignored as docking is not used for Form instances.
    /// </summary>
    /// <param name="child">The child form to register</param>
    /// <param name="enabled">Ignored - kept for API compatibility</param>
    public void RegisterAsDockingMDIChild(Form child, bool enabled)
    {
        // Delegate to the actual implementation (enabled parameter is ignored)
        RegisterMdiChildWithDocking(child);
    }

    /// <summary>
    /// Closes the settings panel if it's displayed in the current form.
    /// Called by SettingsPanel to hide itself.
    /// </summary>
    public void CloseSettingsPanel()
    {
        // Find and close any SettingsForm child windows
        foreach (Form childForm in this.MdiChildren)
        {
            if (childForm is SettingsForm settingsForm)
            {
                settingsForm.Close();
                return;
            }
        }
    }

    /// <summary>
    /// Closes a panel by name. Used by panels to close themselves.
    /// </summary>
    public void ClosePanel(string panelName)
    {
        // Find and close child form or panel by name using LINQ
        var matchingForm = this.MdiChildren.FirstOrDefault(f =>
            f.Text.Contains(panelName, StringComparison.OrdinalIgnoreCase));

        matchingForm?.Close();
    }

    private TForm CreateFormInstance<TForm, TViewModel>(IServiceScope scope)
        where TForm : Form
        where TViewModel : class
    {
        if (typeof(TForm) == typeof(ChartForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ChartViewModel>(scope.ServiceProvider);
            var chartForm = ActivatorUtilities.CreateInstance<ChartForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)chartForm;
        }

        if (typeof(TForm) == typeof(AccountsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scope.ServiceProvider);
            var accountsForm = ActivatorUtilities.CreateInstance<AccountsForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)accountsForm;
        }

        if (typeof(TForm) == typeof(BudgetOverviewForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<BudgetOverviewViewModel>(scope.ServiceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<BudgetOverviewForm>>(scope.ServiceProvider);
            var budgetOverviewForm = ActivatorUtilities.CreateInstance<BudgetOverviewForm>(scope.ServiceProvider, vm, logger, this);
            return (TForm)(Form)budgetOverviewForm;
        }

        if (typeof(TForm) == typeof(DashboardForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DashboardViewModel>(scope.ServiceProvider);
            var dashboardForm = ActivatorUtilities.CreateInstance<DashboardForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)dashboardForm;
        }

        if (typeof(TForm) == typeof(ReportsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ReportsViewModel>(scope.ServiceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ReportsForm>>(scope.ServiceProvider);
            var reportsForm = ActivatorUtilities.CreateInstance<ReportsForm>(scope.ServiceProvider, vm, logger, this);
            return (TForm)(Form)reportsForm;
        }

        if (typeof(TForm) == typeof(SettingsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(scope.ServiceProvider);
            var settingsForm = ActivatorUtilities.CreateInstance<SettingsForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)settingsForm;
        }

        return ActivatorUtilities.CreateInstance<TForm>(scope.ServiceProvider);
    }

    /// <summary>
    /// Get all currently open MDI child forms of a specific type.
    /// </summary>
    private IEnumerable<TForm> GetMdiChildrenOfType<TForm>() where TForm : Form
    {
        return MdiChildren.OfType<TForm>();
    }

    /// <summary>
    /// Activate (bring to front) an MDI child form of specific type if it exists.
    /// </summary>
    /// <returns>True if form was found and activated, false otherwise.</returns>
    private bool ActivateMdiChildOfType<TForm>() where TForm : Form
    {
        var existingForm = GetMdiChildrenOfType<TForm>().FirstOrDefault();
        if (existingForm != null && !existingForm.IsDisposed)
        {
            existingForm.Activate();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Dispose MDI-related resources.
    /// Call this from the main Dispose method.
    /// </summary>
    private void DisposeMdiResources()
    {
        try
        {
            // Dispose TabbedMDIManager first
            if (_tabbedMdiManager != null)
            {
                try
                {
                    // Remove event handlers via reflection so we can compile/run against
                    // multiple Syncfusion versions which may not expose the same events
                    try
                    {
                        var dmType = _tabbedMdiManager.GetType();

                        var ev1 = dmType.GetEvent("TabControlAdded");
                        if (ev1 != null && ev1.EventHandlerType != null)
                        {
                            try { var handler = Delegate.CreateDelegate(ev1.EventHandlerType, this, nameof(OnTabbedMdiTabControlAdded)); ev1.RemoveEventHandler(_tabbedMdiManager, handler); } catch { }
                        }

                        var ev2 = dmType.GetEvent("BeforeDropDownPopup");
                        if (ev2 != null && ev2.EventHandlerType != null)
                        {
                            try { var handler2 = Delegate.CreateDelegate(ev2.EventHandlerType, this, nameof(OnBeforeDropDownPopup)); ev2.RemoveEventHandler(_tabbedMdiManager, handler2); } catch { }
                        }

                        var ev3 = dmType.GetEvent("MdiChildActivate");
                        if (ev3 != null && ev3.EventHandlerType != null)
                        {
                            try { var handler3 = Delegate.CreateDelegate(ev3.EventHandlerType, this, nameof(OnMdiChildActivate)); ev3.RemoveEventHandler(_tabbedMdiManager, handler3); } catch { }
                        }

                        var ev4 = dmType.GetEvent("MdiChildRemoved");
                        if (ev4 != null && ev4.EventHandlerType != null)
                        {
                            try { var handler4 = Delegate.CreateDelegate(ev4.EventHandlerType, this, nameof(OnMdiChildRemoved)); ev4.RemoveEventHandler(_tabbedMdiManager, handler4); } catch { }
                        }
                    }
                    catch { }

                    // Dispose context menu if exists - clear property first to remove any placeholder binding
                    var tabCtrl = GetTabbedTabControl();
                    if (tabCtrl != null)
                    {
                        ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuStrip");
                        ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuPlaceHolder");
                        ResetTabControlContextMenuProperty(tabCtrl, "ContextMenuStripPlaceHolder");
                    }

                    try
                    {
                        var dmType = _tabbedMdiManager.GetType();
                        var noArg = dmType.GetMethod("DetachFromMdiContainer", Type.EmptyTypes);
                        if (noArg != null) noArg.Invoke(_tabbedMdiManager, null);
                        else
                        {
                            var oneBool = dmType.GetMethods().FirstOrDefault(m => m.Name == "DetachFromMdiContainer" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));
                            oneBool?.Invoke(_tabbedMdiManager, new object[] { false });
                        }
                    }
                    catch { }
                    _tabbedMdiManager.Dispose();
                    _tabbedMdiManager = null;
                    _tabbedMdiAttached = false;
                    _logger.LogDebug("TabbedMDIManager disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing TabbedMDIManager");
                }
            }

            // Close all MDI children before disposal
            if (_useMdiMode && MdiChildren.Length > 0)
            {
                CloseAllMdiChildren();
            }

            _activeMdiChildren.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing MDI resources");
        }
    }

    /// <summary>
    /// Handle MDI-specific keyboard shortcuts.
    /// </summary>
    private void HandleMdiKeyboardShortcuts(KeyEventArgs e)
    {
        if (!_useMdiMode) return;

        // Ctrl+Tab: Cycle through MDI children
        if (e.Control && e.KeyCode == Keys.Tab && !e.Shift)
        {
            ActivateNextMdiChild();
            e.Handled = true;
        }
        // Ctrl+Shift+Tab: Cycle backwards through MDI children
        else if (e.Control && e.Shift && e.KeyCode == Keys.Tab)
        {
            ActivatePreviousMdiChild();
            e.Handled = true;
        }
        // Ctrl+F4: Close active MDI child
        else if (e.Control && e.KeyCode == Keys.F4)
        {
            ActiveMdiChild?.Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Activate the next MDI child form (cycle forward).
    /// </summary>
    private void ActivateNextMdiChild()
    {
        var children = MdiChildren;
        if (children.Length <= 1) return;

        var activeChild = ActiveMdiChild;
        if (activeChild == null)
        {
            children[0].Activate();
            return;
        }

        var currentIndex = Array.IndexOf(children, activeChild);
        var nextIndex = (currentIndex + 1) % children.Length;
        children[nextIndex].Activate();
    }

    /// <summary>
    /// Activate the previous MDI child form (cycle backward).
    /// </summary>
    private void ActivatePreviousMdiChild()
    {
        var children = MdiChildren;
        if (children.Length <= 1) return;

        var activeChild = ActiveMdiChild;
        if (activeChild == null)
        {
            children[children.Length - 1].Activate();
            return;
        }

        var currentIndex = Array.IndexOf(children, activeChild);
        var previousIndex = currentIndex == 0 ? children.Length - 1 : currentIndex - 1;
        children[previousIndex].Activate();
    }

    /// <summary>
    /// Configure menu merging for MDI child forms.
    /// This should be called by child forms to enable menu merging.
    /// </summary>
    /// <param name="childMenuStrip">The MenuStrip from the child form.</param>
    public void ConfigureChildMenuMerging(MenuStrip childMenuStrip)
    {
        if (childMenuStrip == null) return;

        // Enable merging
        childMenuStrip.AllowMerge = true;

        // Find parent MenuStrip
        var parentMenuStrip = Controls.OfType<MenuStrip>().FirstOrDefault();
        if (parentMenuStrip != null)
        {
            parentMenuStrip.AllowMerge = true;
            _logger.LogDebug("Configured menu merging for child form");
        }
    }
}
