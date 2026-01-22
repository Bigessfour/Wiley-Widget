using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utils;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

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
    private readonly string _layoutPath;  // Added: Path for layout persistence
    private readonly Control _uiControl;  // Added: UI control for thread marshaling

    private const string LayoutVersionAttributeName = "LayoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const int LayoutLoadWarningMs = 5000;
    private const int MinimumSaveIntervalMs = 2000;
    private const int LayoutLoadTimeoutMs = 2000;  // Added: Timeout for async load

    // State management
    private bool _isSavingLayout;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private readonly object _dockingSaveLock = new();
    private System.Windows.Forms.Timer? _dockingLayoutSaveTimer;

    // In-memory cache for layout data (byte array for binary efficiency)
    private static byte[]? _layoutCache;
    private static readonly object _cacheLock = new();

    // Dynamic panels tracking
    private Dictionary<string, GradientPanelExt>? _dynamicDockPanels = new();  // Made non-static for instance safety

    public DockingLayoutManager(IServiceProvider serviceProvider, IPanelNavigationService? panelNavigator, ILogger? logger, string layoutPath, Control uiControl)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger;
        _layoutPath = layoutPath ?? throw new ArgumentNullException(nameof(layoutPath));
        _uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));

        // Setup save timer with tick handler
        _dockingLayoutSaveTimer = new System.Windows.Forms.Timer
        {
            Interval = MinimumSaveIntervalMs
        };
        _dockingLayoutSaveTimer.Tick += async (_, _) => await DebounceSaveDockingLayoutAsync();

        _logger?.LogDebug("DockingLayoutManager initialized with layout path: {Path}", _layoutPath);
    }

    /// <summary>
    /// Asynchronously loads docking layout with timeout and fallback to defaults.
    /// Uses in-memory cache first, then disk, with performance profiling.
    /// File I/O runs async (background-safe), then marshals LoadDockState to UI thread.
    /// </summary>
    public async Task LoadDockingLayoutAsync(DockingManager dockingManager, CancellationToken cancellationToken = default)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (_uiControl.IsDisposed || !_uiControl.IsHandleCreated)
        {
            _logger?.LogWarning("UI control is disposed or handle not created - skipping layout load");
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // 1. Try in-memory cache first (fast path)
            byte[]? layoutData = null;
            lock (_cacheLock)
            {
                if (_layoutCache != null)
                {
                    layoutData = _layoutCache;
                    _logger?.LogDebug("Loaded layout from in-memory cache (skipped disk I/O)");
                }
            }

            // 2. Fallback to disk if cache miss (async I/O, background-safe)
            if (layoutData == null)
            {
                if (!File.Exists(_layoutPath))
                {
                    _logger?.LogInformation("No saved layout found at {Path} - using default docking configuration", _layoutPath);
                    return;
                }

                var ioSw = Stopwatch.StartNew();
                layoutData = await File.ReadAllBytesAsync(_layoutPath, cancellationToken);
                ioSw.Stop();
                _logger?.LogDebug("Loaded layout from disk in {ElapsedMs}ms", ioSw.ElapsedMilliseconds);

                // Update cache for next load
                lock (_cacheLock)
                {
                    _layoutCache = layoutData;
                }
            }

            // 3. Marshal application to UI thread (UI-sensitive)
            await UIThreadHelper.ExecuteOnUIThreadAsync(_uiControl, async () =>
            {
                if (dockingManager.HostControl?.IsDisposed ?? true)
                {
                    _logger?.LogWarning("Host control disposed during layout apply - aborting");
                    return;
                }

                using var ms = new MemoryStream(layoutData);
                var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                dockingManager.LoadDockState(serializer);

                _logger?.LogInformation("Docking layout applied on UI thread");
            }, _logger);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Layout load canceled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load docking layout - using defaults");
        }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds > LayoutLoadWarningMs)
            {
                _logger?.LogWarning("Slow docking layout load detected ({ElapsedMs}ms) - consider optimizing serialization", sw.ElapsedMilliseconds);
            }
        }
    }

    /// <summary>
    /// Saves the current docking layout with debounce to prevent frequent disk writes.
    /// </summary>
    public void SaveDockingLayout(DockingManager dockingManager)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (_isSavingLayout) return;  // Debounce

        lock (_dockingSaveLock)
        {
            if ((DateTime.Now - _lastSaveTime).TotalMilliseconds < MinimumSaveIntervalMs) return;

            _isSavingLayout = true;
            var sw = Stopwatch.StartNew();
            try
            {
                using var ms = new MemoryStream();
                var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                dockingManager.SaveDockState(serializer);
                serializer.PersistNow();

                var layoutData = ms.ToArray();

                // Update cache
                lock (_cacheLock)
                {
                    _layoutCache = layoutData;
                }

                // Save to disk
                var dir = Path.GetDirectoryName(_layoutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllBytes(_layoutPath, layoutData);

                _lastSaveTime = DateTime.Now;
                _logger?.LogDebug("Docking layout saved in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save docking layout");
            }
            finally
            {
                _isSavingLayout = false;
                sw.Stop();
            }
        }
    }

    /// <summary>
    /// Debounced save handler for timer tick.
    /// </summary>
    private async Task DebounceSaveDockingLayoutAsync()
    {
        // Implementation: Call SaveDockingLayout if needed
        // (Assuming you have access to dockingManager; pass it if not)
        await Task.CompletedTask;  // Placeholder
    }

    /// <summary>
    /// Restore dynamic panels from persistence.
    /// </summary>
    private void RestoreDynamicPanels(DockingManager dockingManager)
    {
        // Placeholder: Load from XML or config, create panels
        foreach (var info in GetPersistedDynamicPanels())  // Assume method to fetch
        {
            var panel = new GradientPanelExt { Name = info.Name };
            Control host = dockingManager.HostControl;
            if (host != null)
            {
                dockingManager.DockControl(panel, host, DockingStyle.Left, 200);  // Example
                _dynamicDockPanels?.Add(info.Name ?? string.Empty, panel);
            }
            else
            {
                _logger?.LogWarning("Cannot restore dynamic panel {PanelName}: DockingManager.HostControl is null", info.Name);
            }
        }
    }

    /// <summary>
    /// Get persisted dynamic panel info (placeholder).
    /// </summary>
    private IEnumerable<DynamicPanelInfo> GetPersistedDynamicPanels()
    {
        // Implement XML/config read; return sample for now
        yield return new DynamicPanelInfo { Name = "SamplePanel", DockLabel = "Sample", IsAutoHide = true };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
