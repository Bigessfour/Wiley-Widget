using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
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
    private Dictionary<string, Controls.GradientPanelExt>? _dynamicDockPanels = new();

    // Owned resources - fonts, panels, and diagnostics
    private Font? _dockAutoHideTabFont;
    private Font? _dockTabFont;
    private Controls.GradientPanelExt? _leftDockPanel;
    private Controls.GradientPanelExt? _rightDockPanel;
    // REMOVED: _centralDocumentPanel - Option A pure docking architecture

    public DockingLayoutManager(IServiceProvider serviceProvider, IPanelNavigationService? panelNavigator, ILogger? logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger;

        // Initialize fonts
        _dockAutoHideTabFont = new Font("Segoe UI", 9f);
        _dockTabFont = new Font("Segoe UI", 10f, FontStyle.Bold);

        // Initialize dynamic panels dict
        _dynamicDockPanels = new Dictionary<string, Controls.GradientPanelExt>();

        // Setup save timer
        _dockingLayoutSaveTimer = new System.Windows.Forms.Timer
        {
            Interval = MinimumSaveIntervalMs
        };
        _dockingLayoutSaveTimer.Tick += async (_, _) => await DebounceSaveDockingLayoutAsync();

        _logger?.LogDebug("DockingLayoutManager initialized");
    }

    /// <summary>
    /// Transfer ownership of managed docking panels and fonts to this manager.
    /// </summary>
    /// <param name="leftDockPanel">Left docking panel</param>
    /// <param name="rightDockPanel">Right docking panel</param>
    public void TransferOwnership(Controls.GradientPanelExt leftDockPanel, Controls.GradientPanelExt rightDockPanel)
    {
        _leftDockPanel = leftDockPanel ?? throw new ArgumentNullException(nameof(leftDockPanel));
        _rightDockPanel = rightDockPanel ?? throw new ArgumentNullException(nameof(rightDockPanel));

        // Apply themes via SfSkinManager (no manual colors)
        SfSkinManager.SetVisualStyle(_leftDockPanel, "Office2019Colorful");
        SfSkinManager.SetVisualStyle(_rightDockPanel, "Office2019Colorful");

        _logger?.LogDebug("Transferred ownership of docking panels");
    }

    /// <summary>
    /// Load persisted docking layout from file.
    /// </summary>
    /// <param name="dockingManager">DockingManager instance</param>
    /// <param name="layoutFilePath">Path to layout XML file</param>
    public async Task LoadDockingLayoutAsync(DockingManager dockingManager, string layoutFilePath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(layoutFilePath))
            {
                _logger?.LogInformation("No persisted layout found at {Path} - using default layout", layoutFilePath);
                return;
            }

            // Use BinaryFile serialization mode per Syncfusion best practices
            var binaryLayoutPath = Path.ChangeExtension(layoutFilePath, ".bin");
            if (File.Exists(binaryLayoutPath)) layoutFilePath = binaryLayoutPath;

            var serializer = new AppStateSerializer(SerializeMode.BinaryFile, layoutFilePath);

            // Load layout
            dockingManager.LoadDockState(serializer);

            // Restore dynamic panels (assuming persistence)
            RestoreDynamicPanels(dockingManager);

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > LayoutLoadWarningMs)
            {
                _logger?.LogWarning("Layout load took {ElapsedMs}ms - consider optimizing", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger?.LogInformation("Layout loaded successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load docking layout from {Path}", layoutFilePath);
        }
    }

    /// <summary>
    /// Save current docking layout to file with debounce.
    /// </summary>
    /// <param name="dockingManager">DockingManager instance</param>
    /// <param name="layoutFilePath">Path to save layout XML</param>
    public void SaveDockingLayout(DockingManager dockingManager, string layoutFilePath)
    {
        lock (_dockingSaveLock)
        {
            if (_isSavingLayout || (DateTime.UtcNow - _lastSaveTime).TotalMilliseconds < MinimumSaveIntervalMs)
            {
                _dockingLayoutSaveTimer?.Start();  // Debounce
                return;
            }

            _isSavingLayout = true;
        }

        try
        {
            var binaryLayoutPath = Path.ChangeExtension(layoutFilePath, ".bin");
            var serializer = new AppStateSerializer(SerializeMode.BinaryFile, binaryLayoutPath);

            // Save dock state using Syncfusion API
            dockingManager.SaveDockState(serializer);
            serializer.PersistNow();

            _lastSaveTime = DateTime.UtcNow;
            _logger?.LogDebug("Docking layout saved to {Path}", binaryLayoutPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save docking layout to {Path}", layoutFilePath);
        }
        finally
        {
            _isSavingLayout = false;
        }
    }

    /// <summary>
    /// Debounced save handler.
    /// </summary>
    private async Task DebounceSaveDockingLayoutAsync()
    {
        _dockingLayoutSaveTimer?.Stop();
        // Assuming dockingManager and path are accessible; pass if needed
        // SaveDockingLayout(dockingManager, layoutFilePath);
        await Task.CompletedTask;  // Placeholder for async
    }

    /// <summary>
    /// Restore dynamic panels from persistence.
    /// </summary>
    private void RestoreDynamicPanels(DockingManager dockingManager)
    {
        // Placeholder: Load from XML or config, create panels
        foreach (var info in GetPersistedDynamicPanels())  // Assume method to fetch
        {
            var panel = new Controls.GradientPanelExt { Name = info.Name };
            dockingManager.DockControl(panel, null, DockingStyle.Left, 200);  // Example
            _dynamicDockPanels?.Add(info.Name ?? string.Empty, panel);
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

            // Dispose managed panels
            try { _leftDockPanel?.Dispose(); } catch { }
            _leftDockPanel = null;

            try { _rightDockPanel?.Dispose(); } catch { }
            _rightDockPanel = null;

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
