using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Quick Access Toolbar (QAT) implementation for RibbonControlAdv.
/// Provides frequently-used commands above the ribbon tabs.
/// 
/// SYNCFUSION API: RibbonControlAdv.QuickPanel
/// Reference: https://help.syncfusion.com/windowsforms/ribbon/quick-access-toolbar
/// </summary>
public partial class MainForm
{
    /// <summary>
    /// Initializes Quick Access Toolbar with common commands.
    /// 
    /// SYNCFUSION API PROPERTIES:
    /// - QuickPanelVisible: Show/hide QAT
    /// - QuickPanelItems: Collection of QAT items
    /// - ShowQuickItemsCustomizeMenu: Allow user customization
    /// - QuickPanelImageLayout: Icon layout
    /// </summary>
    private void InitializeQuickAccessToolbar()
    {
        if (_ribbon == null)
        {
            _logger?.LogWarning("Cannot initialize QAT - ribbon is null");
            return;
        }

        try
        {
            _logger?.LogInformation("Initializing Quick Access Toolbar");

            // Enable Quick Access Toolbar
            _ribbon.QuickPanelVisible = true;

            _logger?.LogInformation("Quick Access Toolbar initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Quick Access Toolbar");
        }
    }

    /// <summary>
    /// Refreshes the active panel by finding it and triggering a refresh command.
    /// </summary>
    private void RefreshActivePanel()
    {
        try
        {
            var activeChild = this.ActiveMdiChild;
            if (activeChild == null)
            {
                _logger?.LogDebug("No active MDI child to refresh");
                return;
            }

            // Find ScopedPanelBase in active child
            var panel = FindControlRecursive<WileyWidget.WinForms.Controls.Base.ScopedPanelBase>(activeChild);
            if (panel != null && panel.UntypedViewModel != null)
            {
                // Try to invoke refresh command if available
                var viewModelType = panel.UntypedViewModel.GetType();
                var refreshCommand = viewModelType.GetProperty("RefreshCommand")?.GetValue(panel.UntypedViewModel);

                if (refreshCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                {
                    _ = asyncCmd.ExecuteAsync(null);
                    _logger?.LogDebug("Refreshed active panel: {PanelType}", panel.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error refreshing active panel");
        }
    }

    /// <summary>
    /// Finds a control of specific type recursively in control tree.
    /// </summary>
    private T? FindControlRecursive<T>(Control parent) where T : Control
    {
        if (parent is T match)
        {
            return match;
        }

        foreach (Control child in parent.Controls)
        {
            var found = FindControlRecursive<T>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SafeExecute(System.Action action, string operationName, ILogger? logger)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "{OperationName} failed", operationName);
        }
    }

}
