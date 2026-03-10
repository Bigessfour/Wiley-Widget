using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Layout persistence for MainForm using Syncfusion AppStateSerializer.
/// Saves and restores window state, ribbon layout, MDI documents, and panel positions.
///
/// SYNCFUSION API: AppStateSerializer
/// Reference: https://help.syncfusion.com/windowsforms/serialization/overview
/// </summary>
public partial class MainForm
{
    private const string LayoutDirectory = "Layouts";
    private const string DefaultLayoutFile = "default.xml";
    private const string AutoSaveLayoutFile = "autosave.xml";
    private readonly Dictionary<string, string> _namedWorkspaces = new();

    /// <summary>
    /// Saves the current workspace layout to file.
    /// </summary>
    private void SaveWorkspaceLayout(string? layoutName = null)
    {
        try
        {
            var layoutPath = GetLayoutFilePath(layoutName ?? DefaultLayoutFile);
            EnsureLayoutDirectoryExists();

            _logger?.LogInformation("Saving workspace layout to {LayoutPath}", layoutPath);

            // Create serializer
            var serializer = new AppStateSerializer(SerializeMode.XMLFile, layoutPath);

            // Save main form state
            SaveMainFormState(serializer);

            // Save ribbon state
            SaveRibbonState(serializer);

            InitializeMDIManager();
            if (_tabbedMdi != null)
            {
                _tabbedMdi.SaveTabGroupStates(serializer);
            }

            // Save status bar state
            SaveStatusBarState(serializer);

            // Persist to disk
            serializer.PersistNow();

            _logger?.LogInformation("Workspace layout saved successfully");
            ApplyStatus($"Layout saved: {layoutName ?? "default"}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save workspace layout");
            ApplyStatus("Error saving layout");
        }
    }

    /// <summary>
    /// Loads a previously saved workspace layout.
    /// </summary>
    private void LoadWorkspaceLayout(string? layoutName = null)
    {
        try
        {
            var layoutPath = GetLayoutFilePath(layoutName ?? DefaultLayoutFile);

            if (!File.Exists(layoutPath))
            {
                _logger?.LogDebug("Layout file not found: {LayoutPath}", layoutPath);
                return;
            }

            _logger?.LogInformation("Loading workspace layout from {LayoutPath}", layoutPath);
            TraceLayoutSnapshot("LoadWorkspaceLayout.BeforeRestore");

            var serializer = new AppStateSerializer(SerializeMode.XMLFile, layoutPath);

            // Restore main form state
            RestoreMainFormState(serializer);

            // Restore ribbon state
            RestoreRibbonState(serializer);

            InitializeMDIManager();
            if (_tabbedMdi != null)
            {
                _tabbedMdi.LoadTabGroupStates(serializer);
            }

            // Restore status bar state
            RestoreStatusBarState(serializer);

            // Layout restore can recreate or reparent themed surfaces. Reapply the current
            // theme without mutating persisted settings so the visual tree converges again.
            _themeService?.ReapplyCurrentTheme();
            _panelHost?.PerformLayout();
            TraceLayoutSnapshot("LoadWorkspaceLayout.AfterRestore");

            _logger?.LogInformation("Workspace layout loaded successfully");
            ApplyStatus($"Layout loaded: {layoutName ?? "default"}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load workspace layout");
            ApplyStatus("Error loading layout");
        }
    }

    /// <summary>
    /// Saves main form state (size, location, window state).
    /// </summary>
    private void SaveMainFormState(AppStateSerializer serializer)
    {
        serializer.SerializeObject("MainForm.Size", this.Size);
        serializer.SerializeObject("MainForm.Location", this.Location);
        serializer.SerializeObject("MainForm.WindowState", this.WindowState);
        serializer.SerializeObject("MainForm.IsMDIContainer", this.IsMdiContainer);

        _logger?.LogDebug("Saved main form state: Size={Size}, Location={Location}, WindowState={State}",
            this.Size, this.Location, this.WindowState);
    }

    /// <summary>
    /// Restores main form state.
    /// </summary>
    private void RestoreMainFormState(AppStateSerializer serializer)
    {
        try
        {
            var sizeObj = serializer.DeserializeObject("MainForm.Size", this.Size);
            var locationObj = serializer.DeserializeObject("MainForm.Location", this.Location);
            var windowStateObj = serializer.DeserializeObject("MainForm.WindowState", this.WindowState);

            var size = (Size)(sizeObj ?? this.Size);
            var location = (Point)(locationObj ?? this.Location);
            var windowState = (FormWindowState)(windowStateObj ?? this.WindowState);

            // Validate screen bounds before restoring
            var screen = Screen.FromPoint(location);
            if (screen.WorkingArea.Contains(location))
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = location;
                this.Size = size;
            }

            this.WindowState = windowState;

            _logger?.LogDebug("Restored main form state: Size={Size}, Location={Location}, WindowState={State}",
                size, location, windowState);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error restoring main form state");
        }
    }

    /// <summary>
    /// Saves ribbon state (selected tab, custom QAT).
    /// </summary>
    private void SaveRibbonState(AppStateSerializer serializer)
    {
        if (_ribbon == null || serializer == null) return;

        try
        {
            _ribbon.SaveState(serializer);
            _logger?.LogDebug("Ribbon full state saved via SaveState()");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save Ribbon state");
        }
    }

    /// <summary>
    /// Restores ribbon state.
    /// </summary>
    private void RestoreRibbonState(AppStateSerializer serializer)
    {
        if (_ribbon == null || serializer == null) return;

        try
        {
            _ribbon.LoadState(serializer);
            _logger?.LogDebug("Ribbon full state restored via LoadState()");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore Ribbon state");
        }
    }

    /// <summary>
    /// Saves status bar state.
    /// </summary>
    private void SaveStatusBarState(AppStateSerializer serializer)
    {
        try
        {
            // Save status bar visibility and text
            serializer.SerializeObject("StatusBar.Visible", _statusBar?.Visible ?? true);

            _logger?.LogDebug("Saved status bar state");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error saving status bar state");
        }
    }

    /// <summary>
    /// Restores status bar state.
    /// </summary>
    private void RestoreStatusBarState(AppStateSerializer serializer)
    {
        try
        {
            var visibleObj = serializer.DeserializeObject("StatusBar.Visible", true);
            var visible = (bool)(visibleObj ?? true);
            if (_statusBar != null)
            {
                _statusBar.Visible = visible;
            }

            _logger?.LogDebug("Restored status bar state");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error restoring status bar state");
        }
    }

    /// <summary>
    /// Resets layout to default.
    /// </summary>
    private void ResetLayoutToDefault()
    {
        try
        {
            _logger?.LogInformation("Resetting layout to default");

            // Close all MDI documents
            CloseAllDocuments();

            // Reset main form to default position/size
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(
                (int)DpiAware.LogicalToDeviceUnits(1400f),
                (int)DpiAware.LogicalToDeviceUnits(900f));
            this.CenterToScreen();

            // Reset ribbon to default tab
            if (_ribbon != null && _ribbon.Header.MainItems.Count > 0)
            {
                _ribbon.SelectedTab = (ToolStripTabItem)_ribbon.Header.MainItems[0];
                // RibbonControlAdv may not support RibbonState property directly
            }

            ApplyStatus("Layout reset to default");
            _logger?.LogInformation("Layout reset successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reset layout");
            ApplyStatus("Error resetting layout");
        }
    }

    /// <summary>
    /// Gets the full path to a layout file.
    /// </summary>
    private string GetLayoutFilePath(string layoutFileName)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var layoutDir = Path.Combine(appDataPath, "WileyWidget", LayoutDirectory);
        return Path.Combine(layoutDir, layoutFileName);
    }

    /// <summary>
    /// Ensures the layout directory exists.
    /// </summary>
    private void EnsureLayoutDirectoryExists()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var layoutDir = Path.Combine(appDataPath, "WileyWidget", LayoutDirectory);

        if (!Directory.Exists(layoutDir))
        {
            Directory.CreateDirectory(layoutDir);
            _logger?.LogDebug("Created layout directory: {LayoutDir}", layoutDir);
        }
    }

    /// <summary>
    /// Auto-saves layout on form closing.
    /// </summary>
    private void AutoSaveLayoutOnClosing()
    {
        try
        {
            SaveWorkspaceLayout(AutoSaveLayoutFile);
            _logger?.LogDebug("Auto-saved layout on form closing");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error auto-saving layout on close");
        }
    }

}
