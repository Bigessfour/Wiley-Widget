using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

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
    private bool _useMdiMode = false;
    private bool _useTabbedMdi = false;

    // Syncfusion TabbedMDIManager for enhanced tabbed MDI interface
    private TabbedMDIManager? _tabbedMdiManager;

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
        try
        {
            // Read MDI configuration from appsettings.json
            _useMdiMode = _configuration.GetValue<bool>("UI:UseMdiMode", false);
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

            // Customize the MDI client area background color
            SetMdiClientBackColor(Color.FromArgb(45, 45, 48)); // Dark theme

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

            // Dispose TabbedMDIManager if it exists
            if (_tabbedMdiManager != null)
            {
                try
                {
                    _tabbedMdiManager.DetachFromMdiContainer();
                    _tabbedMdiManager.Dispose();
                    _tabbedMdiManager = null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing TabbedMDIManager");
                }
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

            // Create TabbedMDIManager instance
            _tabbedMdiManager = new TabbedMDIManager
            {
                // Tab positioning
                TabsTextOrientation = System.Windows.Forms.Orientation.Horizontal,

                // Enable close button on tabs
                ShowCloseButton = true,

                // Tab appearance - use ThemeName instead of hardcoded colors
                TabControlAdv =
                {
                    ShowTabCloseButton = true,
                    ShowScroll = true,
                    SizeMode = Syncfusion.Windows.Forms.Tools.TabSizeMode.Normal,
                    TabGap = 2
                },

                // Enable drop-down for tab list when many tabs are open
                ShowTabListPopup = true,

                // Enable New and Close buttons
                ShowNewButton = false, // We handle this through menus
            };

            // Apply theme from configuration (uses SkinManager)
            var themeName = _configuration.GetValue<string>("UI:SyncfusionTheme", "Office2019Colorful");
            _tabbedMdiManager.ThemeName = themeName;

            // Also apply theme to TabControlAdv
            if (_tabbedMdiManager.TabControlAdv != null)
            {
                _tabbedMdiManager.TabControlAdv.ThemeName = themeName;
            }

            // Attach to this MDI container
            _tabbedMdiManager.AttachToMdiContainer(this);

            // Subscribe to tab events
            _tabbedMdiManager.TabControlAdded += OnTabbedMdiTabControlAdded;
            _tabbedMdiManager.BeforeDropDownPopup += OnBeforeDropDownPopup;
            _tabbedMdiManager.MdiChildActivate += OnMdiChildActivate;

            // Configure additional features
            ConfigureTabbedMdiFeatures();

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
    /// Handle TabbedMDI tab control added event.
    /// </summary>
    private void OnTabbedMdiTabControlAdded(object? sender, TabbedMDITabControlEventArgs e)
    {
        try
        {
            // Apply theme to newly added tab control
            if (e.TabControlAdv != null)
            {
                var themeName = _configuration.GetValue<string>("UI:SyncfusionTheme", "Office2019Colorful");
                e.TabControlAdv.ThemeName = themeName;
                _logger.LogDebug("Applied {ThemeName} theme to TabbedMDI tab control", themeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying theme to TabbedMDI tab control");
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
            if (_tabbedMdiManager.TabControlAdv != null)
            {
                var tabControl = _tabbedMdiManager.TabControlAdv;

                // Enable tooltips on tabs
                tabControl.ShowToolTips = true;

                // Configure tab sizing
                tabControl.FixedSingleBorderColor = Color.FromArgb(45, 45, 48);

                // Tab selection behavior
                tabControl.TabStyle = typeof(Syncfusion.Windows.Forms.Tools.TabRendererOffice2016Colorful);
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
        if (_tabbedMdiManager?.TabControlAdv == null) return;

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

            // Assign context menu to TabControlAdv
            _tabbedMdiManager.TabControlAdv.ContextMenuStrip = tabContextMenu;

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
            _logger.LogWarning("MenuStrip not found, cannot add Window menu");
            return;
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
                // Fall back to modal dialog behavior
                ShowChildForm<TForm, TViewModel>();
                return;
            }

            // In MDI mode, check if we should reuse an existing window
            if (!allowMultiple && _activeMdiChildren.TryGetValue(typeof(TForm), out var existingForm))
            {
                // Bring existing form to front
                if (!existingForm.IsDisposed)
                {
                    existingForm.Activate();
                    _logger.LogDebug("Activated existing MDI child {FormType}", typeof(TForm).Name);
                    return;
                }
                else
                {
                    // Remove disposed form from tracking
                    _activeMdiChildren.Remove(typeof(TForm));
                }
            }

            // Create a new scope to get fresh DbContext + ViewModels for each child window
            var scope = _serviceProvider.CreateScope();
            var form = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TForm>(scope.ServiceProvider);

            // Configure as MDI child
            form.MdiParent = this;

            // Handle form closing to clean up scope and tracking
            form.FormClosed += (s, e) =>
            {
                try
                {
                    _activeMdiChildren.Remove(typeof(TForm));
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
                    _tabbedMdiManager.TabControlAdded -= OnTabbedMdiTabControlAdded;
                    _tabbedMdiManager.BeforeDropDownPopup -= OnBeforeDropDownPopup;
                    _tabbedMdiManager.MdiChildActivate -= OnMdiChildActivate;

                    // Dispose context menu if exists
                    if (_tabbedMdiManager.TabControlAdv?.ContextMenuStrip != null)
                    {
                        _tabbedMdiManager.TabControlAdv.ContextMenuStrip.Dispose();
                    }

                    _tabbedMdiManager.DetachFromMdiContainer();
                    _tabbedMdiManager.Dispose();
                    _tabbedMdiManager = null;
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
