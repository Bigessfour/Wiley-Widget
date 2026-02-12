using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Utilities;
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
    /// Navigation history for back/forward functionality.
    /// </summary>
    private readonly System.Collections.Generic.Stack<string> _navigationHistory = new();
    private readonly System.Collections.Generic.Stack<string> _forwardHistory = new();
    private string? _currentPanelName;

    /// <summary>
    /// Shows or activates a docked panel. Creates it if not already present.
    /// Delegates to PanelNavigationService for centralized panel management.
    /// </summary>
    /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
    /// <param name="panelName">Optional panel name. If null, uses type name.</param>
    /// <param name="preferredStyle">Preferred docking position (default: Right).</param>
    /// <param name="allowFloating">If true, panel can be floated by user (default: true).</param>
    public void ShowPanel<TPanel>(
        string? panelName = null,
        DockingStyle preferredStyle = DockingStyle.Right,
        bool allowFloating = true)
        where TPanel : UserControl
    {
        // Ensure panel navigator is initialized before attempting to show panel
        EnsurePanelNavigatorInitialized();

        if (_panelNavigator == null)
        {
            _logger?.LogWarning("ShowPanel<{PanelType}> called but PanelNavigator is still null after initialization attempt", typeof(TPanel).Name);
            return;
        }

#pragma warning disable CS8604 // Possible null reference argument
        _panelNavigator.ShowPanel<TPanel>(panelName, preferredStyle, allowFloating);
#pragma warning restore CS8604
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
        // Ensure panel navigator is initialized before attempting to show panel
        EnsurePanelNavigatorInitialized();

        if (_panelNavigator == null)
        {
            _logger?.LogWarning("ShowPanel<{PanelType}> (with parameters) called but PanelNavigator is still null after initialization attempt", typeof(TPanel).Name);
            return;
        }

        _panelNavigator.ShowPanel<TPanel>(panelName ?? typeof(TPanel).Name, parameters, preferredStyle, allowFloating);
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

        // Directly invoke the command (avoids reflection AmbiguousMatchException)
        try
        {
            // GlobalSearchCommand is AsyncRelayCommand<string> - call ExecuteAsync directly
            await viewModel.GlobalSearchCommand.ExecuteAsync(query);
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
        if (_panelNavigator != null && panel != null && !string.IsNullOrWhiteSpace(panelName))
        {
            await _panelNavigator.AddPanelAsync(panel, panelName, preferredStyle);
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

            // Try to initialize basic docking if not already done
            if (_dockingManager == null)
            {
                _logger?.LogInformation("EnsurePanelNavigatorInitialized: DockingManager is null - attempting basic docking initialization");
                if (!TryInitializeBasicDocking())
                {
                    _logger?.LogWarning("EnsurePanelNavigatorInitialized: Basic docking initialization failed - skipping panel navigator initialization");
                    return;
                }
                _logger?.LogInformation("EnsurePanelNavigatorInitialized: Basic docking initialization succeeded");
            }

            // If it's null, create it with the now-initialized DockingManager
            if (_panelNavigator == null)
            {
                var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<PanelNavigationService>>(_serviceProvider) ?? NullLogger<PanelNavigationService>.Instance;

                try
                {
                    _panelNavigator = new PanelNavigationService(_dockingManager!, this, _serviceProvider, navLogger);
                    _logger?.LogDebug("PanelNavigationService created with initialized DockingManager");
                    // Subscribe to activation events to keep ribbon/navigation selection in sync
                    try
                    {
                        _panelNavigator.PanelActivated += PanelNavigator_OnPanelActivated;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to subscribe to PanelActivated events");
                    }
                }
                catch (ArgumentNullException argEx)
                {
                    _logger?.LogError(argEx, "Failed to create PanelNavigationService due to null argument - docking panel navigation will be unavailable");
                    return;
                }
                catch (InvalidOperationException invEx)
                {
                    _logger?.LogError(invEx, "Failed to create PanelNavigationService due to invalid operation - docking panel navigation will be unavailable");
                    return;
                }
            }
            else
            {
                _logger?.LogDebug("PanelNavigationService already initialized - no action needed");
            }
        }
        catch (ObjectDisposedException dispEx)
        {
            _logger?.LogWarning(dispEx, "Panel navigator initialization failed - docking manager or service provider was disposed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error in EnsurePanelNavigatorInitialized - panel navigation may be unavailable. Error: {ErrorType}", ex.GetType().Name);

            // Provide user feedback for unexpected errors that may impact functionality
            if (this.InvokeRequired)
            {
                this.BeginInvoke(() =>
                {
                    UIHelper.ShowErrorOnUI(this,
                        "Failed to initialize panel navigation system. Some navigation features may be unavailable.",
                        "Initialization Error", _logger);
                });
            }
            else
            {
                UIHelper.ShowErrorOnUI(this,
                    "Failed to initialize panel navigation system. Some navigation features may be unavailable.",
                    "Initialization Error", _logger);
            }
        }
    }

    /// <summary>
    /// Tracks navigation history for back/forward functionality.
    /// Called whenever a panel is activated.
    /// </summary>
    /// <param name="panelName">The name of the panel that was activated.</param>
    private void TrackNavigationHistory(string panelName)
    {
        if (string.IsNullOrEmpty(panelName))
        {
            return;
        }

        try
        {
            // If we're navigating to a different panel, add current to history
            if (_currentPanelName != null && _currentPanelName != panelName)
            {
                _navigationHistory.Push(_currentPanelName);
                _forwardHistory.Clear(); // Clear forward history when navigating to new panel
                _logger?.LogDebug("Added {PreviousPanel} to navigation history, forward history cleared", _currentPanelName);
            }

            _currentPanelName = panelName;
            _logger?.LogDebug("Navigation history updated: Current={CurrentPanel}, HistoryCount={HistoryCount}",
                _currentPanelName, _navigationHistory.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track navigation history for panel: {PanelName}", panelName);
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

    /// <summary>
    /// Navigates back to the previously active panel.
    /// </summary>
    /// <returns>True if navigation succeeded, false if no history available.</returns>
    public bool NavigateBack()
    {
        if (_navigationHistory.Count == 0)
        {
            _logger?.LogDebug("Cannot navigate back: No navigation history available");
            return false;
        }

        try
        {
            // Move current panel to forward history
            if (_currentPanelName != null)
            {
                _forwardHistory.Push(_currentPanelName);
            }

            // Get previous panel from history
            string previousPanel = _navigationHistory.Pop();
            _currentPanelName = previousPanel;

            // Activate the previous panel
            if (_panelNavigator != null)
            {
                // Try to show the panel (this will activate it if it exists)
                // We need to determine the panel type from the name
                // For now, we'll use a generic approach
                ActivatePanelByName(previousPanel);
            }

            _logger?.LogInformation("Navigated back to panel: {PanelName}", previousPanel);
            UpdateNavigationButtons();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to navigate back");
            return false;
        }
    }

    /// <summary>
    /// Navigates forward to the next panel in history.
    /// </summary>
    /// <returns>True if navigation succeeded, false if no forward history available.</returns>
    public bool NavigateForward()
    {
        if (_forwardHistory.Count == 0)
        {
            _logger?.LogDebug("Cannot navigate forward: No forward history available");
            return false;
        }

        try
        {
            // Move current panel to back history
            if (_currentPanelName != null)
            {
                _navigationHistory.Push(_currentPanelName);
            }

            // Get next panel from forward history
            string nextPanel = _forwardHistory.Pop();
            _currentPanelName = nextPanel;

            // Activate the next panel
            if (_panelNavigator != null)
            {
                ActivatePanelByName(nextPanel);
            }

            _logger?.LogInformation("Navigated forward to panel: {PanelName}", nextPanel);
            UpdateNavigationButtons();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to navigate forward");
            return false;
        }
    }

    /// <summary>
    /// Gets whether back navigation is available.
    /// </summary>
    public bool CanNavigateBack => _navigationHistory.Count > 0;

    /// <summary>
    /// Gets whether forward navigation is available.
    /// </summary>
    public bool CanNavigateForward => _forwardHistory.Count > 0;

    /// <summary>
    /// Activates a panel by name. This is a helper method for navigation history.
    /// Searches through DockingManager controls to find and activate the panel.
    /// </summary>
    /// <param name="panelName">The name of the panel to activate.</param>
    private void ActivatePanelByName(string panelName)
    {
        if (_dockingManager == null || string.IsNullOrEmpty(panelName))
        {
            return;
        }

        try
        {
            // Search through DockingManager controls for a panel with matching name
            var controlsObj = _dockingManager.Controls;
            if (controlsObj is System.Collections.ICollection coll && coll.Count > 0)
            {
                foreach (var item in coll)
                {
                    if (item is Control control && !control.IsDisposed &&
                        !string.IsNullOrEmpty(control.Name) && control.Name == panelName)
                    {
                        // Found the panel, activate it
                        _dockingManager.ActivateControl(control);
                        _logger?.LogDebug("Activated panel by name: {PanelName}", panelName);
                        return;
                    }
                }
            }

            _logger?.LogWarning("Panel not found for activation: {PanelName}", panelName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to activate panel by name: {PanelName}", panelName);
        }
    }

    /// <summary>
    /// Handles panel activation events from the PanelNavigationService.
    /// Updates ribbon/navigation selection to keep UI in sync with active panel.
    /// Also tracks navigation history for back/forward functionality.
    /// </summary>
    private void PanelNavigator_OnPanelActivated(object? sender, PanelActivatedEventArgs e)
    {
        if (IsDisposed || e == null)
        {
            return;
        }

        try
        {
            _logger?.LogDebug("Panel activated: {PanelName} ({PanelType})", e.PanelName, e.PanelType.Name);

            // Track navigation history
            TrackNavigationHistory(e.PanelName);

            // Update ribbon/navigation selection to match active panel
            // This keeps the UI synchronized with the navigation service state
            UpdateNavigationSelection(e.PanelName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to handle panel activation for {PanelName}", e.PanelName);
        }
    }

    /// <summary>
    /// Updates the ribbon/navigation strip selection to match the active panel.
    /// Called when panels are activated to keep UI selection synchronized.
    /// </summary>
    private void UpdateNavigationSelection(string panelName)
    {
        try
        {
            // Update ribbon navigation buttons
            if (_ribbon != null)
            {
                // Find and select the corresponding ribbon button
                var navigationButtons = FindToolStripItems(_ribbon, item =>
                    item.Tag is string tag && tag.StartsWith("Nav:", StringComparison.OrdinalIgnoreCase) &&
                    tag.EndsWith(panelName, StringComparison.OrdinalIgnoreCase));

                foreach (var button in navigationButtons)
                {
                    if (button is ToolStripButton toolStripButton)
                    {
                        toolStripButton.Checked = true;
                        _logger?.LogDebug("Updated ribbon button selection for panel: {PanelName}", panelName);
                    }
                }
            }

            // Update navigation strip selection
            if (_navigationStrip != null)
            {
                // Find and select the corresponding navigation strip item
                var navigationItems = FindToolStripItems((ToolStripItemCollection)_navigationStrip.Items, item =>
                    item.Tag is string tag && tag.StartsWith("Nav:", StringComparison.OrdinalIgnoreCase) &&
                    tag.EndsWith(panelName, StringComparison.OrdinalIgnoreCase));

                foreach (var item in navigationItems)
                {
                    if (item is ToolStripButton toolStripButton)
                    {
                        toolStripButton.Checked = true;
                        _logger?.LogDebug("Updated navigation strip selection for panel: {PanelName}", panelName);
                    }
                }
            }

            // Update navigation button states (back/forward)
            UpdateNavigationButtons();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update navigation selection for panel: {PanelName}", panelName);
        }
    }

    /// <summary>
    /// Updates the enabled state of back/forward navigation buttons in the UI.
    /// Should be called whenever navigation history changes.
    /// </summary>
    private void UpdateNavigationButtons()
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(UpdateNavigationButtons));
                return;
            }

            // Update ribbon navigation buttons
            if (_ribbon != null)
            {
                var backButton = FindToolStripItem(_ribbon, "NavBack") as ToolStripButton;
                var forwardButton = FindToolStripItem(_ribbon, "NavForward") as ToolStripButton;

                if (backButton != null)
                {
                    backButton.Enabled = CanNavigateBack;
                }

                if (forwardButton != null)
                {
                    forwardButton.Enabled = CanNavigateForward;
                }
            }

            // Update navigation strip buttons
            if (_navigationStrip != null)
            {
                var backButton = _navigationStrip.Items.Find("NavBack", true).FirstOrDefault() as ToolStripButton;
                var forwardButton = _navigationStrip.Items.Find("NavForward", true).FirstOrDefault() as ToolStripButton;

                if (backButton != null)
                {
                    backButton.Enabled = CanNavigateBack;
                }

                if (forwardButton != null)
                {
                    forwardButton.Enabled = CanNavigateForward;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update navigation button states");
        }
    }

    /// <summary>
    /// Finds all ToolStripItem instances that match the specified predicate.
    /// Searches recursively through ribbon tabs, panels, and dropdown items.
    /// </summary>
    private IEnumerable<ToolStripItem> FindToolStripItems(RibbonControlAdv ribbon, Func<ToolStripItem, bool> predicate)
    {
        if (ribbon == null || predicate == null)
        {
            yield break;
        }

        foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
        {
            if (tab.Panel != null)
            {
                foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                {
                    foreach (var item in FindToolStripItemsRecursive(panel.Items, predicate))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds all ToolStripItem instances that match the specified predicate.
    /// Searches recursively through ToolStripItemCollection and nested items.
    /// </summary>
    private IEnumerable<ToolStripItem> FindToolStripItems(ToolStripItemCollection items, Func<ToolStripItem, bool> predicate)
    {
        if (items == null || predicate == null)
        {
            yield break;
        }

        foreach (var item in FindToolStripItemsRecursive(items, predicate))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Recursively searches ToolStripItemCollection for items matching the predicate.
    /// Handles nested ToolStripPanelItem and ToolStripDropDownItem containers.
    /// </summary>
    private IEnumerable<ToolStripItem> FindToolStripItemsRecursive(ToolStripItemCollection items, Func<ToolStripItem, bool> predicate)
    {
        foreach (ToolStripItem item in items)
        {
            ToolStripPanelItem? panelItem = null;
            ToolStripDropDownItem? dropDown = null;
            bool matches = false;

            try
            {
                matches = predicate(item);
                panelItem = item as ToolStripPanelItem;
                dropDown = item as ToolStripDropDownItem;
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("ToolStripItem was disposed during recursive search");
                continue;
            }
            catch (InvalidOperationException)
            {
                _logger?.LogDebug("ToolStripItem collection was modified during search");
                continue;
            }

            if (matches)
            {
                yield return item;
            }

            if (panelItem != null)
            {
                foreach (var found in FindToolStripItemsRecursive(panelItem.Items, predicate))
                {
                    yield return found;
                }
            }

            if (dropDown != null)
            {
                foreach (var found in FindToolStripItemsRecursive(dropDown.DropDownItems, predicate))
                {
                    yield return found;
                }
            }
        }
    }

    /// <summary>
    /// Shows a form as a child or modal dialog. Creates it via DI if not already present.
    /// Use this for standalone forms that should appear independently, not as docked panels.
    /// </summary>
    /// <typeparam name="TForm">The Form type to show.</typeparam>
    /// <param name="asModal">If true, shows as modal dialog; otherwise as modeless child form (default: false).</param>
    public void ShowForm<TForm>(bool asModal = false) where TForm : Form
    {
        if (_serviceProvider == null)
        {
            _logger?.LogWarning("ShowForm<{FormType}> called but ServiceProvider is null", typeof(TForm).Name);
            return;
        }



        try
        {
            // Resolve form from DI container
            var form = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TForm>(_serviceProvider);

            if (form == null)
            {
                _logger?.LogError("Failed to resolve form {FormType} from DI container", typeof(TForm).Name);
                return;
            }

            // Set owner for proper Z-order and taskbar behavior
            if (form.Owner == null)
            {
                form.Owner = this;
            }

            // Show form based on modal flag
            if (asModal)
            {
                form.ShowDialog(this);
            }
            else
            {
                form.Show(this);
            }

            _logger?.LogInformation("Opened {FormType} (Modal: {AsModal})", typeof(TForm).Name, asModal);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show form {FormType}", typeof(TForm).Name);
            UIHelper.ShowErrorOnUI(this, $"Failed to open {typeof(TForm).Name}: {ex.Message}", "Navigation Error", _logger);
        }
    }

    /// <summary>
    /// Shows or activates a docked Form by hosting it inside the panel docking system.
    /// Delegates to PanelNavigationService to preserve docking/floating behavior.
    /// </summary>
    public void ShowForm<TForm>(
        string? panelName = null,
        DockingStyle preferredStyle = DockingStyle.Right,
        bool allowFloating = true)
        where TForm : Form
    {
        EnsurePanelNavigatorInitialized();

        if (_panelNavigator == null)
        {
            _logger?.LogWarning("ShowForm<{FormType}> called but PanelNavigator is still null after initialization attempt", typeof(TForm).Name);
            return;
        }

#pragma warning disable CS8604 // Possible null reference argument
        _panelNavigator.ShowForm<TForm>(panelName ?? typeof(TForm).Name, preferredStyle, allowFloating);
#pragma warning restore CS8604
    }

    /// <summary>
    /// Shows or activates a docked Form with initialization parameters.
    /// Delegates to PanelNavigationService to preserve docking/floating behavior.
    /// </summary>
    public void ShowForm<TForm>(
        string? panelName,
        object? parameters,
        DockingStyle preferredStyle = DockingStyle.Right,
        bool allowFloating = true)
        where TForm : Form
    {
        EnsurePanelNavigatorInitialized();

        if (_panelNavigator == null)
        {
            _logger?.LogWarning("ShowForm<{FormType}> (with parameters) called but PanelNavigator is still null after initialization attempt", typeof(TForm).Name);
            return;
        }

        _panelNavigator.ShowForm<TForm>(panelName ?? typeof(TForm).Name, parameters, preferredStyle, allowFloating);
    }
}
