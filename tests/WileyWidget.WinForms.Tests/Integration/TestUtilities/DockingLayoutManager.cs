using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services;

public sealed class DockingLayoutManager : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPanelNavigationService? _panelNavigator;
    private readonly ILogger _logger;
    private readonly string _layoutPath;
    private readonly Form _ownerForm;
    private bool _disposed;

    private static byte[]? _layoutCache;

    public DockingLayoutManager(
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger logger,
        string layoutPath,
        Form ownerForm,
        DockingManager dockingManager,
        Control? leftPanel,
        Control? rightPanel,
        Control? centralPanel,
        object? activityTimer)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _layoutPath = string.IsNullOrWhiteSpace(layoutPath)
            ? Path.Combine(Path.GetTempPath(), "DockingLayoutV2.bin")
            : layoutPath;
        _ownerForm = ownerForm ?? throw new ArgumentNullException(nameof(ownerForm));

        _ = dockingManager;
        _ = leftPanel;
        _ = rightPanel;
        _ = centralPanel;
        _ = activityTimer;
    }

    public void SaveDockingLayout(DockingManager dockingManager)
    {
        ArgumentNullException.ThrowIfNull(dockingManager);
        ThrowIfDisposed();

        var payload = CreateLayoutPayload(dockingManager);
        _layoutCache = Compress(payload);

        var directory = Path.GetDirectoryName(_layoutPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_layoutPath, _layoutCache);
    }

    public Task LoadDockingLayoutAsync(DockingManager dockingManager)
    {
        ArgumentNullException.ThrowIfNull(dockingManager);
        ThrowIfDisposed();

        try
        {
            byte[]? bytes = _layoutCache;
            if (bytes == null && File.Exists(_layoutPath))
            {
                bytes = File.ReadAllBytes(_layoutPath);
                _layoutCache = bytes;
            }

            if (bytes != null && bytes.Length >= 2)
            {
                _ = TryDecompress(bytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LoadDockingLayoutAsync ignored layout read/decode failure");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _serviceProvider;
        _ = _panelNavigator;
        _ = _ownerForm;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DockingLayoutManager));
        }
    }

    private static byte[] CreateLayoutPayload(DockingManager dockingManager)
    {
        var hostName = dockingManager.HostControl?.Name ?? "<none>";
        var content = $"{{\"host\":\"{hostName}\",\"savedUtc\":\"{DateTime.UtcNow:O}\"}}";
        return Encoding.UTF8.GetBytes(content);
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static string? TryDecompress(byte[] bytes)
    {
        if (bytes.Length < 2 || bytes[0] != 0x1f || bytes[1] != 0x8b)
        {
            return null;
        }

        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
