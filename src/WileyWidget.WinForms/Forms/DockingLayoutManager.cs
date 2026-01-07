using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WileyWidget.WinForms.Controls;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using WileyWidget.WinForms.Controls.ChatUI;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Manages docking layout persistence, state changes, and dynamic panel lifecycle.
/// Single Responsibility: Handle all docking layout and state management operations.
/// </summary>
public class DockingLayoutManager : IDisposable
{
    private bool _disposed;

    // Layout persistence constants
    private readonly ILogger? _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPanelNavigationService? _panelNavigator;

    // Layout persistence constants
    private const string LayoutVersionAttributeName = "LayoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const int LayoutLoadWarningMs = 5000;
    private const int MinimumSaveIntervalMs = 2000;

    // State management
    private bool _isSavingLayout;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private readonly object _dockingSaveLock = new();
    private System.Windows.Forms.Timer? _dockingLayoutSaveTimer;

    // Dynamic panels tracking
    private Dictionary<string, GradientPanelExt>? _dynamicDockPanels = new();

    // Owned resources - fonts, panels, and diagnostics
    private Font? _dockAutoHideTabFont;
    private Font? _dockTabFont;
    private GradientPanelExt? _leftDockPanel;
    private GradientPanelExt? _rightDockPanel;
    // REMOVED: _centralDocumentPanel - Option A pure docking architecture

    public DockingLayoutManager(IServiceProvider serviceProvider, IPanelNavigationService? panelNavigator, ILogger? logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger;
    }

    /// <summary>
    /// Transfer ownership of managed docking panels and fonts to this manager
    /// </summary>
    /// <remarks>Option A architecture - no central panel, pure left/right docking</remarks>
    public void SetManagedResources(GradientPanelExt? leftPanel, GradientPanelExt? rightPanel, Font? dockAutoHideTabFont, Font? dockTabFont)
    {
        _leftDockPanel = leftPanel;
        _rightDockPanel = rightPanel;
        _dockAutoHideTabFont = dockAutoHideTabFont;
        _dockTabFont = dockTabFont;
        _logger?.LogDebug("DockingLayoutManager now owns: {LeftPanel}, {RightPanel}, {AutoHideFont}, {TabFont}",
            leftPanel != null, rightPanel != null, dockAutoHideTabFont != null, dockTabFont != null);
    }

    /// <summary>
    /// Initialize DockingManager with best practice settings
    /// </summary>
    public void InitializeDockingManager(DockingManager dockingManager)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));

        try
        {
            // Set global docking manager properties for optimal behavior
            dockingManager.PersistState = false; // We handle persistence manually for better control
            dockingManager.MaximizeButtonEnabled = true;
            dockingManager.ShowCaptionImages = true;

            // AnimationStep is a static property
            DockingManager.AnimationStep = 5; // Smooth animation

            _logger?.LogInformation("DockingManager initialized with best practice settings");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize some DockingManager settings - using defaults");
        }
    }

    /// <summary>
    /// Attach this manager to a DockingManager instance with comprehensive event handling
    /// </summary>
    public void AttachTo(DockingManager dockingManager)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));

        // Attach all necessary event handlers for proper state management
        dockingManager.DockStateChanged += DockingManager_DockStateChanged;
        dockingManager.DockControlActivated += DockingManager_DockControlActivated;
        dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;
        dockingManager.NewDockStateBeginLoad += DockingManager_NewDockStateBeginLoad;
        dockingManager.NewDockStateEndLoad += DockingManager_NewDockStateEndLoad;

        _logger?.LogInformation("DockingLayoutManager attached to DockingManager with comprehensive event handling");
    }

    /// <summary>
    /// Detach this manager from a DockingManager instance
    /// </summary>
    public void DetachFrom(DockingManager dockingManager)
    {
        if (dockingManager == null) return;

        try
        {
            // Detach all event handlers safely
            dockingManager.DockStateChanged -= DockingManager_DockStateChanged;
            dockingManager.DockControlActivated -= DockingManager_DockControlActivated;
            dockingManager.DockVisibilityChanged -= DockingManager_DockVisibilityChanged;
            dockingManager.NewDockStateBeginLoad -= DockingManager_NewDockStateBeginLoad;
            dockingManager.NewDockStateEndLoad -= DockingManager_NewDockStateEndLoad;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detaching some event handlers from DockingManager");
        }

        _logger?.LogInformation("DockingLayoutManager detached from DockingManager");
    }

    /// <summary>
    /// Load docking layout from disk
    /// </summary>
    public async Task LoadLayoutAsync(DockingManager dockingManager, Control parentForm, string layoutPath)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (parentForm == null) throw new ArgumentNullException(nameof(parentForm));
        if (string.IsNullOrEmpty(layoutPath)) throw new ArgumentNullException(nameof(layoutPath));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug("LoadDockingLayout START - ThreadId={ThreadId}, layoutPath={Path}",
                System.Threading.Thread.CurrentThread.ManagedThreadId, layoutPath);

            // Support either the XML layout path or the binary '.bin' layout path.
            var binaryLayoutPath = Path.ChangeExtension(layoutPath, ".bin");

            if (!File.Exists(layoutPath) && !File.Exists(binaryLayoutPath))
            {
                _logger?.LogInformation("Docking layout file not found (neither {Path} nor {BinaryPath}) - using default layout", layoutPath, binaryLayoutPath);
                return;
            }

            // Prefer binary layout if available
            if (File.Exists(binaryLayoutPath)) layoutPath = binaryLayoutPath;


            // Load dynamic panel metadata first
            var panelInfos = LoadDynamicPanelMetadata(layoutPath);

            // Load the actual dock state
            LogDockStateLoad(layoutPath);
            await LoadDockStateAsync(dockingManager, parentForm, layoutPath);

            // Recreate dynamic panels
            RecreateDynamicPanels(dockingManager, parentForm, panelInfos);

            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs > LayoutLoadWarningMs)
            {
                _logger?.LogWarning("Layout load took {ElapsedMs}ms (threshold: {ThresholdMs}ms) - consider simplifying layout", elapsedMs, LayoutLoadWarningMs);
            }
            else
            {
                _logger?.LogInformation("Docking layout loaded from {Path} in {ElapsedMs}ms", layoutPath, elapsedMs);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HandleDockStateLoadError(layoutPath, ex, "Failed to load docking layout");
        }
    }

    /// <summary>
    /// Save docking layout to disk using proper Syncfusion API patterns
    /// </summary>
    public void SaveLayout(DockingManager dockingManager, string layoutPath)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (string.IsNullOrEmpty(layoutPath)) throw new ArgumentNullException(nameof(layoutPath));

        // Concurrency guard
        lock (_dockingSaveLock)
        {
            if (_isSavingLayout)
            {
                _logger?.LogDebug("SaveDockingLayout skipped - save already in progress");
                return;
            }
            _isSavingLayout = true;
        }

        try
        {
            // CRITICAL: Verify DockingManager has been properly initialized
            // Prevents NullReferenceException in Syncfusion's internal serialization
            if (dockingManager.HostControl == null)
            {
                _logger?.LogDebug("SaveDockingLayout skipped - DockingManager has no HostControl");
                return;
            }

            // Guard: Check if DockingManager has any docked controls
            // Prevents NullReferenceException when accessing internal control collections
            var hostForm = dockingManager.HostControl as Form;
            if (hostForm == null)
            {
                _logger?.LogDebug("SaveDockingLayout skipped - HostControl is not a Form");
                return;
            }

            // Count docked controls by scanning the full host control tree for panels that are managed by DockingManager
            // NOTE: Some docked panels are placed inside DockHost_* containers and may not be direct children of the form
            int dockedControlCount = 0;
            IEnumerable<Control> EnumerateControls(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    yield return c;
                    foreach (var inner in EnumerateControls(c)) yield return inner;
                }
            }

            foreach (var ctrl in EnumerateControls(hostForm))
            {
                if (ctrl is Panel panel)
                {
                    try
                    {
                        var isDocked = dockingManager.GetEnableDocking(panel);
                        if (isDocked)
                        {
                            dockedControlCount++;
                        }
                    }
                    catch { /* Skip panels not managed by DockingManager */ }
                }
            }

            if (dockedControlCount == 0)
            {
                _logger?.LogDebug("SaveDockingLayout skipped - no docked controls present (count: {Count})", dockedControlCount);
                return;
            }

            _logger?.LogDebug("Saving dock state with {Count} docked controls", dockedControlCount);

            // Use BinaryFile serialization mode per Syncfusion best practices
            // Binary format is faster and more reliable than XML for large layouts
            var binaryLayoutPath = Path.ChangeExtension(layoutPath, ".bin");
            var tempPath = binaryLayoutPath + ".tmp";

            // Create AppStateSerializer with proper error handling
            AppStateSerializer? serializer = null;
            try
            {
                serializer = new AppStateSerializer(SerializeMode.BinaryFile, tempPath);

                // Save dock state using Syncfusion API directly (no reflection needed)
                // Some Syncfusion versions require PersistState to be enabled for SaveDockState to emit file output.
                var originalPersistState = dockingManager.PersistState;
                try
                {
                    dockingManager.PersistState = true;
                    dockingManager.SaveDockState(serializer);
                    // CRITICAL: Call PersistNow() to actually write the serializer to disk
                    serializer.PersistNow();

                    // Some implementations may flush files asynchronously; wait briefly for output to appear
                    var swWriter = System.Diagnostics.Stopwatch.StartNew();
                    while (!File.Exists(tempPath) && !File.Exists(binaryLayoutPath) && swWriter.ElapsedMilliseconds < 500)
                    {
                        System.Threading.Thread.Sleep(25);
                    }
                }
                finally
                {
                    // Restore original setting
                    dockingManager.PersistState = originalPersistState;
                }

                _logger?.LogDebug("Saved and persisted dock state (temp: {TempPath}, intended final: {BinaryPath})", tempPath, binaryLayoutPath);

                // Verify serializer produced output. AppStateSerializer may write either the temp file or write directly
                // to the final path depending on underlying implementation. Handle both cases safely.
                var tempExists = File.Exists(tempPath);
                var finalExistsBeforeReplace = File.Exists(binaryLayoutPath);

                if (tempExists)
                {
                    // Atomic file replacement
                    ReplaceDockingLayoutFile(tempPath, binaryLayoutPath);
                }
                else if (!finalExistsBeforeReplace)
                {
                    // Nothing was written - attempt a safe fallback by writing directly to the final path
                    _logger?.LogWarning("No serializer output detected at temp or final paths. Attempting fallback write to final binary path: {FinalPath}", binaryLayoutPath);

                    // First attempt: fallback to BinaryFile direct write
                    try
                    {
                        var fallbackSerializer = new AppStateSerializer(SerializeMode.BinaryFile, binaryLayoutPath);
                        var originalPersistState2 = dockingManager.PersistState;
                        try
                        {
                            dockingManager.PersistState = true;
                            dockingManager.SaveDockState(fallbackSerializer);
                            fallbackSerializer.PersistNow();
                        }
                        finally
                        {
                            dockingManager.PersistState = originalPersistState2;
                        }

                        // As above, wait briefly for final file to appear
                        var swFallback = System.Diagnostics.Stopwatch.StartNew();
                        while (!File.Exists(binaryLayoutPath) && swFallback.ElapsedMilliseconds < 500)
                        {
                            System.Threading.Thread.Sleep(25);
                        }

                        _logger?.LogDebug("Fallback serializer attempted writing directly to {FinalPath}", binaryLayoutPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Fallback serializer failed to write final binary layout {FinalPath}", binaryLayoutPath);
                    }

                    // If still not written, attempt memory-stream fallback using BinaryFmtStream
                    if (!File.Exists(binaryLayoutPath))
                    {
                        _logger?.LogWarning("Fallback binary file not created by BinaryFile serializer. Attempting memory-stream fallback (SerializeMode.BinaryFmtStream) to write bytes to {FinalPath}", binaryLayoutPath);
                        try
                        {
                            using var ms = new System.IO.MemoryStream();
                            var memSerializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                            var originalPersistState3 = dockingManager.PersistState;
                            try
                            {
                                dockingManager.PersistState = true;
                                dockingManager.SaveDockState(memSerializer);
                                memSerializer.PersistNow();
                            }
                            finally
                            {
                                dockingManager.PersistState = originalPersistState3;
                            }

                            // Ensure stream has data and write it atomically to temp then replace
                            if (ms.Length > 0)
                            {
                                ms.Position = 0;
                                var bytes = ms.ToArray();
                                File.WriteAllBytes(tempPath, bytes);
                                ReplaceDockingLayoutFile(tempPath, binaryLayoutPath);

                                var swMem = System.Diagnostics.Stopwatch.StartNew();
                                while (!File.Exists(binaryLayoutPath) && swMem.ElapsedMilliseconds < 500)
                                {
                                    System.Threading.Thread.Sleep(25);
                                }

                                _logger?.LogDebug("Memory-stream fallback wrote {ByteCount} bytes to {FinalPath}", bytes.Length, binaryLayoutPath);
                            }
                            else
                            {
                                _logger?.LogWarning("Memory-stream fallback produced 0 bytes for docking layout - aborting");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Memory-stream fallback failed to serialize dock state to {FinalPath}", binaryLayoutPath);
                        }
                    }
                }

                // Final verification: ensure the binary layout file exists before proceeding
                if (!File.Exists(binaryLayoutPath))
                {
                    _logger?.LogError("Docking layout final file not found after save attempt: {FinalPath}", binaryLayoutPath);
                    return;
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save dock state with AppStateSerializer");
                return;
            }

            // Save dynamic panels metadata (JSON)
            // Always write panels metadata file (empty array when no dynamic panels) to avoid missing artifacts in consumers/tests
            if (_dynamicDockPanels == null || !_dynamicDockPanels.Any())
            {
                try
                {
                    var panelsPath = layoutPath + ".panels.json";
                    File.WriteAllText(panelsPath, "[]");
                    _logger?.LogDebug("Wrote empty panels metadata file to {Path}", panelsPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to write empty panels metadata file");
                }
            }
            else
            {
                SaveDynamicPanels(layoutPath);
            }

            _logger?.LogInformation("Docking layout saved to {Path} (binary format)", binaryLayoutPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save docking layout to {Path}", layoutPath);
        }
        finally
        {
            _isSavingLayout = false;
        }
    }

    /// <summary>
    /// Apply theme to docking panels
    /// </summary>
    /// <remarks>Option A architecture - left and right panels only</remarks>
    public void ApplyThemeToDockingPanels(DockingManager? dockingManager, Panel? leftPanel, Panel? rightPanel)
    {
        try
        {
            if (dockingManager != null)
            {
                _logger?.LogDebug("DockingManager uses cascaded theme from ApplicationVisualTheme");
            }

            ApplyPanelTheme(leftPanel);
            ApplyPanelTheme(rightPanel);

            _logger?.LogInformation("Applied SkinManager theme to docking panels");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme to docking panels - using default colors");
        }
    }

    // REMOVED: EnsureCentralPanelVisibility - Option A has no central panel

    /// <summary>
    /// Set floating mode for a panel
    /// </summary>
    public void TrySetFloatingMode(DockingManager? dockingManager, Panel panel, bool allowFloating)
    {
        if (panel == null) throw new ArgumentNullException(nameof(panel));

        try
        {
            dockingManager?.SetAllowFloating(panel, allowFloating);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set floating mode for panel '{PanelName}'", panel.Name);
        }
    }

    /// <summary>
    /// Start debounced save timer
    /// </summary>
    public void StartDebouncedSave(DockingManager dockingManager, string layoutPath)
    {
        try
        {
            _logger?.LogDebug("DebouncedSaveDockingLayout invoked - _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
                _isSavingLayout, _lastSaveTime);
        }
        catch (Exception ex)
        {
            // Logging failure is non-critical
            Debug.WriteLine($"Failed to log debounce info: {ex.Message}");
        }

        if (_isSavingLayout)
        {
            _logger?.LogDebug("Skipping debounced save - save already in progress");
            return;
        }

        var timeSinceLastSave = DateTime.Now - _lastSaveTime;
        if (timeSinceLastSave < TimeSpan.FromMilliseconds(MinimumSaveIntervalMs))
        {
            _logger?.LogDebug("Skipping debounced save - too soon since last save ({Elapsed}ms ago)",
                timeSinceLastSave.TotalMilliseconds);
            return;
        }

        _dockingLayoutSaveTimer?.Stop();

        if (_dockingLayoutSaveTimer == null)
        {
            _dockingLayoutSaveTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _dockingLayoutSaveTimer.Tick += (s, e) => OnSaveTimerTick(dockingManager, layoutPath);
        }

        _dockingLayoutSaveTimer.Start();
    }

    // Private helper methods

    private async Task LoadDockStateAsync(DockingManager dockingManager, Control parentForm, string layoutPath)
    {
        // Use BinaryFile serialization mode per Syncfusion best practices
        // Binary format is faster and more reliable than XML for large layouts
        var binaryLayoutPath = Path.ChangeExtension(layoutPath, ".bin");
        if (File.Exists(binaryLayoutPath))
        {
            layoutPath = binaryLayoutPath;
            _logger?.LogDebug("Using binary layout file: {Path}", layoutPath);
        }

        AppStateSerializer? serializer = null;
        try
        {
            serializer = new AppStateSerializer(SerializeMode.BinaryFile, layoutPath);

            bool success;
            try
            {
                success = dockingManager.LoadDockState(serializer);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Exception during LoadDockState - falling back to default docking");
                success = false;
            }

            if (success)
            {
                // Ensure caption buttons are visible after load (LoadDockState can overwrite some per-panel visibility)
                try
                {
                    EnsureCaptionButtonsVisible(dockingManager, parentForm);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to ensure caption buttons visible after LoadDockState");
                }

                _logger?.LogInformation("Successfully loaded dock state from {Path}", layoutPath);
            }
            else
            {
                _logger?.LogWarning("LoadDockState returned false - layout file may be corrupt or incompatible. Falling back to default docking.");

                // Fallback: Re-dock panels programmatically
                await ReDockPanelsProgrammatically(dockingManager, parentForm);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during LoadDockState - falling back to default docking");

            // Fallback: Re-dock panels programmatically
            await ReDockPanelsProgrammatically(dockingManager, parentForm);
        }
    }

    /// <summary>
    /// Re-docks left and right panels programmatically using default positions.
    /// Used as a fallback when LoadDockState fails or layout file is missing/corrupt.
    /// Follows Syncfusion best practices for programmatic docking.
    /// </summary>
    /// <param name="dockingManager">The DockingManager instance to configure.</param>
    /// <param name="parentForm">The parent form to dock panels to.</param>
    private Task ReDockPanelsProgrammatically(DockingManager dockingManager, Control parentForm)
    {
        try
        {
            _logger?.LogInformation("Re-docking panels programmatically to default positions");

            // Re-dock left panel (Dashboard) if it exists
            if (_leftDockPanel != null)
            {
                try
                {
                    _logger?.LogDebug("Re-docking {PanelName} to Left with width 280px", _leftDockPanel.Name);

                    // Enable docking first (Syncfusion requirement)
                    dockingManager.SetEnableDocking(_leftDockPanel, true);

                    // Set dock label
                    dockingManager.SetDockLabel(_leftDockPanel, "Dashboard");

                    // Configure floating behavior
                    dockingManager.SetAllowFloating(_leftDockPanel, true);

                    // Dock the panel
                    dockingManager.DockControl(_leftDockPanel, parentForm, DockingStyle.Left, 280);

                    // Ensure caption buttons are visible
                    dockingManager.SetCloseButtonVisibility(_leftDockPanel, true);
                    dockingManager.SetAutoHideButtonVisibility(_leftDockPanel, true);
                    dockingManager.SetMenuButtonVisibility(_leftDockPanel, true);

                    _logger?.LogDebug("Successfully re-docked left panel");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to re-dock left panel");
                }
            }

            // Re-dock right panel (Activity) if it exists
            if (_rightDockPanel != null)
            {
                try
                {
                    _logger?.LogDebug("Re-docking {PanelName} to Right with width 280px", _rightDockPanel.Name);

                    // Enable docking first (Syncfusion requirement)
                    dockingManager.SetEnableDocking(_rightDockPanel, true);

                    // Set dock label
                    dockingManager.SetDockLabel(_rightDockPanel, "Activity");

                    // Configure floating behavior
                    dockingManager.SetAllowFloating(_rightDockPanel, true);

                    // Dock the panel
                    dockingManager.DockControl(_rightDockPanel, parentForm, DockingStyle.Right, 280);

                    // Ensure caption buttons are visible
                    dockingManager.SetCloseButtonVisibility(_rightDockPanel, true);
                    dockingManager.SetAutoHideButtonVisibility(_rightDockPanel, true);
                    dockingManager.SetMenuButtonVisibility(_rightDockPanel, true);

                    _logger?.LogDebug("Successfully re-docked right panel");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to re-dock right panel");
                }
            }

            _logger?.LogInformation("Successfully re-docked panels to default positions");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to re-dock panels programmatically");
        }

        return Task.CompletedTask;
    }

    private void LogDockStateLoad(string layoutPath)
    {
        _logger?.LogInformation("Calling _dockingManager.LoadDockState - ThreadId={ThreadId}, layoutPath={Path}",
            System.Threading.Thread.CurrentThread.ManagedThreadId, layoutPath);
    }

    private void HandleDockStateLoadError(string layoutPath, Exception ex, string message)
    {
        _logger?.LogWarning(ex, "{Message} - resetting to default layout ({Path})", message, layoutPath);

        try
        {
            File.Delete(layoutPath);
            _logger?.LogInformation("Deleted corrupt docking layout file {Path}", layoutPath);
        }
        catch (Exception deleteEx)
        {
            _logger?.LogWarning(deleteEx, "Failed to delete corrupt docking layout file {Path}", layoutPath);
        }
    }

    private List<DynamicPanelInfo> LoadDynamicPanelMetadata(string layoutPath)
    {
        var results = new List<DynamicPanelInfo>();
        if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath)) return results;

        try
        {
            var doc = new XmlDocument();
            doc.Load(layoutPath);

            var nodes = doc.SelectNodes("//PanelInfo") ?? doc.SelectNodes("//Panel");
            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    try
                    {
                        var info = new DynamicPanelInfo();
                        var nameAttr = node.Attributes?["Name"]?.Value ?? node.Attributes?["name"]?.Value;
                        if (!string.IsNullOrWhiteSpace(nameAttr)) info.Name = nameAttr;
                        info.DockLabel = node.Attributes?["DockLabel"]?.Value ?? node.Attributes?["dockLabel"]?.Value;
                        if (bool.TryParse(node.Attributes?["IsAutoHide"]?.Value ?? node.Attributes?["isAutoHide"]?.Value, out var isAuto))
                        {
                            info.IsAutoHide = isAuto;
                        }
                        results.Add(info);
                    }
                    catch { /* ignore individual node errors */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to parse dynamic panel metadata from layout {Path}", layoutPath);
        }

        return results;
    }

    private void RecreateDynamicPanels(DockingManager dockingManager, Control parentForm, List<DynamicPanelInfo> panelInfos)
    {
        foreach (var panelInfo in panelInfos)
        {
            try
            {
                RecreateDynamicPanel(dockingManager, parentForm, panelInfo);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to recreate dynamic panel '{PanelName}'", panelInfo.Name);
            }
        }
    }

    private void RecreateDynamicPanel(DockingManager? dockingManager, Control parentForm, DynamicPanelInfo panelInfo)
    {
        if (panelInfo?.Name == null) return;

        if (_dynamicDockPanels.ContainsKey(panelInfo.Name)) return;

        try
        {
            // Prefer rebuilding known panels through PanelNavigationService so real controls are used
            if (_panelNavigator != null && panelInfo.Name.Contains("Chat", StringComparison.OrdinalIgnoreCase))
            {
                var label = panelInfo.DockLabel ?? panelInfo.Name;
                _panelNavigator.ShowPanel<ChatPanel>(label, DockingStyle.Right, allowFloating: true);
                _logger?.LogInformation("Recreated chat panel via PanelNavigationService: {PanelName}", label);
                return;
            }

            // STEP 1: Create empty panel
            var panel = new GradientPanelExt
            {
                Name = panelInfo.Name,
                BorderStyle = BorderStyle.None,
                BackgroundColor = new Syncfusion.Drawing.BrushInfo(Syncfusion.Drawing.GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(panel, "Office2019Colorful");

            // STEP 2-4: Dock panel FIRST (Syncfusion official pattern)
            // Set up docking if DockingManager is available
            if (dockingManager != null)
            {
                dockingManager.SetEnableDocking(panel, true);
                dockingManager.SetDockLabel(panel, panelInfo.DockLabel ?? panelInfo.Name);
                dockingManager.SetAllowFloating(panel, true);
                dockingManager.DockControl(panel, parentForm, Syncfusion.Windows.Forms.Tools.DockingStyle.Left, 200);
                if (panelInfo.IsAutoHide)
                {
                    dockingManager.SetAutoHideMode(panel, true);
                }

                // Ensure caption buttons are visible for recreated dynamic panels
                try
                {
                    dockingManager.SetCloseButtonVisibility(panel, true);
                    dockingManager.SetAutoHideButtonVisibility(panel, true);
                    dockingManager.SetMenuButtonVisibility(panel, true);
                }
                catch { }
            }

            // STEP 5: Add child content AFTER docking (Syncfusion official pattern)
            // Adding content after DockControl ensures DockingManager's internal control collections are ready
            if (panelInfo.Name.Contains("Chat", StringComparison.OrdinalIgnoreCase))
            {
                panel.Controls.Add(new Label { Text = "AI Chat Panel", Dock = DockStyle.Top });
            }
            else if (panelInfo.Name.Contains("Log", StringComparison.OrdinalIgnoreCase))
            {
                panel.Controls.Add(new Label { Text = "Log Panel", Dock = DockStyle.Top });
            }
            else
            {
                panel.Controls.Add(new Label { Text = $"{panelInfo.Name} Panel", Dock = DockStyle.Top });
            }

            _dynamicDockPanels[panelInfo.Name] = panel;
            _logger?.LogDebug("Recreated dynamic panel: {PanelName} (content added after docking)", panelInfo.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to recreate dynamic panel '{PanelName}'", panelInfo.Name);
        }
    }

    private void SaveDynamicPanels(string layoutPath)
    {
        if (_dynamicDockPanels == null || !_dynamicDockPanels.Any()) return;

        try
        {
            var panelsPath = layoutPath + ".panels.json";
            var panelData = _dynamicDockPanels.Select(kvp => new
            {
                Name = kvp.Key,
                Type = kvp.Value.GetType().FullName,
                IsVisible = kvp.Value.Visible,
                DockStyle = kvp.Value.Dock.ToString(),
                AllowFloating = true // Default for dynamic panels
            });

            var json = System.Text.Json.JsonSerializer.Serialize(panelData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(panelsPath, json);
            _logger?.LogDebug("Saved dynamic panels metadata to {Path}", panelsPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save dynamic panels metadata");
        }
    }

    private void LoadDynamicPanels(string layoutPath)
    {
        var panelsPath = layoutPath + ".panels.json";
        if (!File.Exists(panelsPath)) return;

        try
        {
            var json = File.ReadAllText(panelsPath);
            var panelData = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);

            if (panelData != null)
            {
                foreach (var element in panelData)
                {
                    try
                    {
                        var name = element.GetProperty("Name").GetString();
                        var typeName = element.GetProperty("Type").GetString();
                        var isVisible = element.GetProperty("IsVisible").GetBoolean();
                        var dockStyleStr = element.GetProperty("DockStyle").GetString();
                        var allowFloating = element.GetProperty("AllowFloating").GetBoolean();

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(typeName))
                        {
                            RestoreDynamicPanel(name, typeName, isVisible, dockStyleStr, allowFloating);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to restore dynamic panel from metadata");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load dynamic panels metadata");
        }
    }

    private void RestoreDynamicPanel(string name, string typeName, bool isVisible, string? dockStyleStr, bool allowFloating)
    {
        try
        {
            var dockStyle = DockingStyle.Right;

            // `dockStyleStr` is persisted from WinForms `Control.Dock` (DockStyle), not Syncfusion DockingStyle.
            // Map it to a safe DockingStyle to avoid passing `DockingStyle.Fill` into `DockControl`.
            if (!string.IsNullOrWhiteSpace(dockStyleStr))
            {
                if (Enum.TryParse<DockStyle>(dockStyleStr, out var winFormsDockStyle))
                {
                    dockStyle = winFormsDockStyle switch
                    {
                        DockStyle.Left => DockingStyle.Left,
                        DockStyle.Right => DockingStyle.Right,
                        DockStyle.Top => DockingStyle.Top,
                        DockStyle.Bottom => DockingStyle.Bottom,
                        _ => DockingStyle.Right
                    };
                }
                else if (Enum.TryParse<DockingStyle>(dockStyleStr, out var parsedDockingStyle) && parsedDockingStyle != DockingStyle.Fill)
                {
                    dockStyle = parsedDockingStyle;
                }
            }

            var panelType = Type.GetType(typeName);
            if (panelType != null && panelType.IsSubclassOf(typeof(UserControl)))
            {
                var method = typeof(IPanelNavigationService).GetMethod("ShowPanel", new[] { typeof(string), typeof(DockingStyle), typeof(bool) });
                if (method != null && _panelNavigator != null)
                {
                    var genericMethod = method.MakeGenericMethod(panelType);
                    genericMethod.Invoke(_panelNavigator, new object[] { name, dockStyle, allowFloating });

                    if (_dynamicDockPanels != null && _dynamicDockPanels.TryGetValue(name, out var panel) && !isVisible)
                    {
                        // Note: This would need access to the DockingManager to set visibility
                        _logger?.LogDebug("Restored dynamic panel: {PanelName} ({PanelType})", name, typeName);
                    }
                }
            }
            else
            {
                _logger?.LogWarning("Cannot restore dynamic panel - type not found or not a UserControl: {TypeName}", typeName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to restore dynamic panel {PanelName}: {TypeName}", name, typeName);
        }
    }

    private void ReplaceDockingLayoutFile(string tempPath, string finalPath)
    {
        if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(finalPath)) return;
        if (!File.Exists(tempPath)) return;

        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath, true);
            InjectLayoutVersion(finalPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to replace layout file: {ex.Message}");
        }
        finally
        {
            TryCleanupTempFile(tempPath);
        }
    }

    /// <summary>
    /// Ensure standard caption buttons (Close, AutoHide, Menu) are visible for all docked controls.
    /// Also enforces global DockingManager caption-related settings (maximize button, caption images).
    /// Follows Syncfusion best practices for caption management.
    /// </summary>
    private void EnsureCaptionButtonsVisible(DockingManager dockingManager, Control parentForm)
    {
        if (dockingManager == null || parentForm == null) return;

        try
        {
            // Set global DockingManager properties for consistent behavior
            try { dockingManager.MaximizeButtonEnabled = true; } catch { }
            try { dockingManager.ShowCaptionImages = true; } catch { }

            // Get all docked controls and ensure they have proper caption buttons
            var dockedControls = GetAllDockedControls(dockingManager, parentForm);
            int successCount = 0;

            foreach (var ctrl in dockedControls)
            {
                try
                {
                    // Set caption button visibility with proper error handling
                    dockingManager.SetCloseButtonVisibility(ctrl, true);
                    dockingManager.SetAutoHideButtonVisibility(ctrl, true);
                    dockingManager.SetMenuButtonVisibility(ctrl, true);

                    // Ensure the control is properly enabled for docking operations
                    if (!dockingManager.GetEnableDocking(ctrl))
                    {
                        dockingManager.SetEnableDocking(ctrl, true);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to set caption buttons for control '{ControlName}'", ctrl.Name);
                }
            }

            _logger?.LogInformation("Ensured caption buttons visible for {SuccessCount}/{TotalCount} docked controls",
                successCount, dockedControls.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure caption buttons visible for docking manager");
        }
    }

    private List<Control> GetAllDockedControls(DockingManager dockingManager, Control root)
    {
        var results = new List<Control>();
        if (dockingManager == null || root == null) return results;

        void Walk(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                try
                {
                    if (dockingManager.GetEnableDocking(child))
                    {
                        results.Add(child);
                    }
                }
                catch { /* not managed by docking manager or API not available */ }

                if (child.HasChildren) Walk(child);
            }
        }

        Walk(root);
        return results;
    }

    private void InjectLayoutVersion(string layoutPath)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(layoutPath);
            xmlDoc.DocumentElement?.SetAttribute(LayoutVersionAttributeName, CurrentLayoutVersion);
            xmlDoc.Save(layoutPath);
            _logger?.LogDebug("Injected layout version {Version} into {Path}", CurrentLayoutVersion, layoutPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to inject layout version into {Path}", layoutPath);
        }
    }

    private static void TryCleanupTempFile(string tempPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to cleanup temp file: {ex.Message}");
        }
    }

    private static void ApplyPanelTheme(Control? panel)
    {
        if (panel == null) return;
        Debug.WriteLine("Panel uses cascaded theme from ApplicationVisualTheme");
    }

    // REMOVED: EnsureCentralPanelVisible - Option A architecture has no central panel

    private static void EnsureSidePanelsZOrder(Panel? leftPanel, Panel? rightPanel)
    {
        if (leftPanel != null)
        {
            try { leftPanel.SendToBack(); }
            catch (Exception ex) { Debug.WriteLine($"Failed to set left dock panel z-order: {ex.Message}"); }
        }

        if (rightPanel != null)
        {
            try { rightPanel.SendToBack(); }
            catch (Exception ex) { Debug.WriteLine($"Failed to set right dock panel z-order: {ex.Message}"); }
        }
    }

    private void OnSaveTimerTick(DockingManager dockingManager, string layoutPath)
    {
        _dockingLayoutSaveTimer?.Stop();

        if (_isSavingLayout)
        {
            _logger?.LogDebug("Skipping timer save - save already in progress");
            return;
        }

        try
        {
            _logger?.LogDebug("OnSaveTimerTick - performing debounced save (ThreadId={ThreadId})", System.Threading.Thread.CurrentThread.ManagedThreadId);
            SaveLayout(dockingManager, layoutPath);
            _lastSaveTime = DateTime.Now;
            _logger?.LogDebug("Debounced auto-save completed - Time={Time}", _lastSaveTime);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to auto-save docking layout after debounce period");
        }
    }

    // Event handlers

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        _logger?.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Note: Debounced save is handled by the form, not here, to avoid circular dependencies
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger?.LogDebug("Dock control activated: {Control}", e.Control?.Name ?? "null");
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        _logger?.LogDebug("Dock visibility changed: Control={Control}, Visible={Visible}",
            e.Control?.Name ?? "null", e.Control?.Visible ?? false);
    }

    private void DockingManager_NewDockStateBeginLoad(object? sender, EventArgs e)
    {
        _logger?.LogDebug("DockingManager beginning to load new dock state");
    }

    private void DockingManager_NewDockStateEndLoad(object? sender, EventArgs e)
    {
        _logger?.LogDebug("DockingManager finished loading new dock state");
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose debounce timer
            if (_dockingLayoutSaveTimer != null)
            {
                try
                {
                    _dockingLayoutSaveTimer.Stop();
                    _dockingLayoutSaveTimer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to dispose docking layout save timer");
                }
                finally
                {
                    _dockingLayoutSaveTimer = null;
                }
            }

            // Dispose dynamic panels
            if (_dynamicDockPanels != null)
            {
                foreach (var panel in _dynamicDockPanels.Values)
                {
                    try { panel?.Dispose(); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to dispose dynamic dock panel"); }
                }
                _dynamicDockPanels.Clear();
                _dynamicDockPanels = null;
            }

            // Dispose managed panels
            try { _leftDockPanel?.Dispose(); } catch { }
            _leftDockPanel = null;

            try { _rightDockPanel?.Dispose(); } catch { }
            _rightDockPanel = null;

            // REMOVED: _centralDocumentPanel disposal - Option A has no central panel

            // Dispose fonts used by DockingManager
            try { _dockAutoHideTabFont?.Dispose(); } catch { }
            _dockAutoHideTabFont = null;

            try { _dockTabFont?.Dispose(); } catch { }
            _dockTabFont = null;

            _logger?.LogDebug("DockingLayoutManager disposed all owned resources");
        }

        _disposed = true;
    }
}

/// <summary>
/// Information about a dynamic panel for persistence
/// </summary>
public class DynamicPanelInfo
{
    public string? Name { get; set; }
    public string? DockLabel { get; set; }
    public bool IsAutoHide { get; set; }
}
