using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
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

    private AppStateSerializer? _workspaceSerializer;
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

            // Save MDI documents
            SaveMDIDocumentState(serializer);

            InitializeMDIManager();
            if (_mdiManager != null)
            {
                _mdiManager.SaveTabGroupStates(serializer);
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

            var serializer = new AppStateSerializer(SerializeMode.XMLFile, layoutPath);

            // Restore main form state
            RestoreMainFormState(serializer);

            // Restore ribbon state
            RestoreRibbonState(serializer);

            InitializeMDIManager();
            if (_mdiManager != null)
            {
                _mdiManager.LoadTabGroupStates(serializer);
            }

            // Restore MDI documents
            RestoreMDIDocumentState(serializer);

            // Restore status bar state
            RestoreStatusBarState(serializer);

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
        if (_ribbon == null) return;

        try
        {
            serializer.SerializeObject("Ribbon.SelectedTab", _ribbon.SelectedTab?.Name ?? string.Empty);
            serializer.SerializeObject("Ribbon.QuickPanelVisible", _ribbon.QuickPanelVisible);

            _logger?.LogDebug("Saved ribbon state: SelectedTab={Tab}",
                _ribbon.SelectedTab?.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error saving ribbon state");
        }
    }

    /// <summary>
    /// Restores ribbon state.
    /// </summary>
    private void RestoreRibbonState(AppStateSerializer serializer)
    {
        if (_ribbon == null) return;

        try
        {
            var selectedTabNameObj = serializer.DeserializeObject("Ribbon.SelectedTab", string.Empty);
            var qatVisibleObj = serializer.DeserializeObject("Ribbon.QuickPanelVisible", true);

            var selectedTabName = (string)(selectedTabNameObj ?? string.Empty);
            var qatVisible = (bool)(qatVisibleObj ?? true);

            // Restore selected tab
            if (!string.IsNullOrEmpty(selectedTabName))
            {
                foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
                {
                    if (string.Equals(tab.Name, selectedTabName, StringComparison.OrdinalIgnoreCase))
                    {
                        _ribbon.SelectedTab = tab;
                        break;
                    }
                }
            }

            // Restore QAT visibility
            _ribbon.QuickPanelVisible = qatVisible;

            _logger?.LogDebug("Restored ribbon state: SelectedTab={Tab}",
                selectedTabName);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error restoring ribbon state");
        }
    }

    /// <summary>
    /// Saves open MDI documents and their positions.
    /// </summary>
    private void SaveMDIDocumentState(AppStateSerializer serializer)
    {
        if (!this.IsMdiContainer || this.MdiChildren == null) return;

        try
        {
            var openPanels = new List<string>();

            var mdiChildren = _mdiManager?.MdiChildren ?? this.MdiChildren;

            foreach (var child in mdiChildren)
            {
                if (child.IsDisposed) continue;

                var panelName = child.Text;
                openPanels.Add(panelName);

                // Save individual document state
                serializer.SerializeObject($"MDIDoc.{panelName}.Location", child.Location);
                serializer.SerializeObject($"MDIDoc.{panelName}.Size", child.Size);
                serializer.SerializeObject($"MDIDoc.{panelName}.WindowState", child.WindowState);
            }

            serializer.SerializeObject("MDI.OpenPanels", openPanels.ToArray());

            _logger?.LogDebug("Saved {Count} MDI documents", openPanels.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error saving MDI document state");
        }
    }

    /// <summary>
    /// Restores open MDI documents.
    /// </summary>
    private void RestoreMDIDocumentState(AppStateSerializer serializer)
    {
        if (!this.IsMdiContainer) return;

        try
        {
            var openPanelsObj = serializer.DeserializeObject("MDI.OpenPanels", Array.Empty<string>());
            var openPanels = (string[])(openPanelsObj ?? Array.Empty<string>());

            foreach (var panelName in openPanels)
            {
                try
                {
                    // Find panel in registry
                    var panel = Services.PanelRegistry.Panels
                        .FirstOrDefault(p => string.Equals(p.DisplayName, panelName, StringComparison.OrdinalIgnoreCase));

                    if (panel != null)
                    {
                        // Reopen panel (will be handled by navigation service)
                        ShowPanel(panel.PanelType, panel.DisplayName, panel.DefaultDock);

                        // Restore document state
                        var locationObj = serializer.DeserializeObject($"MDIDoc.{panelName}.Location", Point.Empty);
                        var sizeObj = serializer.DeserializeObject($"MDIDoc.{panelName}.Size", Size.Empty);
                        var windowStateObj = serializer.DeserializeObject($"MDIDoc.{panelName}.WindowState", FormWindowState.Normal);

                        var location = (Point)(locationObj ?? Point.Empty);
                        var size = (Size)(sizeObj ?? Size.Empty);
                        var windowState = (FormWindowState)(windowStateObj ?? FormWindowState.Normal);

                        // Apply restored state to newly opened document
                        var mdiChildren = _mdiManager?.MdiChildren ?? this.MdiChildren;
                        var mdiChild = mdiChildren.LastOrDefault();
                        if (mdiChild != null)
                        {
                            if (windowState != FormWindowState.Maximized)
                            {
                                mdiChild.WindowState = FormWindowState.Normal;
                                mdiChild.Location = location;
                                mdiChild.Size = size;
                            }
                            mdiChild.WindowState = windowState;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error restoring MDI document: {PanelName}", panelName);
                }
            }

            _logger?.LogDebug("Restored {Count} MDI documents", openPanels.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error restoring MDI document state");
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
            this.Size = new Size(1400, 900);
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

    /// <summary>
    /// Auto-loads layout on form shown.
    /// </summary>
    private void AutoLoadLayoutOnShown()
    {
        try
        {
            LoadWorkspaceLayout(AutoSaveLayoutFile);
            _logger?.LogDebug("Auto-loaded layout on form shown");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error auto-loading layout on shown");
        }
    }
}
