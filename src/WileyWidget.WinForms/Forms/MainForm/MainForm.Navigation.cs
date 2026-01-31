using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    /// <summary>
    /// Global PanelNavigator for the application.
    /// Created during MainForm initialization and exposed for DI resolution.
    /// </summary>
    public IPanelNavigationService? PanelNavigator => _panelNavigator;

    /// <summary>
    /// Shows or activates a docked panel. Creates it if not already present.
    /// Delegates to PanelNavigationService for centralized panel management.
    /// Enforces Z-order and validates panel hosting after showing.
    /// </summary>
    /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
    /// <param name="panelName">Optional panel name. If null, uses type name.</param>
    /// <param name="preferredStyle">Preferred docking position (default: Right).</param>
    /// <param name="allowFloating">If true, panel can be floated by user (default: true).</param>
    /// <summary>
    /// Validates that the docking system is ready for navigation operations.
    /// Checks both DockingManager and PanelNavigator; logs and shows user feedback if not ready.
    /// </summary>
    private bool ValidateDockingState(string operation)
    {
        if (_dockingManager == null)
        {
            _logger?.LogWarning("[DOCKING.VALIDATE] {Operation}: DockingManager is null", operation);
            ApplyStatus("Docking system not ready — please wait a moment");

            if (!IsDisposed && IsHandleCreated && Visible)
            {
                try
                {
                    UIHelper.ShowMessageOnUI(this,
                        "The docking system is still initializing.\nPlease wait 2–3 seconds and try again.",
                        "Navigation Initializing",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information,
                        _logger);
                }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to show message"); }
            }
            return false;
        }

        if (_panelNavigator == null)
        {
            _logger?.LogDebug("[DOCKING.VALIDATE] {Operation}: PanelNavigator is null - attempting initialization", operation);
            EnsurePanelNavigatorInitialized();

            if (_panelNavigator == null)
            {
                _logger?.LogError("[DOCKING.VALIDATE] {Operation}: PanelNavigator initialization failed", operation);
                ApplyStatus("Panel navigation unavailable");

                if (!IsDisposed && IsHandleCreated && Visible)
                {
                    try
                    {
                        UIHelper.ShowMessageOnUI(this,
                            "Panel navigation service failed to initialize.",
                            "Navigation Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning,
                            _logger);
                    }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to show error dialog"); }
                }
                return false;
            }
            _logger?.LogInformation("[DOCKING.VALIDATE] {Operation}: PanelNavigator initialized successfully", operation);
        }

        return true;
    }

    public void ShowPanel<TPanel>(
        string? panelName = null,
        DockingStyle preferredStyle = DockingStyle.Right,
        bool allowFloating = true)
        where TPanel : UserControl
    {
        var displayName = panelName ?? typeof(TPanel).Name;

        if (!ValidateDockingState($"ShowPanel<{typeof(TPanel).Name}>"))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(displayName))
        {
            _logger?.LogWarning("[PANEL] ShowPanel called with invalid/empty panel name for type {Type}", typeof(TPanel).Name);
            ApplyStatus("Cannot open panel — invalid name");
            return;
        }

        try
        {
            _logger?.LogInformation("[PANEL] Showing {PanelName} ({Type})", displayName, typeof(TPanel).Name);
            _panelNavigator!.ShowPanel<TPanel>(displayName, preferredStyle, allowFloating);

            try { EnsureDockingZOrder(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "[PANEL] Failed to ensure z-order (non-critical)"); }

            // NOTE: GetControl() doesn't exist on DockingManager. Panel visibility is managed by PanelNavigationService.
            // Removing manual visibility check as PanelNavigationService already handles activation.

            ApplyStatus($"Opened: {displayName}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DockingManager"))
        {
            _logger?.LogError(ex, "[PANEL] DockingManager issue while showing panel {PanelType}", typeof(TPanel).Name);
            ApplyStatus("Docking system error — please try again");
            if (!IsDisposed && IsHandleCreated && Visible)
            {
                try
                {
                    UIHelper.ShowMessageOnUI(this,
                        "Docking system encountered an error. Please try again.",
                        "Docking Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning,
                        _logger);
                }
                catch (Exception ex2) { _logger?.LogDebug(ex2, "Failed to show docking error dialog"); }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[PANEL] Failed to show panel {PanelName} of type {Type}", displayName, typeof(TPanel).Name);
            ApplyStatus("Error opening panel — check logs for details");
            if (!IsDisposed && IsHandleCreated && Visible)
            {
                try
                {
                    UIHelper.ShowErrorOnUI(this,
                        $"Failed to open '{displayName}':\n{ex.Message}",
                        "Panel Error",
                        _logger);
                }
                catch (Exception ex2) { _logger?.LogDebug(ex2, "Failed to show panel error dialog"); }
            }
        }
    }

    /// <summary>
    /// Shows or activates a docked panel with initialization parameters. Creates it if not already present.
    /// Delegates to PanelNavigationService for centralized panel management.
    /// </summary>
    public void ShowPanel<TPanel>(
        string? panelName,
        object? parameters,
        DockingStyle preferredStyle = DockingStyle.Right,
        bool allowFloating = true)
        where TPanel : UserControl
    {
        var displayName = panelName ?? typeof(TPanel).Name;

        if (!ValidateDockingState($"ShowPanel<{typeof(TPanel).Name}>(params)"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            _logger?.LogWarning("[PANEL] ShowPanel with parameters called with invalid/empty panel name for type {Type}", typeof(TPanel).Name);
            ApplyStatus("Cannot open panel — invalid name");
            return;
        }

        try
        {
            _logger?.LogInformation("[PANEL] Showing {PanelName} ({Type}) with parameters", displayName, typeof(TPanel).Name);
            _panelNavigator!.ShowPanel<TPanel>(displayName, parameters, preferredStyle, allowFloating);

            try { EnsureDockingZOrder(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "[PANEL] Failed to ensure z-order (non-critical)"); }

            // NOTE: GetControl() doesn't exist on DockingManager. Panel visibility is managed by PanelNavigationService.
            // Removing manual visibility check as PanelNavigationService already handles activation.

            ApplyStatus($"Opened: {displayName}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No service for type") || ex.Message.Contains("not registered"))
        {
            _logger?.LogError(ex, "[PANEL] Panel {PanelType} not registered in DI container", typeof(TPanel).Name);
            ApplyStatus("Panel type not registered — check configuration");
            if (!IsDisposed && IsHandleCreated && Visible)
            {
                UIHelper.ShowErrorOnUI(this,
                    $"Panel '{displayName}' ({typeof(TPanel).Name}) is not registered in the service container.\nPlease verify DependencyInjection.cs.",
                    "DI Registration Error", _logger);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DockingManager"))
        {
            _logger?.LogError(ex, "[PANEL] DockingManager issue while showing panel {PanelType} with parameters", typeof(TPanel).Name);
            ApplyStatus("Docking system error — please try again");
            if (!IsDisposed && IsHandleCreated && Visible)
            {
                try
                {
                    UIHelper.ShowMessageOnUI(this,
                        "Docking system encountered an error. Please try again.",
                        "Docking Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning,
                        _logger);
                }
                catch (Exception ex2) { _logger?.LogDebug(ex2, "Failed to show docking error dialog"); }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[PANEL] Failed to show panel {PanelName} with parameters ({Type})", displayName, typeof(TPanel).Name);
            ApplyStatus("Error opening panel — check logs for details");
            if (!IsDisposed && IsHandleCreated && Visible)
            {
                try
                {
                    UIHelper.ShowErrorOnUI(this,
                        $"Failed to open '{displayName}':\n{ex.Message}",
                        "Panel Error",
                        _logger);
                }
                catch (Exception ex2) { _logger?.LogDebug(ex2, "Failed to show panel error dialog"); }
            }
        }
    }

    /// <summary>
    /// Performs a global search across all modules (accounts, budgets, reports).
    /// This method delegates to MainViewModel.GlobalSearchCommand for MVVM purity.
    /// Called from ribbon search box for backward compatibility.
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <remarks>
    /// DEPRECATED: Use MainViewModel.GlobalSearchCommand.ExecuteAsync(query) directly.
    /// Kept for backward compatibility with ribbon event handlers.
    /// </remarks>
    public async Task PerformGlobalSearchAsync(string query)
    {
        var viewModel = MainViewModel;
        if (viewModel?.GlobalSearchCommand == null)
        {
            UIHelper.ShowErrorOnUI(this, "ViewModel not initialized.", "Search Error", _logger);
            return;
        }

        // Delegate to ViewModel's GlobalSearchCommand
        // Command handles validation, error display, and resilience
        try
        {
            var method = viewModel.GlobalSearchCommand.GetType().GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                var task = (Task?)method.Invoke(viewModel.GlobalSearchCommand, new object?[] { query });
                if (task != null)
                    await task;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute GlobalSearchCommand");
            UIHelper.ShowErrorOnUI(this, $"Search failed: {ex.Message}", "Search Error", _logger);
        }
    }

    /// <summary>
    /// Synchronous wrapper for backward compatibility with async void callers.
    /// New code should use PerformGlobalSearchAsync() or MainViewModel.GlobalSearchCommand directly.
    /// </summary>
    [Obsolete("Use PerformGlobalSearchAsync() or MainViewModel.GlobalSearchCommand.ExecuteAsync() instead")]
    public void PerformGlobalSearch(string query)
    {
        // Fire-and-forget for backward compatibility
        _ = PerformGlobalSearchAsync(query);
    }

    /// <summary>
    /// Adds an existing panel instance to the docking manager asynchronously.
    /// Useful for panels pre-initialized with specific ViewModels or state.
    /// </summary>
    /// <param name="panel">The UserControl panel instance to add.</param>
    /// <param name="panelName">The display name used for caption and identification.</param>
    /// <param name="preferredStyle">The preferred docking style (default: Right).</param>
    /// <returns>A task that completes when the panel is added.</returns>
    public async Task AddPanelAsync(
        UserControl panel,
        string panelName,
        DockingStyle preferredStyle = DockingStyle.Right)
    {
        // Ensure panel navigator is initialized before proceeding
        if (_panelNavigator == null)
        {
            _logger?.LogDebug("PanelNavigator is null - attempting to initialize");
            EnsurePanelNavigatorInitialized();

            // Re-check after initialization attempt
            if (_panelNavigator == null)
            {
                _logger?.LogWarning("Cannot add panel async - PanelNavigationService initialization failed");
                if (!IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        UIHelper.ShowMessageOnUI(this,
                            "Panel navigation is not available. The docking system may not be initialized.",
                            "Navigation Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning,
                            _logger);
                    }
                    catch { }
                }
                return;
            }
        }

        if (panel == null)
        {
            _logger?.LogWarning("AddPanelAsync called with null panel");
            return;
        }

        if (string.IsNullOrWhiteSpace(panelName))
        {
            _logger?.LogWarning("AddPanelAsync called with invalid panel name");
            return;
        }

        try
        {
            await _panelNavigator.AddPanelAsync(panel, panelName, preferredStyle);

            // Force z-order after showing panel to prevent layout issues
            try { EnsureDockingZOrder(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to ensure docking z-order after AddPanelAsync"); }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DockingManager"))
        {
            _logger?.LogError(ex, "DockingManager issue while adding panel {PanelName} async", panelName);
            if (!IsDisposed && IsHandleCreated)
            {
                try
                {
                    UIHelper.ShowMessageOnUI(this,
                        "Docking system is not ready. Please try again.",
                        "Docking Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning,
                        _logger);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add panel {PanelName} async in MainForm", panelName);
            if (!IsDisposed && IsHandleCreated)
            {
                try { UIHelper.ShowMessageOnUI(this, "Failed to open panel: " + ex.Message, "Panel Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, _logger); } catch { }
            }
        }
    }

    private void EnsurePanelNavigatorInitialized()
    {
        try
        {
            // Defensive checks: Validate all dependencies before proceeding
            if (_serviceProvider == null)
            {
                _logger?.LogWarning("EnsurePanelNavigatorInitialized: ServiceProvider is null - skipping panel navigator initialization");
                return;
            }

            if (_dockingManager == null)
            {
                _logger?.LogWarning("EnsurePanelNavigatorInitialized: DockingManager is null - skipping panel navigator initialization");
                return;
            }

            // If it's null, create it with the now-initialized DockingManager
            if (_panelNavigator == null)
            {
                var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<PanelNavigationService>>(_serviceProvider) ?? NullLogger<PanelNavigationService>.Instance;

                try
                {
                    _panelNavigator = new PanelNavigationService(_dockingManager, this, _serviceProvider, navLogger);
                    _logger?.LogDebug("PanelNavigationService created with initialized DockingManager");
                }
                catch (Exception creationEx)
                {
                    _logger?.LogError(creationEx, "Failed to create PanelNavigationService - docking panel navigation will be unavailable");
                    return;
                }
            }
            else
            {
                _logger?.LogDebug("PanelNavigationService already initialized - no action needed");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Unexpected error in EnsurePanelNavigatorInitialized - panel navigation may be unavailable");
        }
    }

    /// <summary>
    /// Closes the settings panel if it's currently visible.
    /// Legacy method: SettingsForm replaced by SettingsPanel.
    /// </summary>
    public void CloseSettingsPanel()
    {
        // Legacy method - SettingsForm replaced by SettingsPanel
        _panelNavigator?.HidePanel("Settings");
    }

    /// <summary>
    /// Closes a panel with the specified name.
    /// </summary>
    /// <param name="panelName">Name of the panel to close.</param>
    public void ClosePanel(string panelName)
    {
        _panelNavigator?.HidePanel(panelName);
    }
}
