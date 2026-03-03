using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Workspace layout persistence service.
/// Uses Syncfusion's native <see cref="AppStateSerializer"/> to persist and restore
/// docking positions and ribbon state via <see cref="DockingManager.SaveDockState"/> /
/// <see cref="DockingManager.LoadDockState"/> and <see cref="RibbonControlAdv.SaveState"/> /
/// <see cref="RibbonControlAdv.LoadState"/>.
///
/// Wire-up:  Call <see cref="Configure"/> once after chrome has been built so the service
/// holds references to the live controls.  Then call <see cref="SaveLayout"/> /
/// <see cref="LoadLayout"/> as needed (e.g., on form closing / form shown).
/// </summary>
public sealed class WorkspaceLayoutService : IWorkspaceLayoutService, IDisposable
{
    private readonly ILogger<WorkspaceLayoutService> _logger;
    private readonly string _layoutPath;

    private DockingManager? _dockingManager;
    private RibbonControlAdv? _ribbon;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkspaceLayoutService"/>.
    /// </summary>
    public WorkspaceLayoutService(ILogger<WorkspaceLayoutService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _layoutPath = BuildAutoSavePath();
    }

    /// <inheritdoc />
    public void Configure(DockingManager dockingManager, RibbonControlAdv ribbon)
    {
        _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        _ribbon = ribbon ?? throw new ArgumentNullException(nameof(ribbon));
        _logger.LogDebug("WorkspaceLayoutService configured with live controls");
    }

    /// <inheritdoc />
    public void SaveLayout()
    {
        if (!IsConfigured()) return;

        try
        {
            EnsureDirectory();

            var serializer = new AppStateSerializer(SerializeMode.XMLFile, _layoutPath);
            _dockingManager!.SaveDockState(serializer);
            _ribbon!.SaveState(serializer);
            serializer.PersistNow();

            _logger.LogInformation("Workspace layout saved to {Path}", _layoutPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workspace layout");
        }
    }

    /// <inheritdoc />
    public void LoadLayout()
    {
        if (!IsConfigured()) return;
        if (!File.Exists(_layoutPath))
        {
            _logger.LogDebug("No saved layout found at {Path} — skipping load (first run)", _layoutPath);
            return;
        }

        try
        {
            var serializer = new AppStateSerializer(SerializeMode.XMLFile, _layoutPath);
            _dockingManager!.LoadDockState(serializer);
            _ribbon!.LoadState(serializer);
            _ribbon.FindForm()?.PerformLayout();

            _logger.LogInformation("Workspace layout loaded from {Path}", _layoutPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace layout — reverting to default state");
            TryDeleteLayoutFile();
        }
    }

    /// <inheritdoc />
    public void ResetLayout()
    {
        TryDeleteLayoutFile();
        _logger.LogInformation("Workspace layout reset (auto-save file deleted)");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dockingManager = null;
        _ribbon = null;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private bool IsConfigured()
    {
        if (_dockingManager != null && _ribbon != null) return true;
        _logger.LogWarning("WorkspaceLayoutService.Configure() has not been called; layout operation skipped");
        return false;
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(_layoutPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void TryDeleteLayoutFile()
    {
        try
        {
            if (File.Exists(_layoutPath))
            {
                File.Delete(_layoutPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not delete layout file at {Path}", _layoutPath);
        }
    }

    private static string BuildAutoSavePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WileyWidget", "Layout", "AutoSave.xml");
}
