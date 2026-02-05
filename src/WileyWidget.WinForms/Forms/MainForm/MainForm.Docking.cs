using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private DockingManager? _dockingManager;
    private ActivityLogPanel? _activityLogPanel;
    private DockingLayoutManager? _dockingLayoutManager;
    private DockingManagerThemeAdapter? _dockingManagerThemeAdapter;
    private Panel? _leftDockPanel;
#pragma warning disable CS0649
    private Panel? _centralDocumentPanel;
#pragma warning restore CS0649
    private Panel? _rightDockPanel;
    private Dictionary<string, Control>? _dynamicDockPanels;

    [DllImport("user32.dll")]
    private static extern bool LockWindowUpdate(IntPtr hWndLock);

    // Layout state management
    private readonly DateTime _lastSaveTime = DateTime.MinValue;

    // Layout versioning
    private const string LayoutVersionAttributeName = "layoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const string SegoeUiFontName = "Segoe UI";

    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";

    // [PERF] Z-order debounce timer (consolidates redundant EnsureZOrder() calls)
    // Multiple dock state changes trigger Z-order adjustment via debounced 100ms timer
    private System.Windows.Forms.Timer? _zOrderDebounceTimer;

    private bool TryInitializeBasicDocking()
    {
        if (_dockingManager != null)
        {
            return true;
        }

        if (_serviceProvider == null)
        {
            _logger?.LogWarning("Basic DockingManager initialization skipped: ServiceProvider is null");
            return false;
        }

        try
        {
            var (dockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, _, layoutManager) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);

            _dockingManager = dockingManager;
            _leftDockPanel = leftPanel;
            _rightDockPanel = rightPanel;
            _centralDocumentPanel = centralPanel;
            _activityLogPanel = activityLogPanel;
            _dockingLayoutManager = layoutManager;
            _dynamicDockPanels ??= new Dictionary<string, Control>();

            if (_leftDockPanel != null)
            {
                _leftDockPanel.AccessibleName ??= "Left dock panel";
                _leftDockPanel.AccessibleDescription ??= "Left-side docking region";
            }

            if (_rightDockPanel != null)
            {
                _rightDockPanel.AccessibleName ??= "Right dock panel";
                _rightDockPanel.AccessibleDescription ??= "Right-side docking region";
            }

            if (_centralDocumentPanel != null)
            {
                _centralDocumentPanel.AccessibleName ??= "Document panel";
                _centralDocumentPanel.AccessibleDescription ??= "Main document workspace";
            }

            if (_activityLogPanel != null)
            {
                _activityLogPanel.AccessibleName ??= "Activity log panel";
                _activityLogPanel.AccessibleDescription ??= "Application activity log";
            }

            // ConfigureDockingManagerSettings() removed - was dead code never called from active code path

            var currentTheme = _themeService?.CurrentTheme ?? AppThemeColors.DefaultTheme;
            ThemeApplicationHelper.ApplyThemeToDockingManager(_dockingManager, currentTheme, _logger);

            if (_themeService != null && _dockingManagerThemeAdapter == null && _dockingManager != null)
            {
                _dockingManagerThemeAdapter = new DockingManagerThemeAdapter(_dockingManager, _logger);
                _dockingManagerThemeAdapter.RegisterThemeListener(_themeService);
            }

            var panelCount = (_dockingManager.Controls as System.Collections.ICollection)?.Count ?? 0;
            _logger?.LogDebug("Applying dock visibility/state for {Count} panels", panelCount);

            if (_leftDockPanel != null)
            {
                _leftDockPanel.Visible = true;
                try
                {
                    _dockingManager.SetDockVisibility(_leftDockPanel, true);
                    _logger?.LogDebug("SetDockVisibility succeeded for {PanelName}", _leftDockPanel.Name ?? "null");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "SetDockVisibility failed for {PanelName}",
                        _leftDockPanel.Name ?? "null");
                }
                _dockingManager.SetControlMinimumSize(_leftDockPanel, new Size(280, 360));
            }

            if (_centralDocumentPanel != null)
            {
                _centralDocumentPanel.Visible = true;
                _centralDocumentPanel.Dock = DockStyle.Fill;
                var hostControl = _dockingManager.HostControl as Control;
                if (hostControl != null && !ReferenceEquals(_centralDocumentPanel.Parent, hostControl))
                {
                    hostControl.Controls.Add(_centralDocumentPanel);
                }
                _logger?.LogDebug("Central panel set to Fill parent (no SetDockVisibility needed)");
            }

            if (_rightDockPanel != null)
            {
                _rightDockPanel.Visible = false;
                try
                {
                    _dockingManager.SetDockVisibility(_rightDockPanel, false);
                    _logger?.LogDebug("SetDockVisibility succeeded for {PanelName}", _rightDockPanel.Name ?? "null");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "SetDockVisibility failed for {PanelName}",
                        _rightDockPanel.Name ?? "null");
                }
                _dockingManager.SetControlMinimumSize(_rightDockPanel, new Size(300, 360));
            }

            if (_activityLogPanel != null)
            {
                _activityLogPanel.Visible = true;
            }

            if (_ribbon != null) _ribbon.BringToFront();
            if (_statusBar != null) _statusBar.BringToFront();

            Refresh();
            _logger?.LogInformation("Basic DockingManager initialization complete");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Basic DockingManager initialization failed");
            _asyncLogger?.Error($"Basic docking initialization failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Initializes Syncfusion DockingManager with layout management.
    /// Delegates to DockingHostFactory for centralized docking creation logic.
    /// Loads saved layout from AppData if available.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        if (TryInitializeBasicDocking())
        {
            return;
        }

        var globalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Syncfusion Docking Initialization");

        _logger?.LogInformation("InitializeSyncfusionDocking START - handleCreated={HandleCreated}, UIThread={ThreadId}",
            IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);
        _asyncLogger?.Information("InitializeSyncfusionDocking START - handleCreated={HandleCreated}, UIThread={ThreadId}", IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);

        try
        {

            // Phase: DockingManager Creation
            var dockingHostStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger?.LogInformation("Creating DockingHost via factory...");
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider cannot be null when creating DockingHost.");
            }
            var (dockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, activityTimer, layoutManager) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);
            dockingHostStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("DockingManager Creation", dockingHostStopwatch.ElapsedMilliseconds);

            _dockingManager = dockingManager;
            _leftDockPanel = leftPanel;
            _rightDockPanel = rightPanel;
            _centralDocumentPanel = centralPanel;
            _activityLogPanel = activityLogPanel;
            _dockingLayoutManager = layoutManager;
            _dynamicDockPanels ??= new Dictionary<string, Control>();
            _logger?.LogDebug("Docking panels assigned: Left={Left}, Right={Right}, Central={Central}, Activity={Activity}",
                _leftDockPanel?.Name, _rightDockPanel?.Name, _centralDocumentPanel?.Name, _activityLogPanel?.Name);

            // ConfigureDockingManagerSettings() removed - was dead code never called from active code path

            var currentTheme = _themeService?.CurrentTheme ?? AppThemeColors.DefaultTheme;
            ThemeApplicationHelper.ApplyThemeToDockingManager(_dockingManager, currentTheme, _logger);

            // Wire up cache invalidation for grid discovery
            // Note: ActiveControlChanged event not available on DockingManager
            // Grid cache invalidation handled via DockStateChanged event instead

            _logger?.LogInformation("DockingHost created: HasManager={HasManager}, HostControl={HostControl}",
                _dockingManager != null, _dockingManager?.HostControl?.Name ?? "<null>");

            // Ensure panels are visible via DockingManager API
            if (_dockingManager != null)
            {
                var panelCount = (_dockingManager.Controls as System.Collections.ICollection)?.Count ?? 0;
                _logger?.LogDebug("Applying dock visibility/state for {Count} panels", panelCount);
                if (_leftDockPanel != null)
                {
                    _leftDockPanel.Visible = true;
                    try
                    {
                        _dockingManager.SetDockVisibility(_leftDockPanel, true);
                        _logger?.LogDebug("SetDockVisibility succeeded for {PanelName}", _leftDockPanel.Name ?? "null");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "SetDockVisibility failed for {PanelName}",
                            _leftDockPanel.Name ?? "null");
                    }
                    _leftDockPanel.Refresh();
                    _dockingManager.SetControlMinimumSize(_leftDockPanel, new Size(300, 360));
                    _logger?.LogDebug("Left panel configured: Visible=true, MinSize=300x360");
                }

                if (_rightDockPanel != null)
                {
                    // Start hidden by default to avoid covering central UI until user opens it
                    _rightDockPanel.Visible = false;
                    try
                    {
                        _dockingManager.SetDockVisibility(_rightDockPanel, false);
                        _logger?.LogDebug("SetDockVisibility succeeded for {PanelName}", _rightDockPanel.Name ?? "null");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "SetDockVisibility failed for {PanelName}",
                            _rightDockPanel.Name ?? "null");
                    }
                    _dockingManager.SetControlMinimumSize(_rightDockPanel, new Size(300, 360));
                    _logger?.LogDebug("Right panel configured: Visible=false, MinSize=300x360");
                }
            }

            // 3. Layout recalc is deferred until form is fully shown

            // 4. Activity grid will be loaded with actual data after docking completes
            if (_activityLogPanel != null)
            {
                _activityLogPanel.Visible = true;
            }

            // Panel activation and layout recalc deferred until form is fully shown

            // Guard: Ensure at least DockingManager was created
            if (_dockingManager == null)
            {
                _asyncLogger?.Error("DockingManager creation failed");
                _logger?.LogError("DockingManager creation failed - docking will be unavailable");
                Console.WriteLine("[DIAGNOSTIC ERROR] DockingManager is null after CreateDockingHost");
            }
            else
            {
                // CRITICAL: Ensure ribbon and status bar are on top of docking panels after DockingManager creation
                // This prevents docking panels from covering the ribbon UI chrome
                if (_ribbon != null)
                {
                    try
                    {
                        _ribbon.BringToFront();
                        _logger?.LogDebug("Ribbon z-order corrected after DockingManager initialization");
                    }
                    catch (Exception zEx)
                    {
                        _logger?.LogDebug(zEx, "Failed to bring ribbon to front after docking initialization");
                    }
                }

                if (_statusBar != null)
                {
                    try
                    {
                        _statusBar.BringToFront();
                        _logger?.LogDebug("Status bar z-order corrected after DockingManager initialization");
                    }
                    catch (Exception zEx)
                    {
                        _logger?.LogDebug(zEx, "Failed to bring status bar to front after docking initialization");
                    }
                }

                // Ensure panel navigation is available before layout load so dynamic panels recreate with real controls
                EnsurePanelNavigatorInitialized();

                // Layout manager is now created by DockingHostFactory
                var layoutPath = GetDockingLayoutPath();
                _logger?.LogDebug("DockingLayoutManager provided by factory with path {LayoutPath}", layoutPath);

                // Transfer ownership of panels and fonts to the layout manager
                var dockAutoHideTabFont = new Font(SegoeUiFontName, 9F);
                var dockTabFont = new Font(SegoeUiFontName, 9F);

                // Note: DockingLayoutManager initialization deferred - methods will be available in future
                // For now, panels are managed directly by DockingManager
                _logger?.LogDebug("DockingLayoutManager from factory - initialization deferred");

                HideStandardPanelsForDocking();

                // CRITICAL FIX: Create and dock initial panels BEFORE suspending layout
                // This ensures DockingManager has controls in its collection before OnPaint events fire
                // Without this, ArgumentOutOfRangeException occurs in DockHost.GetPaintInfo()
                try
                {
                    // Panels are now created by DockingHostFactory - just ensure they're properly configured
                    var dynamicPanelCount = _dynamicDockPanels?.Count ?? 0;
                    var createdPanelCount = new[] { _leftDockPanel, _centralDocumentPanel, _rightDockPanel }.Count(p => p != null) + dynamicPanelCount;
                    _logger?.LogInformation("Docking panels created by factory — count={PanelCount}, dynamicPanels={DynamicCount}, layoutManagerReady={LayoutManagerReady}",
                        createdPanelCount, dynamicPanelCount, _dockingLayoutManager != null);
                }
                catch (Exception panelEx)
                {
                    _logger?.LogError(panelEx, "Failed to configure docking panels - paint exceptions may occur");
                }

                // Reduce flicker during layout load + theme application (best-effort).
                try
                {
                    try
                    {
                        _dockingManager.LockHostFormUpdate();
                        _dockingManager.LockDockPanelsUpdate();
                    }
                    catch (Exception lockEx)
                    {
                        _logger?.LogWarning(lockEx, "Failed to lock DockingManager updates - continuing without lock");
                    }

                    try
                    {
                        _dockingManager.SuspendLayout();
                    }
                    catch (Exception suspendEx)
                    {
                        _logger?.LogWarning(suspendEx, "Failed to suspend DockingManager layout - continuing");
                    }

                    // CRITICAL: Layout loading is now deferred to InitializeAsync() via LoadAndApplyDockingLayout()
                    // This ensures the form is fully rendered before we start heavy docking restoration.
                    _logger?.LogDebug("DockingManager infrastructure initialized. Layout restoration will follow in InitializeAsync().");

                    // Theme application deferred: Applied after DockingManager.ResumeLayout() to avoid conflicts

                    // CRITICAL: Apply SFSkinManager theme AFTER DockingManager is fully initialized and panels are docked
                    // This ensures theme cascade works correctly and prevents ArgumentOutOfRangeException in paint events
                    var themeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
                        SfSkinManager.SetVisualStyle(this, themeName);
                        themeStopwatch.Stop();
                        StartupInstrumentation.RecordPhaseTime("Theme Application", themeStopwatch.ElapsedMilliseconds);
                        _logger?.LogInformation("Applied SfSkinManager theme to MainForm after DockingManager setup: {Theme} ({Time}ms)", themeName, themeStopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception themeEx)
                    {
                        themeStopwatch.Stop();
                        _logger?.LogWarning(themeEx, "Failed to apply SFSkinManager theme to MainForm after DockingManager setup");
                    }

                    // Ensure panels start visible & activated
                    if (_dockingManager != null)
                    {
                        try
                        {
                            var controlsObj = _dockingManager.Controls;
                            if (controlsObj != null)
                            {
                                // Prefer ICollection for Count and safe enumeration
                                var coll = controlsObj as System.Collections.ICollection;
                                if (coll != null && coll.Count > 0)
                                {
                                    int floatingWindowCount = 0;
                                    foreach (var item in coll)
                                    {
                                        if (item is Control control && !control.IsDisposed)
                                        {
                                            try
                                            {
                                                // Verify control has valid handle before invalidating
                                                if (control.IsHandleCreated)
                                                {
                                                    // Refresh floating window by invalidating to trigger repaint
                                                    // This ensures Z-order is respected and no overlap occurs
                                                    control.Invalidate(true);
                                                    floatingWindowCount++;
                                                }
                                            }
                                            catch (ArgumentOutOfRangeException aorEx)
                                            {
                                                _logger?.LogDebug(aorEx, "ArgumentOutOfRangeException refreshing window: {WindowName} - window may not be fully initialized", control.Name);
                                            }
                                            catch (Exception windowEx)
                                            {
                                                _logger?.LogDebug(windowEx, "Failed to refresh window: {WindowName}", control.Name);
                                            }
                                        }
                                    }

                                    if (floatingWindowCount > 0)
                                    {
                                        _logger?.LogDebug("Float window Z-order refreshed ({Count} windows)", floatingWindowCount);
                                    }
                                }
                                else
                                {
                                    _logger?.LogWarning("No controls registered in DockingManager – skipping GetControls operations");
                                }
                            }
                        }
                        catch (ArgumentOutOfRangeException aorEx)
                        {
                            _logger?.LogWarning(aorEx, "ArgumentOutOfRangeException during DockingManager control enumeration - docking system may not be fully initialized yet");
                        }
                        catch (Exception enumEx)
                        {
                            _logger?.LogWarning(enumEx, "Exception during DockingManager control enumeration");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Exception during docking updates phase");
                }

                _logger?.LogInformation(
                    "InitializeSyncfusionDocking complete - WileyWidget.WinForms.Controls.Panels.ActivityLogPanel={HasActivityPanel}",
                    _activityLogPanel != null);

                // FINAL Z-order correction: Ensure ribbon stays on top of all docking content
                if (_ribbon != null)
                {
                    try
                    {
                        _ribbon.BringToFront();
                        _logger?.LogDebug("Ribbon z-order finalized before layout resume");
                    }
                    catch (Exception zEx)
                    {
                        _logger?.LogDebug(zEx, "Failed to finalize ribbon z-order");
                    }
                }

                globalStopwatch.Stop();
                StartupInstrumentation.RecordPhaseTime("Total DockingManager Initialization", globalStopwatch.ElapsedMilliseconds);
                StartupInstrumentation.LogInitializationState(_logger);
                Console.WriteLine(StartupInstrumentation.GetFormattedMetrics());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Syncfusion DockingManager: {Message}", ex.Message);
            _asyncLogger?.Error($"Initialization Error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ConfigureDockingManagerChromeLayout()
    {
        _logger?.LogInformation("ConfigureDockingManagerChromeLayout: ENTERED");
        if (_dockingManager == null)
        {
            _logger?.LogWarning("ConfigureDockingManagerChromeLayout skipped: DockingManager is null");
            return;
        }

        var hostControl = _dockingManager.HostControl as Control;
        if (hostControl == null)
        {
            _logger?.LogWarning("ConfigureDockingManagerChromeLayout skipped: HostControl not available");
            return;
        }

        var panelCount = (_dockingManager.Controls as System.Collections.ICollection)?.Count ?? 0;
        _logger?.LogDebug("Applying dock visibility/state for {Count} panels", panelCount);

        if (_centralDocumentPanel != null)
        {
            _centralDocumentPanel.Dock = DockStyle.Fill;
            if (!ReferenceEquals(_centralDocumentPanel.Parent, hostControl))
            {
                hostControl.Controls.Add(_centralDocumentPanel);
            }
            _logger?.LogDebug("Central panel set to Fill parent (no DockControl needed)");
        }

        SuspendLayout();
        _logger?.LogInformation("ConfigureDockingManagerChromeLayout: Starting layout configuration");
        try
        {
            if (ReferenceEquals(hostControl, this))
            {
                UpdateChromePadding();
            }
            else
            {
                if (hostControl.Parent == this)
                {
                    hostControl.BringToFront();
                }

                AdjustDockingHostBounds(hostControl);
                hostControl.SendToBack();
            }

            if (_statusBar != null)
            {
                _statusBar.BringToFront();
            }

            if (_navigationStrip != null)
            {
                _navigationStrip.BringToFront();
            }

            if (_ribbon != null)
            {
                _ribbon.BringToFront();
            }

            if (_menuStrip != null)
            {
                _menuStrip.BringToFront();
            }

            TrySetDockingManagerBoolProperty(_dockingManager, "MDIEnabled", false);
            TrySetDockingManagerBoolProperty(_dockingManager, "CloseButton", false);
        }
        finally
        {
            ResumeLayout(false);
        }

        LogDockingManagerMetrics(_dockingManager, hostControl);
    }

    private void AdjustDockingHostBounds(Control? hostControlOverride = null)
    {
        UpdateChromePadding();

        var hostControl = hostControlOverride ?? _dockingManager?.HostControl as Control;
        if (hostControl == null || hostControl.IsDisposed) return;
        if (ReferenceEquals(hostControl, this)) return;

        var top = 0;
        if (_menuStrip != null)
        {
            top = Math.Max(top, _menuStrip.Bottom);
        }

        if (_ribbon != null)
        {
            top = Math.Max(top, _ribbon.Bottom);
        }

        var bottom = ClientSize.Height;
        if (_statusBar != null)
        {
            bottom = Math.Min(bottom, _statusBar.Top);
        }

        var height = Math.Max(0, bottom - top);

        hostControl.SuspendLayout();
        try
        {
            hostControl.Dock = DockStyle.None;
            hostControl.Location = new Point(0, top);
            hostControl.Size = new Size(ClientSize.Width, height);
            hostControl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }
        finally
        {
            hostControl.ResumeLayout(true);
        }
    }

    private void TrySetDockingManagerBoolProperty(DockingManager dockingManager, string propertyName, bool value)
    {
        try
        {
            var property = dockingManager.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(dockingManager, value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to set DockingManager property {PropertyName}", propertyName);
        }
    }

    private void LogDockingManagerMetrics(DockingManager dockingManager, Control hostControl)
    {
        try
        {
            _logger?.LogInformation("DockingManager host bounds: {Bounds}, client: {Client}", hostControl.Bounds, hostControl.ClientRectangle);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to log DockingManager metrics");
        }
    }

    /// <summary>
    /// Handles theme changes at runtime and reapplies theme to all form and docking panels.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// Uses ApplyThemeRecursive for comprehensive coverage of all controls including dynamically created panels.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="theme">New theme to apply.</param>
    private void OnThemeChanged(object? sender, string theme)
    {
        if (!IsHandleCreated)
            return;

        if (IsDisposed)
        {
            _logger?.LogDebug("OnThemeChanged called on disposed form - ignoring");
            return;
        }

        // Validate theme name before applying
        theme = AppThemeColors.ValidateTheme(theme, _logger);

        if (InvokeRequired)
        {
            try
            {
                // Prefer synchronous invoke so theme changes are applied before returning to caller
                Invoke(new System.Action(() => OnThemeChanged(sender, theme)));
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogDebug(invEx, "Invoke failed - handle may be destroyed or not created");
                return;
            }
            catch (Exception ex)
            {
                try
                {
                    // Fallback to asynchronous marshal if synchronous invoke fails
                    BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
                }
                catch (Exception inner)
                {
                    _logger?.LogDebug(inner, "Failed to marshal OnThemeChanged to UI thread (both Invoke and BeginInvoke failed)");
                }
                _logger?.LogDebug(ex, "Invoke failed when marshaling OnThemeChanged to UI thread");
            }
            return;
        }

        try
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp"));
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp", "theme-log.txt"), $"OnThemeChanged invoked: {theme}\n");
            }
            catch { }
            _logger?.LogInformation("Applying theme change to all form controls: {Theme}", theme);

            // Apply theme recursively to entire control tree (form and all children)
            ApplyThemeRecursive(this, theme);

            // Update button text in Ribbon if present
            if (_ribbon != null)
            {
                var themeToggleBtn = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggleBtn == null)
                {
                    // Fallback: search whole form if ribbon-scoped lookup fails
                    try { themeToggleBtn = FindToolStripItem(this, "ThemeToggle") as ToolStripButton; } catch { }
                }

                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp", "theme-log.txt"), $"Updated ribbon themeToggle text to: {themeToggleBtn.Text}\n"); } catch { }
                }
                else
                {
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp", "theme-log.txt"), "ribbon themeToggle not found\n"); } catch { }
                }
            }

            // Update button text in NavigationStrip if present
            if (_navigationStrip != null)
            {
                var themeToggleBtn = _navigationStrip.Items.Find("ThemeToggle", true).FirstOrDefault() as ToolStripButton;
                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                }
                // Update ToolStripEx theme to match application theme
                _navigationStrip.ThemeName = theme;
            }

            this.Refresh();
            _logger?.LogInformation("Theme successfully applied to all controls: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme change: {Theme}", theme);
        }
    }

    // REMOVED: Dead code - InitializeDockingManager() and ConfigureDockingManagerSettings() were never called
    // DockingManager is now created exclusively through DockingHostFactory.CreateDockingHost()
    // See MainForm.OnShown() → InitializeSyncfusionDocking() for the active creation path

    private async Task LoadAndApplyDockingLayout(string layoutPath, CancellationToken cancellationToken = default)
    {
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingManager is null");
            return;
        }

        if (_dockingLayoutManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingLayoutManager is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            _logger?.LogWarning("Docking layout path is empty - using default layout");
            return;
        }

        // Ensure layout directory exists before attempting load
        try
        {
            var dir = Path.GetDirectoryName(layoutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger?.LogInformation("Created docking layout directory: {Directory}", dir);
            }
        }
        catch (Exception dirEx)
        {
            _logger?.LogWarning(dirEx, "Failed to create layout directory - layout may not persist");
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Docking Layout Restoration");

        try
        {
            _asyncLogger?.Information("LoadAndApplyDockingLayout start - path={Path}", layoutPath);
            LoadDynamicPanels(layoutPath);

            // Minimize flicker by locking OS-level painting and batching DockingManager updates
            try
            {
                try { LockWindowUpdate(this.Handle); }
                catch (Exception lockWinEx) { _logger?.LogWarning(lockWinEx, "LockWindowUpdate failed - continuing without OS-level lock"); }

                bool panelsLocked = false;
                try
                {
                    try { _dockingManager?.LockDockPanelsUpdate(); panelsLocked = true; }
                    catch (Exception lockEx) { _logger?.LogWarning(lockEx, "Failed to lock DockingManager panel updates - continuing"); }

                    await _dockingLayoutManager.LoadDockingLayoutAsync(_dockingManager!, cancellationToken).ConfigureAwait(true);

                    _asyncLogger?.Information("LoadAndApplyDockingLayout: LoadDockingLayoutAsync completed");

                    try
                    {
                        var rightPanel = GetRightDockPanel();
                        var leftPanel = _leftDockPanel;
                        var centralPanel = _centralDocumentPanel;

                        if (rightPanel != null)
                        {
                            RightDockPanelFactory.SwitchRightPanelContent(rightPanel, RightDockPanelFactory.RightPanelMode.ActivityLog, _logger);
                        }

                        if (_dockingManager != null)
                        {
                            var panelCount = (_dockingManager.Controls as System.Collections.ICollection)?.Count ?? 0;
                            _logger?.LogDebug("Applying dock visibility/state for {Count} panels", panelCount);
                            if (leftPanel != null)
                            {
                                _dockingManager.SetDockVisibility(leftPanel, true);
                                leftPanel.Refresh();
                            }

                            if (centralPanel != null)
                            {
                                centralPanel.Visible = true;
                                centralPanel.Dock = DockStyle.Fill;
                                var hostControl = _dockingManager.HostControl as Control;
                                if (hostControl != null && !ReferenceEquals(centralPanel.Parent, hostControl))
                                {
                                    hostControl.Controls.Add(centralPanel);
                                }
                                centralPanel.Refresh();
                                _logger?.LogDebug("Central panel set to Fill parent (no SetDockVisibility needed)");
                            }

                            if (rightPanel != null)
                            {
                                _dockingManager.SetDockVisibility(rightPanel, true);
                                rightPanel.Refresh();
                            }
                        }

                        ActivateDockingControl(centralPanel);

                        var requiresReset = (leftPanel != null && !leftPanel.Visible)
                            || (centralPanel != null && !centralPanel.Visible)
                            || (rightPanel != null && !rightPanel.Visible);

                        if (requiresReset)
                        {
                            _logger?.LogWarning("Docking layout restore left panels hidden; falling back to defaults");
                            ResetToDefaultLayout();
                        }

                        RefreshFormLayout();
                    }
                    catch (Exception rpEx)
                    {
                        _logger?.LogDebug(rpEx, "Failed to finalize docking layout restore state");
                    }
                }
                finally
                {
                    if (panelsLocked)
                    {
                        try { _dockingManager?.UnlockDockPanelsUpdate(); }
                        catch (Exception unlockEx) { _logger?.LogWarning(unlockEx, "Failed to unlock DockingManager panel updates"); }
                    }
                }
            }
            finally
            {
                try { LockWindowUpdate(IntPtr.Zero); }
                catch (Exception unlockWinEx) { _logger?.LogWarning(unlockWinEx, "LockWindowUpdate(IntPtr.Zero) failed"); }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Docking layout restoration was cancelled");
            _asyncLogger?.Information("Docking layout restoration was cancelled");
        }
        catch (IOException ioEx)
        {
            _logger?.LogWarning(ioEx, "IO error during layout restoration - file may be locked or corrupted, using default layout");
            _asyncLogger?.Warning(ioEx, "IO error during layout restoration - using default layout");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during layout restoration - using default layout");
            _asyncLogger?.Error(ex, "Unexpected error during layout restoration - using default layout");
        }
    }

    private void ActivateDockingControl(Control? control)
    {
        if (control == null || _dockingManager == null)
        {
            return;
        }

        try
        {
            var method = _dockingManager.GetType().GetMethod("ActivateControl", new[] { typeof(Control) });
            if (method != null)
            {
                method.Invoke(_dockingManager, new object[] { control });
                return;
            }

            if (control.CanSelect)
            {
                control.Select();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to activate docking control {ControlName}", control.Name);
        }
    }

    private static void TryDeleteLayoutFiles(string? layoutPath)
    {
        if (string.IsNullOrWhiteSpace(layoutPath)) return;
        try { if (File.Exists(layoutPath)) File.Delete(layoutPath); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to delete layout file: {ex.Message}"); }
        TryCleanupTempFile(layoutPath + ".tmp");
    }

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
            try { return Path.Combine(Path.GetTempPath(), DockingLayoutFileName); } catch { return DockingLayoutFileName; }
        }
    }

    private Task SafeInvokeAsync(System.Action action, CancellationToken cancellationToken = default)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (!this.IsHandleCreated)
        {
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
                try
                {
                    Task.Run(() =>
                    {
                        try { action(); tcs.SetResult(null); }
                        catch (Exception innerEx) { tcs.SetException(innerEx); }
                    });
                }
                catch (Exception fallbackEx) { tcs.SetException(fallbackEx); }
            }
            return tcs.Task;
        }

        try { action(); return Task.CompletedTask; }
        catch (Exception ex) { return Task.FromException(ex); }
    }

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        if (e.Controls == null) return;

        foreach (Control control in e.Controls)
        {
            if (control == null) continue;
            _logger?.LogDebug("Dock state changed: Control={Control}, NewState={NewState}, OldState={OldState}", control.Name, e.NewState, e.OldState);
            _asyncLogger?.Verbose("DockStateChanged: Control={Control}, NewState={NewState}, OldState={OldState}", control.Name, e.NewState, e.OldState);
            HandleDockingChange(control);
        }
    }

    /// <summary>
    /// Basic docking change handler for visibility updates only.
    /// </summary>
    /// <param name="control">The control whose docking state changed (may be null if change is global)</param>
    private void HandleDockingChange(Control? control)
    {
        if (!_uiConfig.UseSyncfusionDocking || _dockingManager == null)
        {
            return;
        }

        try
        {
            if (control != null)
            {
                BeginInvoke(new Func<Task>(async () => await NotifyPanelVisibilityChangedAsync(control)));
            }

            UpdateDockingStateText();

            _logger?.LogDebug("Docking change handled: {Control}", control?.Name ?? "<global>");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error in consolidated docking change handler");
        }
    }

    /// <summary>
    /// Schedules a debounced Z-order adjustment to consolidate redundant calls.
    /// Multiple rapid dock state changes are coalesced into a single Z-order fix.
    /// Uses 100ms debounce timer to avoid performance overhead from excessive re-layout.
    /// </summary>
    private void ScheduleZOrderDebounce()
    {
        try
        {
            if (_zOrderDebounceTimer == null)
            {
                _zOrderDebounceTimer = new System.Windows.Forms.Timer
                {
                    Interval = 100  // 100ms debounce
                };
                _zOrderDebounceTimer.Tick += (_, _) => DebouncedZOrderAdjustment();
            }

            // Restart timer - if already running, this extends the delay
            // Multiple calls within 100ms result in a single Z-order adjustment
            _zOrderDebounceTimer.Stop();
            _zOrderDebounceTimer.Start();
            _logger?.LogDebug("Z-order adjustment scheduled (100ms debounce)");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to schedule debounced Z-order adjustment");
        }
    }

    /// <summary>
    /// Executes debounced Z-order adjustment after layout changes.
    /// Consolidates redundant EnsureZOrder() calls from multiple events into a single operation.
    /// Prevents performance overhead while ensuring proper panel stacking on every state change.
    /// </summary>
    private void DebouncedZOrderAdjustment()
    {
        try
        {
            // Stop timer first to prevent re-entry
            if (_zOrderDebounceTimer?.Enabled ?? false)
            {
                _zOrderDebounceTimer.Stop();
            }

            // Perform consolidated Z-order adjustment
            EnsureDockingZOrder(validateHosting: false);
            _logger?.LogDebug("Debounced Z-order adjustment completed");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to adjust Z-order in debounced handler");
        }
    }

    private void EnsureSidePanelsZOrder()
    {
        if (_leftDockPanel != null)
        {
            try { _leftDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set left dock panel z-order"); }
        }

        if (_rightDockPanel != null)
        {
            try { _rightDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set right dock panel z-order"); }
        }
    }

    /// <summary>
    /// Ensures floating windows maintain proper Z-order (stacking order) after docking changes.
    /// Refreshes floating pane controls to prevent overlap issues.
    /// Safe to call after dock state changes or when floating windows need to be re-rendered.
    /// Handles null docking manager gracefully without exception propagation.
    /// </summary>
    private void EnsureFloatWindowZOrder()
    {
        if (_dockingManager == null)
        {
            _logger?.LogDebug("Cannot ensure float window Z-order: DockingManager is null");
            return;
        }

        try
        {
            // Refresh floating windows by iterating through docking manager controls
            // and ensuring panes are properly positioned (Syncfusion's internal API)
            var controlsObj = _dockingManager.Controls;
            if (controlsObj == null)
            {
                return;  // No controls to adjust
            }

            // Cast to ICollection for enumeration
            if (!(controlsObj is System.Collections.ICollection coll))
            {
                return;
            }
            if (coll.Count == 0)
            {
                _logger?.LogWarning("No controls registered in DockingManager – skipping GetControls operations");
                return;
            }

            int floatingWindowCount = 0;
            foreach (var item in coll)
            {
                if (item is not Control control || control.IsDisposed) continue;

                try
                {
                    // Refresh floating window by invalidating to trigger repaint
                    // This ensures Z-order is respected and no overlap occurs
                    control.Invalidate(true);
                    floatingWindowCount++;
                }
                catch (Exception windowEx)
                {
                    _logger?.LogDebug(windowEx, "Failed to refresh window: {WindowName}", control.Name);
                }
            }

            if (floatingWindowCount > 0)
            {
                _logger?.LogDebug("Float window Z-order refreshed ({Count} windows)", floatingWindowCount);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure floating window Z-order");
        }
    }

    private void RefreshFormLayout()
    {
        if (_dockingManager == null) return;
        this.Refresh();
        this.Invalidate();
    }

    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot add dynamic dock panel - DockingManager not initialized");
            return false;
        }

        LegacyGradientPanel? panel = null;
        try
        {
            panel = new LegacyGradientPanel
            {
                Name = panelName,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(panel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            _dockingManager.SetEnableDocking(panel, true);

            if (dockStyle == DockingStyle.Left || dockStyle == DockingStyle.Right)
            {
                _dockingManager.DockControl(panel, this, dockStyle, width);
            }
            else
            {
                _dockingManager.DockControl(panel, this, dockStyle, height);
            }

            _dockingManager.SetAutoHideMode(panel, false);
            _dockingManager.SetDockLabel(panel, displayLabel);
            _dockingManager.SetAllowFloating(panel, true);
            _logger?.LogDebug("Dynamic panel '{PanelName}' configured with auto-hide; floating mode available via UI", panelName);

            panel = null;

            _logger?.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
        finally
        {
            panel?.Dispose();
        }
    }

    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return false;
        _logger?.LogDebug("RemoveDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return false;
    }

    public Control? GetDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return null;
        _logger?.LogDebug("GetDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return null;
    }

    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return new List<string>().AsReadOnly();
    }

    private void ResetToDefaultLayout()
    {
        _logger?.LogInformation("Resetting to default layout");
        // Ensure all panels are visible
        if (_dockingManager != null)
        {
            if (_dockingManager.Controls is System.Collections.ICollection coll && coll.Count == 0)
            {
                _logger?.LogWarning("No controls registered in DockingManager – skipping ResetToDefaultLayout operations");
                return;
            }
            var controlsObj = _dockingManager.Controls;
            if (controlsObj is System.Collections.ICollection coll2 && coll2.Count > 0)
            {
                foreach (var item in coll2)
                {
                    if (item is Control control)
                    {
                        if (ReferenceEquals(control, _centralDocumentPanel) || ReferenceEquals(control, _dockingManager.HostControl))
                        {
                            _logger?.LogDebug("ResetToDefaultLayout: Skipping dock visibility for central/host control {PanelName}", control.Name ?? "null");
                            continue;
                        }

                        _logger?.LogDebug("ResetToDefaultLayout: Applying dock visibility for {PanelName}", control.Name ?? "null");
                        _dockingManager.SetDockVisibility(control, true);
                        control.Refresh();
                    }
                }
            }
        }
    }

    private void DisposeSyncfusionDockingResources()
    {
        _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - delegating to DockingLayoutManager");

        // [PERF] Dispose Z-order debounce timer
        if (_zOrderDebounceTimer != null)
        {
            try
            {
                _zOrderDebounceTimer.Stop();
                _zOrderDebounceTimer.Dispose();
                _zOrderDebounceTimer = null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to dispose Z-order debounce timer");
            }
        }

        if (_dockingLayoutManager != null)
        {
            try
            {
                if (_dockingManager != null && this.IsHandleCreated)
                {
                    try { _logger?.LogDebug("Form disposal: layout persistence delegated to DockingLayoutManager"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to finalize layout persistence during disposal (non-critical)"); }
                }

                if (_dockingManager != null)
                {
                    try { _logger?.LogDebug("Form disposal: DockingLayoutManager event cleanup in progress"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Error during event unsubscription (non-critical)"); }
                }

                _dockingLayoutManager.Dispose();
                _dockingLayoutManager = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose DockingLayoutManager");
            }
        }

        if (_dockingManager != null)
        {
            var mgr = _dockingManager;
            _dockingManager = null;
            mgr.DisposeSafely();
        }

        _logger?.LogDebug("DisposeSyncfusionDockingResources completed - all resources delegated to DockingLayoutManager");
    }

    private void UpdateDockingStateText()
    {
        try
        {
            if (!IsHandleCreated) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new System.Action(UpdateDockingStateText)); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to marshal UpdateDockingStateText to UI thread"); }
                return;
            }

            if (_statePanel == null || _statePanel.IsDisposed) return;

            var stateInfo = new System.Text.StringBuilder();
            var controls = _dockingManager?.Controls as Control.ControlCollection;
            var childCount = controls?.Count ?? 0;
            stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Panels: {childCount} panel{(childCount != 1 ? "s" : "")}");

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);
            _logger?.LogDebug("UpdateDockingStateText: DockingManager control count = {ControlCount}, MainForm control count = {FormControlCount}", childCount, this.Controls.Count);
            Console.WriteLine($"[DIAGNOSTIC] UpdateDockingStateText: DockingManager controls={childCount}, MainForm controls={this.Controls.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    private (Panel Panel, Label TitleLabel, Label DescLabel) CreateDashboardCard(string title, string description)
    {
        var cardPanel = new Panel
        {
            Size = new Size(200, 100),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Cursor = Cursors.Hand,
            AccessibleName = $"{title} card",
            AccessibleDescription = description,
            AccessibleRole = AccessibleRole.PushButton,
            TabStop = true,
            TabIndex = 0
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font(SegoeUiFontName, 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10),
            TabStop = false
        };

        var descLabel = new Label
        {
            Text = description,
            Font = new Font(SegoeUiFontName, 9F),
            AutoSize = true,
            Location = new Point(10, 40),
            TabStop = false
        };

        cardPanel.Controls.Add(titleLabel);
        cardPanel.Controls.Add(descLabel);

        return (cardPanel, titleLabel, descLabel);
    }

    private void SetupCardClickHandler(Panel card, System.Action action)
    {
        card.Click += (s, e) => action?.Invoke();
        card.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                action?.Invoke();
                e.Handled = true;
            }
        };
        foreach (Control child in card.Controls)
        {
            child.Click += (s, e) => action?.Invoke();
        }
    }

    private void ApplyThemeToDockingPanels()
    {
        if (_dockingManager == null) return;
        try { _logger?.LogDebug("Theme cascade applied via SfSkinManager to docking panels"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to apply theme to docking panels"); }
    }

    private void LoadDynamicPanels(string? layoutPath = null)
    {
        try { _logger?.LogDebug("Dynamic panels load requested: {LayoutPath}", layoutPath ?? "<default>"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to load dynamic panels"); }
    }

    private static void TryCleanupTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"Cleaned up temporary file: {filePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup temporary file: {filePath}, Error: {ex.Message}");
        }
    }

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
                    _logger?.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
                }
            }
        }
        _logger?.LogDebug("Standard panels hidden for Syncfusion docking");
    }

    /// <summary>
    /// Ensures proper Z-order (stacking order) for docked panels and validates their hosting state.
    /// Validates that docked panels are visible and properly hosted in the DockingManager.
    /// Corrects Z-order issues by calling BringToFront on panels with incorrect stacking.
    /// Logs diagnostic information if validation fails.
    /// </summary>
    /// <param name="validateHosting">If true, validates panel hosting state and corrects visibility/z-order issues. Default: true.</param>
    private void EnsureDockingZOrder(bool validateHosting = true)
    {
        if (_dockingManager == null)
        {
            _logger?.LogDebug("Cannot ensure Z-order: DockingManager is null");
            return;
        }

        try
        {
            // First, apply standard z-order enforcement from DockingManager
            _dockingManager.EnsureZOrder();
            _logger?.LogDebug("Standard Z-order enforcement applied via DockingManager.EnsureZOrder()");

            // If validation is enabled, check panel hosting state
            if (validateHosting)
            {
                ValidatePanelHostingState();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure docking Z-order");
        }
    }

    /// <summary>
    /// Validates that all docked panels are properly hosted, visible, and have correct Z-order.
    /// Iterates through DockingManager-controlled panels and:
    /// - Checks if panel is docked (GetEnableDocking returns true)
    /// - Verifies panel.Visible is true
    /// - Calls BringToFront() on panels with incorrect Z-order
    /// Logs warnings if validation issues are detected.
    /// </summary>
    private void ValidatePanelHostingState()
    {
        if (_dockingManager == null) return;

        try
        {
            var controlsObj = _dockingManager.Controls;
            if (controlsObj == null || !(controlsObj is System.Collections.ICollection coll))
            {
                return;
            }
            if (coll.Count == 0)
            {
                _logger?.LogWarning("No controls registered in DockingManager – skipping ValidatePanelHostingState operations");
                return;
            }

            int validPanels = 0;
            int hiddenPanels = 0;
            int correctedZOrder = 0;

            foreach (var item in coll)
            {
                if (item is not Control panel || panel == null || panel.IsDisposed)
                {
                    continue;
                }

                try
                {
                    // Check if panel is docked (enabled in DockingManager)
                    bool isDocked = _dockingManager.GetEnableDocking(panel);
                    bool isVisible = panel.Visible;
                    string panelName = !string.IsNullOrEmpty(panel.Name) ? panel.Name : panel.GetType().Name;

                    if (!isDocked)
                    {
                        _logger?.LogDebug("Panel '{PanelName}' is not docked (GetEnableDocking=false)", panelName);
                        continue;
                    }

                    // Check visibility and correct if needed
                    if (!isVisible)
                    {
                        _logger?.LogWarning("Docked panel '{PanelName}' is not visible - setting Visible=true", panelName);
                        panel.Visible = true;
                        hiddenPanels++;
                    }

                    // Check Z-order by attempting to bring to front if needed
                    // This is a heuristic: if the panel isn't at the top, correct it
                    // We use Parent?.Controls.GetChildIndex() to determine stacking order
                    if (panel.Parent != null && panel.Parent.Controls.Count > 0)
                    {
                        int childIndex = panel.Parent.Controls.GetChildIndex(panel, false);
                        int expectedIndex = panel.Parent.Controls.Count - 1; // Top should be at last index

                        if (childIndex < expectedIndex && !panel.IsDisposed)
                        {
                            try
                            {
                                panel.BringToFront();
                                _logger?.LogDebug("Z-order corrected for panel '{PanelName}' (was at index {OldIndex}, now at top)", panelName, childIndex);
                                correctedZOrder++;
                            }
                            catch (Exception bringEx)
                            {
                                _logger?.LogDebug(bringEx, "Failed to bring panel '{PanelName}' to front", panelName);
                            }
                        }
                    }

                    validPanels++;
                }
                catch (Exception panelEx)
                {
                    _logger?.LogDebug(panelEx, "Error validating hosting state for panel");
                }
            }

            // Log summary of validation
            if (validPanels > 0 || hiddenPanels > 0 || correctedZOrder > 0)
            {
                _logger?.LogDebug("Panel hosting validation complete: {ValidPanels} valid, {HiddenPanels} corrected visibility, {CorrectedZOrder} corrected Z-order",
                    validPanels, hiddenPanels, correctedZOrder);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate panel hosting state");
        }
    }

    private void EnsureDockingZOrder()
    {
        EnsureDockingZOrder(validateHosting: true);
    }

    /// <summary>
    /// Gets the right dock panel for programmatic access to its tabs and controls.
    /// </summary>
    /// <returns>The right dock panel (LegacyGradientPanel) containing Activity Log and JARVIS Chat tabs, or null if not initialized</returns>
    public LegacyGradientPanel? GetRightDockPanel()
    {
        if (_rightDockPanel != null && !_rightDockPanel.IsDisposed)
        {
            return _rightDockPanel as LegacyGradientPanel;
        }
        _logger?.LogDebug("GetRightDockPanel: Right dock panel is null or disposed");
        return null;
    }

    /// <summary>
    /// Switches the active tab in the right dock panel between Activity Log and JARVIS Chat.
    /// Ensures the right panel is visible and docked before switching tabs.
    /// </summary>
    /// <param name="tabName">The name of the tab to switch to (e.g., "JarvisChat", "ActivityLog")</param>
    public void SwitchRightPanel(string tabName)
    {
        try
        {
            _logger?.LogInformation("[SWITCH_RIGHT_PANEL] User requested {TabName}", tabName);

            // Map friendly names to actual tab names
            string actualTabName = tabName switch
            {
                "JarvisChat" => "JARVISChatTab",
                "ActivityLog" => "ActivityLogTab",
                _ => tabName
            };

            var rightPanel = GetRightDockPanel();
            if (rightPanel == null)
            {
                _logger?.LogWarning("[SWITCH_RIGHT_PANEL] Right dock panel is null or disposed - cannot switch");
                MessageBox.Show(
                    "The right panel is not yet initialized. Please try again in a moment.",
                    "Panel Not Ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (rightPanel.IsDisposed)
            {
                _logger?.LogWarning("[SWITCH_RIGHT_PANEL] Right dock panel has been disposed - cannot switch");
                return;
            }

            // Ensure the right panel is visible before switching
            if (!rightPanel.Visible)
            {
                rightPanel.Visible = true;
                _logger?.LogDebug("[SWITCH_RIGHT_PANEL] Made right panel visible");
            }

            // Ensure right panel is docked and in proper position
            if (_dockingManager != null)
            {
                try
                {
                    // Defensive check: Verify panel is actually managed by DockingManager
                    var isDocked = _dockingManager.GetDockVisibility(rightPanel);
                    _logger?.LogDebug("[SWITCH_RIGHT_PANEL] Right panel current dock visibility: {IsDocked}", isDocked);

                    if (!isDocked)
                    {
                        _logger?.LogWarning("[SWITCH_RIGHT_PANEL] Right panel is not docked - attempting to re-dock");
                        _dockingManager.SetDockVisibility(rightPanel, true);
                    }

                    _logger?.LogDebug("SetDockVisibility succeeded for {PanelName}", rightPanel.Name ?? "null");
                    rightPanel.Refresh();
                    _logger?.LogDebug("[SWITCH_RIGHT_PANEL] Ensured right panel is docked");

                    // Activate the control to bring it to front
                    _dockingManager.ActivateControl(rightPanel);
                    _logger?.LogDebug("ActivateControl succeeded for {PanelName}", rightPanel.Name ?? "null");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[SWITCH_RIGHT_PANEL] SetDockVisibility/ActivateControl failed for {PanelName}. " +
                        "IsDisposed={IsDisposed}, Parent={Parent}",
                        rightPanel.Name ?? "null", rightPanel.IsDisposed, rightPanel.Parent?.Name ?? "null");
                    // Don't show MessageBox - just log error and try to continue with tab switch
                }
            }
            else
            {
                _logger?.LogWarning("[SWITCH_RIGHT_PANEL] DockingManager is null - cannot ensure proper docking");
                // Don't block user - panel might still work without docking manager activation
            }

            // Now switch the tab content
            _logger?.LogInformation("[SWITCH_RIGHT_PANEL] Switching to tab {ActualTabName}", actualTabName);

            // Try both TabControlAdv and standard TabControl
            var tabControlAdv = rightPanel.Controls.OfType<TabControlAdv>().FirstOrDefault();
            var tabControl = rightPanel.Controls.OfType<TabControl>().FirstOrDefault();

            if (tabControlAdv == null && tabControl == null)
            {
                _logger?.LogError("[SWITCH_RIGHT_PANEL] No TabControl found in right panel. Available controls: {Controls}",
                    string.Join(", ", rightPanel.Controls.Cast<Control>().Select(c => $"{c.GetType().Name}:{c.Name}")));
                MessageBox.Show(
                    "The right panel tab control was not initialized properly. Please restart the application.",
                    "Tab Control Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Find the requested tab by name using pattern matching (prefer standard TabControl)
            object? foundTab = null;
            int selectedIndex = -1;
            int totalCount = 0;

            if (tabControl is not null)
            {
                foreach (TabPage page in tabControl.TabPages)
                {
                    if (page.Name == actualTabName)
                    {
                        foundTab = page;
                        break;
                    }
                }

                if (foundTab is not null)
                {
                    tabControl.SelectedTab = (TabPage)foundTab;
                    selectedIndex = tabControl.SelectedIndex;
                    totalCount = tabControl.TabPages.Count;
                }
            }
            else if (tabControlAdv is not null)
            {
                foreach (var page in tabControlAdv.TabPages)
                {
                    if (page is TabPageAdv advPage && advPage.Name == actualTabName)
                    {
                        foundTab = advPage;
                        break;
                    }
                }

                if (foundTab is not null)
                {
                    tabControlAdv.SelectedTab = (TabPageAdv)foundTab;
                    selectedIndex = tabControlAdv.SelectedIndex;
                    totalCount = tabControlAdv.TabPages.Count;
                }
            }

            if (foundTab is not null)
            {
                _logger?.LogInformation("[SWITCH_RIGHT_PANEL] Successfully switched to tab {TabName} (index {Index} of {Total})",
                    actualTabName, selectedIndex, totalCount);

                // Lazy initialization for JARVISChatUserControl (deferred until first navigation)
                if (actualTabName == "JARVISChatTab" && foundTab is TabPage jarvisTabPage)
                {
                    var jarvisControl = jarvisTabPage.Controls.OfType<JARVISChatUserControl>().FirstOrDefault();
                    if (jarvisControl != null)
                    {
                        // JARVISChatUserControl implements IAsyncInitializable - cast is guaranteed safe
#pragma warning disable CA1508 // Avoid dead conditional code (analyzer false positive)
                        var asyncInit = (WileyWidget.Abstractions.IAsyncInitializable)jarvisControl;
#pragma warning restore CA1508
                        _logger?.LogDebug("[SWITCH_RIGHT_PANEL] Triggering lazy initialization for JARVISChatUserControl");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await asyncInit.InitializeAsync().ConfigureAwait(false);
                                _logger?.LogInformation("[SWITCH_RIGHT_PANEL] JARVISChatUserControl initialized successfully");
                            }
                            catch (Exception initEx)
                            {
                                _logger?.LogError(initEx, "[SWITCH_RIGHT_PANEL] Failed to initialize JARVISChatUserControl");
                            }
                        });
                    }
                }
            }
            else
            {
                var availableTabs = tabControl is not null
                    ? string.Join(", ", tabControl.TabPages.Cast<TabPage>().Select(t => t.Name))
                    : tabControlAdv is not null
                        ? string.Join(", ", tabControlAdv.TabPages.Cast<TabPageAdv>().Select(t => t.Name))
                        : "";
                var displayTabs = tabControl is not null
                    ? string.Join(", ", tabControl.TabPages.Cast<TabPage>().Select(t => t.Text))
                    : tabControlAdv is not null
                        ? string.Join(", ", tabControlAdv.TabPages.Cast<TabPageAdv>().Select(t => t.Text))
                        : "";

                _logger?.LogError("[SWITCH_RIGHT_PANEL] Tab {ActualTabName} not found. Available tabs: {AvailableTabs}",
                    actualTabName, availableTabs);
                MessageBox.Show(
                    $"Tab '{actualTabName}' was not found. Available tabs: {displayTabs}",
                    "Tab Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[SWITCH_RIGHT_PANEL] Failed to switch right panel to {TabName}", tabName);
            MessageBox.Show(
                $"Failed to open panel: {ex.Message}",
                "Switch Panel Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
