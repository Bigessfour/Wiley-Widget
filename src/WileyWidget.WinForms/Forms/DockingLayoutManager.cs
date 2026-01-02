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
    private Dictionary<string, Panel>? _dynamicDockPanels = new();

    // Owned resources - fonts, panels, and diagnostics
    private Font? _dockAutoHideTabFont;
    private Font? _dockTabFont;
    private Panel? _leftDockPanel;
    private Panel? _rightDockPanel;
    private Panel? _centralDocumentPanel;

    public DockingLayoutManager(IServiceProvider serviceProvider, IPanelNavigationService? panelNavigator, ILogger? logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger;
    }

    /// <summary>
    /// Transfer ownership of managed docking panels and fonts to this manager
    /// </summary>
    public void SetManagedResources(Panel? leftPanel, Panel? rightPanel, Panel? centralPanel, Font? dockAutoHideTabFont, Font? dockTabFont)
    {
        _leftDockPanel = leftPanel;
        _rightDockPanel = rightPanel;
        _centralDocumentPanel = centralPanel;
        _dockAutoHideTabFont = dockAutoHideTabFont;
        _dockTabFont = dockTabFont;
        _logger?.LogDebug("DockingLayoutManager now owns: {LeftPanel}, {RightPanel}, {CentralPanel}, {AutoHideFont}, {TabFont}",
            leftPanel != null, rightPanel != null, centralPanel != null, dockAutoHideTabFont != null, dockTabFont != null);
    }

    /// <summary>
    /// Attach this manager to a DockingManager instance
    /// </summary>
    public void AttachTo(DockingManager dockingManager)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));

        dockingManager.DockStateChanged += DockingManager_DockStateChanged;
        dockingManager.DockControlActivated += DockingManager_DockControlActivated;
        dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;

        _logger?.LogInformation("DockingLayoutManager attached to DockingManager");
    }

    /// <summary>
    /// Detach this manager from a DockingManager instance
    /// </summary>
    public void DetachFrom(DockingManager dockingManager)
    {
        if (dockingManager == null) return;

        try { dockingManager.DockStateChanged -= DockingManager_DockStateChanged; } catch { }
        try { dockingManager.DockControlActivated -= DockingManager_DockControlActivated; } catch { }
        try { dockingManager.DockVisibilityChanged -= DockingManager_DockVisibilityChanged; } catch { }

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

            if (!File.Exists(layoutPath))
            {
                _logger?.LogInformation("Docking layout file not found - using default layout ({Path})", layoutPath);
                return;
            }

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
    /// Save docking layout to disk
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

            // Count docked controls by checking for panels that are docked
            int dockedControlCount = 0;
            foreach (Control ctrl in hostForm.Controls)
            {
                if (ctrl is Panel panel && ctrl.Visible)
                {
                    try
                    {
                        // Check if this panel is managed by DockingManager
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

            // Save dock state using Syncfusion API (wrapped in try-catch for Syncfusion internal errors)
            var serializerType = typeof(AppStateSerializer);
            var serializer = Activator.CreateInstance(serializerType, new object[] {
                Syncfusion.Runtime.Serialization.SerializeMode.BinaryFile,
                tempPath
            })!;

            var saveMethod = dockingManager.GetType().GetMethod("SaveDockState", new Type[] { serializerType });

            if (saveMethod != null)
            {
                saveMethod.Invoke(dockingManager, new object[] { serializer });

                // CRITICAL: Call PersistNow() to actually write the serializer to disk
                var persistMethod = serializerType.GetMethod("PersistNow");
                if (persistMethod != null)
                {
                    persistMethod.Invoke(serializer, null);
                    _logger?.LogDebug("Saved and persisted dock state to temp file {TempPath}", tempPath);
                }
                else
                {
                    _logger?.LogWarning("PersistNow method not found - layout may not be persisted correctly");
                }
            }
            else
            {
                _logger?.LogWarning("SaveDockState method not found - layout will not be persisted");
                return;
            }

            // Save dynamic panels metadata (still use XML for metadata)
            SaveDynamicPanels(layoutPath);

            // Atomic file replacement
            ReplaceDockingLayoutFile(tempPath, binaryLayoutPath);

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
    public void ApplyThemeToDockingPanels(DockingManager? dockingManager, Panel? leftPanel, Panel? rightPanel, Panel? centralPanel)
    {
        try
        {
            if (dockingManager != null)
            {
                _logger?.LogDebug("DockingManager uses cascaded theme from ApplicationVisualTheme");
            }

            ApplyPanelTheme(leftPanel);
            ApplyPanelTheme(rightPanel);
            ApplyPanelTheme(centralPanel);

            _logger?.LogInformation("Applied SkinManager theme to docking panels");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme to docking panels - using default colors");
        }
    }

    /// <summary>
    /// Ensure central panel visibility and z-order
    /// </summary>
    public void EnsureCentralPanelVisibility(Panel? centralPanel, Panel? leftPanel, Panel? rightPanel)
    {
        try
        {
            EnsureCentralPanelVisible(centralPanel);
            EnsureSidePanelsZOrder(leftPanel, rightPanel);
            _logger?.LogDebug("Central panel visibility ensured for docked layout");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure central panel visibility in docked layout");
        }
    }

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

        var serializerType = typeof(AppStateSerializer);
        var serializer = Activator.CreateInstance(serializerType, new object[] {
            Syncfusion.Runtime.Serialization.SerializeMode.BinaryFile,
            layoutPath
        })!;

        var loadMethod = dockingManager.GetType().GetMethod("LoadDockState", new Type[] { serializerType });

        if (loadMethod != null)
        {
            try
            {
                // Run on thread pool to avoid blocking UI
                bool success = false;
                await Task.Run(() =>
                {
                    var result = loadMethod.Invoke(dockingManager, new object[] { serializer });
                    // LoadDockState returns bool indicating success
                    if (result is bool boolResult)
                    {
                        success = boolResult;
                    }
                });

                if (success)
                {
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
        else
        {
            _logger?.LogWarning("LoadDockState method not found - using default layout");
        }
    }

    /// <summary>
    /// Re-docks left and right panels programmatically using default positions.
    /// Used as a fallback when LoadDockState fails or layout file is missing/corrupt.
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
                _logger?.LogDebug("Re-docking {PanelName} to Left with width 280px", _leftDockPanel.Name);
                dockingManager.DockControl(_leftDockPanel, parentForm, DockingStyle.Left, 280);
                dockingManager.SetEnableDocking(_leftDockPanel, true);
                dockingManager.SetDockLabel(_leftDockPanel, "Dashboard");
            }

            // Re-dock right panel (Activity) if it exists
            if (_rightDockPanel != null)
            {
                _logger?.LogDebug("Re-docking {PanelName} to Right with width 280px", _rightDockPanel.Name);
                dockingManager.DockControl(_rightDockPanel, parentForm, DockingStyle.Right, 280);
                dockingManager.SetEnableDocking(_rightDockPanel, true);
                dockingManager.SetDockLabel(_rightDockPanel, "Activity");
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
            // STEP 1: Create empty panel
            var panel = new Panel { Name = panelInfo.Name };

            // STEP 2-4: Dock panel FIRST (Syncfusion official pattern)
            // Set up docking if DockingManager is available
            if (dockingManager != null)
            {
                // Dock the panel BEFORE adding content (position will be restored by LoadDockState)
                dockingManager.DockControl(panel, parentForm, Syncfusion.Windows.Forms.Tools.DockingStyle.Left, 200);
                dockingManager.SetEnableDocking(panel, true);
                dockingManager.SetDockLabel(panel, panelInfo.DockLabel ?? panelInfo.Name);
                if (panelInfo.IsAutoHide)
                {
                    dockingManager.SetAutoHideMode(panel, true);
                }
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
            if (!string.IsNullOrEmpty(dockStyleStr) && Enum.TryParse<DockingStyle>(dockStyleStr, out var parsedStyle))
            {
                dockStyle = parsedStyle;
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

    private static void EnsureCentralPanelVisible(Panel? centralPanel)
    {
        if (centralPanel == null) return;

        try
        {
            centralPanel.Visible = true;
            centralPanel.BringToFront();
            centralPanel.Invalidate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set central document panel visibility: {ex.Message}");
        }
    }

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
        _logger?.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}", e.NewState, e.OldState);
        // Note: Central panel visibility and debounced save would be handled by the form
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger?.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        _logger?.LogDebug("Dock visibility changed");
        // Note: Central panel visibility would be handled by the form
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

            try { _centralDocumentPanel?.Dispose(); } catch { }
            _centralDocumentPanel = null;

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
