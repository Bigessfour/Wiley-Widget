using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Handles persistence and restoration of docking panel layout state.
/// </summary>
public sealed class DockingLayoutManager : IDisposable
{
    private sealed record DockingLayoutSnapshot(
        int LeftWidth,
        int RightWidth,
        bool LeftVisible,
        bool RightVisible,
        bool CentralVisible);

    private readonly string _layoutPath;
    private readonly Form _hostForm;
    private readonly Control? _leftDockPanel;
    private readonly Control? _rightDockPanel;
    private readonly Control? _centralDocumentPanel;
    private readonly ILogger _logger;
    private bool _disposed;

    private static DockingLayoutSnapshot? _layoutCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockingLayoutManager"/> class.
    /// </summary>
    public DockingLayoutManager(
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigationService,
        ILogger logger,
        string layoutPath,
        Form hostForm,
        DockingManager dockingManager,
        Control? leftDockPanel,
        Control? rightDockPanel,
        Control? centralDocumentPanel,
        System.Windows.Forms.Timer? activityRefreshTimer)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ = panelNavigationService;
        _ = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        _ = activityRefreshTimer;

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _layoutPath = string.IsNullOrWhiteSpace(layoutPath)
            ? throw new ArgumentException("Layout path cannot be empty.", nameof(layoutPath))
            : layoutPath;
        _hostForm = hostForm ?? throw new ArgumentNullException(nameof(hostForm));
        _leftDockPanel = leftDockPanel;
        _rightDockPanel = rightDockPanel;
        _centralDocumentPanel = centralDocumentPanel;
    }

    /// <summary>
    /// Saves current docking-related panel state to compressed layout file.
    /// </summary>
    public void SaveDockingLayout(DockingManager dockingManager)
    {
        _ = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));

        if (_disposed)
        {
            return;
        }

        try
        {
            var snapshot = CaptureSnapshot();
            _layoutCache = snapshot;

            var directory = Path.GetDirectoryName(_layoutPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.Serialize(snapshot);
            var bytes = Encoding.UTF8.GetBytes(payload);

            using var fileStream = new FileStream(_layoutPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
            gzip.Write(bytes, 0, bytes.Length);
            gzip.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save docking layout to {LayoutPath}", _layoutPath);
        }
    }

    /// <summary>
    /// Loads docking-related panel state from memory cache or compressed layout file.
    /// </summary>
    public async Task LoadDockingLayoutAsync(DockingManager dockingManager)
    {
        _ = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));

        if (_disposed)
        {
            return;
        }

        try
        {
            var snapshot = _layoutCache ?? await ReadSnapshotFromFileAsync().ConfigureAwait(false);
            if (snapshot is null)
            {
                return;
            }

            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load docking layout from {LayoutPath}", _layoutPath);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    private DockingLayoutSnapshot CaptureSnapshot()
    {
        return new DockingLayoutSnapshot(
            LeftWidth: SafeWidth(_leftDockPanel),
            RightWidth: SafeWidth(_rightDockPanel),
            LeftVisible: SafeVisible(_leftDockPanel),
            RightVisible: SafeVisible(_rightDockPanel),
            CentralVisible: SafeVisible(_centralDocumentPanel));
    }

    private static int SafeWidth(Control? control)
    {
        return control is { IsDisposed: false } ? control.Width : 0;
    }

    private static bool SafeVisible(Control? control)
    {
        return control is { IsDisposed: false } && control.Visible;
    }

    private async Task<DockingLayoutSnapshot?> ReadSnapshotFromFileAsync()
    {
        if (!File.Exists(_layoutPath))
        {
            return null;
        }

        await using var fileStream = new FileStream(_layoutPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var memory = new MemoryStream();
        await gzip.CopyToAsync(memory).ConfigureAwait(false);

        var json = Encoding.UTF8.GetString(memory.ToArray());
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<DockingLayoutSnapshot>(json);
        _layoutCache = snapshot;
        return snapshot;
    }

    private void ApplySnapshot(DockingLayoutSnapshot snapshot)
    {
        if (_hostForm.IsDisposed)
        {
            return;
        }

        void Apply()
        {
            if (_hostForm.IsDisposed)
            {
                return;
            }

            ApplyToControl(_leftDockPanel, snapshot.LeftWidth, snapshot.LeftVisible);
            ApplyToControl(_rightDockPanel, snapshot.RightWidth, snapshot.RightVisible);
            ApplyToControl(_centralDocumentPanel, null, snapshot.CentralVisible);
            _hostForm.PerformLayout();
            _hostForm.Invalidate(true);
        }

        if (_hostForm.InvokeRequired)
        {
            _hostForm.Invoke(new MethodInvoker(Apply));
            return;
        }

        Apply();
    }

    private static void ApplyToControl(Control? control, int? width, bool visible)
    {
        if (control is null || control.IsDisposed)
        {
            return;
        }

        if (width.HasValue && width.Value > 0)
        {
            control.Width = width.Value;
        }

        control.Visible = visible;
    }
}
