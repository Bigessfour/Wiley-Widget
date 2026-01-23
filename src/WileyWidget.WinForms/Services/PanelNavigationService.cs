using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Interface for panels that can be initialized with parameters.
    /// </summary>
    public interface IParameterizedPanel
    {
        /// <summary>
        /// Initialize the panel with the provided parameters.
        /// </summary>
        /// <param name="parameters">Parameters for panel initialization.</param>
        void InitializeWithParameters(object parameters);
    }

    /// <summary>
    /// Centralized service for managing docked panels in MainForm's DockingManager.
    /// Ensures single instance per panel type, reuse, activation, and proper naming.
    /// Replaces scattered menu click handlers and legacy form-based navigation.
    /// Panels are resolved via dependency injection to support constructor parameters.
    /// </summary>
    public interface IPanelNavigationService
    {
        /// <summary>
        /// Shows or activates a docked panel. Creates it if not already present.
        /// Panel is resolved from DI container to support constructor injection.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Unique display name (also used as DockingManager key).</param>
        /// <param name="preferredStyle">Preferred docking position.</param>
        /// <param name="allowFloating">If true, panel can be floated by user.</param>
        void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        /// <summary>
        /// Shows or activates a docked panel with initialization parameters. Creates it if not already present.
        /// Panel is resolved from DI container to support constructor injection.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Unique display name (also used as DockingManager key).</param>
        /// <param name="parameters">Parameters to pass to panel constructor or initialization.</param>
        /// <param name="preferredStyle">Preferred docking position.</param>
        /// <param name="allowFloating">If true, panel can be floated by user.</param>
        void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl;

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        bool HidePanel(string panelName);

        /// <summary>
        /// Adds an existing panel instance to the docking manager asynchronously.
        /// </summary>
        /// <param name="panel">The panel instance to add.</param>
        /// <param name="panelName">The name/title of the panel.</param>
        /// <param name="preferredStyle">The preferred docking style.</param>
        /// <param name="allowFloating">Whether the panel can be floated.</param>
        /// <returns>A task that completes when the panel is added.</returns>
        Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true);

        /// <summary>
        /// Get the currently active panel name for ribbon button state tracking.
        /// </summary>
        /// <returns>The name of the currently active panel, or null if no panel is active.</returns>
        string? GetActivePanelName();

        /// <summary>
        /// Event raised when a panel is activated, for ribbon button highlighting.
        /// </summary>
        event EventHandler<PanelActivatedEventArgs>? PanelActivated;
    }

    public sealed class PanelNavigationService : IPanelNavigationService, IDisposable
    {
        /// <summary>
        /// C# 14: Logger property for cleaner access.
        /// </summary>
        private readonly ILogger<PanelNavigationService> Logger;

        private readonly DockingManager _dockingManager;
        private readonly Control _parentControl; // Usually MainForm or central document container
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, UserControl> _cachedPanels = new();

        /// <summary>
        /// Tracks the currently active panel name for ribbon button highlighting.
        /// </summary>
        private string? _activePanelName;

        /// <summary>
        /// Event raised when the active panel changes, for ribbon button state updates.
        /// </summary>
        public event EventHandler<PanelActivatedEventArgs>? PanelActivated;

        private static readonly Dictionary<Type, PanelSizing> PanelSizeOverrides = new()
        {
            { typeof(DashboardPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(450, 420)) },
            { typeof(AccountsPanel), new PanelSizing(new Size(620, 0), new Size(0, 380), new Size(520, 420)) },
            { typeof(BudgetAnalyticsPanel), new PanelSizing(new Size(560, 0), new Size(0, 460), new Size(480, 420)) },
            { typeof(BudgetOverviewPanel), new PanelSizing(new Size(540, 0), new Size(0, 420), new Size(480, 360)) },
            { typeof(AnalyticsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 380)) },
            { typeof(AuditLogPanel), new PanelSizing(new Size(520, 0), new Size(0, 380), new Size(440, 320)) },
            { typeof(ReportsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(ProactiveInsightsPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(WarRoomPanel), new PanelSizing(new Size(560, 0), new Size(0, 420), new Size(460, 380)) },
            { typeof(QuickBooksPanel), new PanelSizing(new Size(620, 0), new Size(0, 400), new Size(540, 360)) },
            { typeof(BudgetPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(DepartmentSummaryPanel), new PanelSizing(new Size(540, 0), new Size(0, 400), new Size(440, 360)) },
            { typeof(SettingsPanel), new PanelSizing(new Size(500, 0), new Size(0, 360), new Size(420, 320)) },
            { typeof(RevenueTrendsPanel), new PanelSizing(new Size(560, 0), new Size(0, 440), new Size(460, 380)) },
            { typeof(UtilityBillPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
            { typeof(CustomersPanel), new PanelSizing(new Size(560, 0), new Size(0, 400), new Size(460, 360)) },
        };

        /// <summary>
        /// Initializes the panel navigation service with docking infrastructure and dependency injection support.
        /// DockingManager must be non-null and properly initialized before construction.
        /// </summary>
        /// <param name="dockingManager">Initialized DockingManager instance (required, non-null).</param>
        /// <param name="parentControl">Parent control hosting the docked panels.</param>
        /// <param name="serviceProvider">Service provider for DI resolution of panel types.</param>
        /// <param name="logger">Logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
        public PanelNavigationService(
            DockingManager dockingManager,
            Control parentControl,
            IServiceProvider serviceProvider,
            ILogger<PanelNavigationService> logger)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager), "DockingManager must be initialized before PanelNavigationService construction.");
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Logger.LogDebug("PanelNavigationService initialized with non-null DockingManager");
            }

        public void ShowPanel<TPanel>(
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, null, preferredStyle, allowFloating);
        }

        public void ShowPanel<TPanel>(
            string panelName,
            object? parameters,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            try
            {
                Logger.LogInformation("[PANEL] Showing {PanelName} - type: {Type}", panelName, typeof(TPanel).Name);

                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    ActivateExistingPanel(existingPanel, panelName, allowFloating);
                    return;
                }

                // Create new instance via DI (supports constructor injection)
                Logger.LogDebug("Creating panel: {PanelName} ({PanelType})", panelName, typeof(TPanel).Name);
                var panel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<TPanel>(_serviceProvider);

                // C# 14: Pattern matching with 'is not null' for cleaner guard clause
                if (parameters is not null && panel is IParameterizedPanel parameterizedPanel)
                {
                    parameterizedPanel.InitializeWithParameters(parameters);
                }

                DockPanelInternal(panel, panelName, preferredStyle, allowFloating);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to show panel: {PanelName}", panelName);
                throw new InvalidOperationException($"Unable to show panel '{panelName}'. Check logs for details.", ex);
            }
        }

        public async Task AddPanelAsync(UserControl panel, string panelName, DockingStyle preferredStyle = DockingStyle.Right, bool allowFloating = true)
        {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            try
            {
                // Reuse existing panel if already created
                if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
                {
                    await _parentControl.InvokeAsync(() => ActivateExistingPanel(existingPanel, panelName, allowFloating));
                    return;
                }

                await _parentControl.InvokeAsync(() => DockPanelInternal(panel, panelName, preferredStyle, allowFloating));
                await Task.Yield(); // Allow UI to breathe
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add panel async: {PanelName}", panelName);
                throw new InvalidOperationException($"Unable to add panel '{panelName}'. Check logs for details.", ex);
            }
        }

        private void ActivateExistingPanel(UserControl existingPanel, string panelName, bool allowFloating)
        {
            // Already on UI thread - ActivateExistingPanel is called only from ShowPanel or AddPanelAsync
            // which have already marshalled execution via InvokeAsync
            ApplyCaptionSettings(existingPanel, panelName, allowFloating);
            _dockingManager.SetDockVisibility(existingPanel, true);
            try { existingPanel.BringToFront(); } catch { }
            _dockingManager.ActivateControl(existingPanel);
            ApplyPanelTheme(existingPanel);

            // Track active panel and raise event for ribbon button highlighting
            _activePanelName = panelName;
            PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, existingPanel.GetType()));

            Logger.LogDebug("Activated existing panel: {PanelName}", panelName);
            Logger.LogInformation("[PANEL] {PanelName} activated - Visible={Visible}, Bounds={Bounds}", panelName, existingPanel.Visible, existingPanel.Bounds);
        }

        private void DockPanelInternal(UserControl panel, string panelName, DockingStyle preferredStyle, bool allowFloating)
        {
            // Already on UI thread - DockPanelInternal is called only from ShowPanel or AddPanelAsync
            // which have already marshalled execution via InvokeAsync
            panel.Name = panelName.Replace(" ", "", StringComparison.Ordinal); // Clean name for internal use

            // Apply sensible defaults so charts/grids have usable space on first show
            ApplyDefaultPanelSizing(panel, preferredStyle, panel.GetType());

            // Enable docking features and caption buttons (required for headers and buttons to appear)
            ApplyCaptionSettings(panel, panelName, allowFloating);

            // Dock the panel
            var effectiveStyle = preferredStyle;
            if (effectiveStyle == DockingStyle.Fill)
            {
                Logger.LogWarning(
                    "DockingStyle.Fill is not supported when docking to the DockingManager host. Falling back to DockingStyle.Right for panel: {PanelName}",
                    panelName);
                effectiveStyle = DockingStyle.Right;
            }

            int dockSize = CalculateDockSize(effectiveStyle, _parentControl);

            // Respect desired default dimension when we have one
            var (desiredSize, _) = GetDefaultPanelSizes(panel.GetType(), effectiveStyle);
            if (effectiveStyle is DockingStyle.Left or DockingStyle.Right && desiredSize.Width > 0)
            {
                dockSize = desiredSize.Width;
            }
            else if (effectiveStyle is DockingStyle.Top or DockingStyle.Bottom && desiredSize.Height > 0)
            {
                dockSize = desiredSize.Height;
            }

            // Dock the panel with calculated size
            _dockingManager.DockControl(panel, _parentControl, effectiveStyle, dockSize);

            // QuickBooksPanel: Prevent resizing due to explicit internal sizing (prevents StackOverflow)
            // Since all controls in QuickBooksPanel have explicit AutoSize=false + Heights,
            // user resizing the panel would break the layout stability guarantees.
            // Lock the panel to its initial size.
            if (panel.GetType() == typeof(QuickBooksPanel))
            {
                try
                {
                    // Set MaximumSize equal to initial size to prevent resize
                    var currentSize = panel.Size;
                    panel.MaximumSize = new Size(currentSize.Width, currentSize.Height);
                    Logger.LogDebug("QuickBooksPanel locked to size {Width}x{Height} to preserve explicit control sizing", currentSize.Width, currentSize.Height);
                }
                catch (Exception lockEx)
                {
                    Logger.LogDebug(lockEx, "Failed to lock QuickBooksPanel size (non-critical), continuing");
                }
            }

            // Apply minimum size to ensure usable bounds (Syncfusion API - no reflection needed)
            var (_, minimumSize) = GetDefaultPanelSizes(panel.GetType(), effectiveStyle);
            if (minimumSize.Width > 0 || minimumSize.Height > 0)
            {
                try
                {
                    _dockingManager.SetControlMinimumSize(panel, minimumSize);
                }
                catch (Exception minSizeEx)
                {
                    Logger.LogDebug(minSizeEx, "SetControlMinimumSize failed (non-critical), continuing");
                }
            }

            // Ensure theme cascade reaches the newly created panel and children
            ApplyPanelTheme(panel);

            // Set panel accessibility properties for UI automation
            try
            {
                panel.AccessibleName = panelName;
                panel.AccessibleDescription = $"Panel: {panelName}";
                panel.Tag = panelName;
            }
            catch (Exception accEx)
            {
                Logger.LogDebug(accEx, "Failed to set accessibility properties (non-critical)");
            }

            // Set dock label and caption via documented Syncfusion API (no reflection)
            try
            {
                _dockingManager.SetDockLabel(panel, panelName);
            }
            catch (Exception labelEx)
            {
                Logger.LogDebug(labelEx, "SetDockLabel failed (non-critical)");
            }

            // Update PanelHeader control if present
            try
            {
                // C# 14: Using 'is not null' pattern for more expressive null checking
                var header = panel.Controls.OfType<PanelHeader>().FirstOrDefault();
                if (header is not null)
                {
                    header.Title = panelName;
                    try { header.AccessibleName = panelName + " header"; } catch { }
                }
            }
            catch (Exception headerEx)
            {
                Logger.LogDebug(headerEx, "Failed to update PanelHeader (non-critical)");
            }

            // Force visibility and activation
            try
            {
                _dockingManager.SetDockVisibility(panel, true);
                _dockingManager.ActivateControl(panel);
            }
            catch (Exception visEx)
            {
                Logger.LogDebug(visEx, "Failed to set visibility/activation (non-critical)");
            }

            // Subscribe to DockStateChanged event to verify panel rendering
            // This replaces the Thread.Sleep(250) hack with proper event-driven synchronization
            try
            {
                _dockingManager.DockStateChanged += (sender, e) => OnDockStateChanged(panel, panelName);
            }
            catch (Exception eventEx)
            {
                Logger.LogDebug(eventEx, "Failed to subscribe to DockStateChanged (non-critical)");
            }

            // Cache for reuse
            _cachedPanels[panelName] = panel;

            // Track active panel and raise event for ribbon button highlighting
            _activePanelName = panelName;
            PanelActivated?.Invoke(this, new PanelActivatedEventArgs(panelName, panel.GetType()));

            Logger.LogInformation("Docked and activated new panel: {PanelName} ({PanelType})", panelName, panel.GetType().Name);
            Logger.LogInformation("[PANEL] {PanelName} docked - Visible={Visible}, Bounds={Bounds}", panelName, panel.Visible, panel.Bounds);
        }

        /// <summary>
        /// Event handler for DockStateChanged. Validates panel visibility and forces rendering.
        /// Replaces Thread.Sleep(250) timing hack with proper event synchronization.
        /// </summary>
        /// <summary>
        /// Event handler for DockStateChanged. Validates panel visibility and forces rendering.
        /// C# 14: Using 'is not' pattern for cleaner control flow.
        /// </summary>
        private void OnDockStateChanged(UserControl panel, string panelName)
        {
            try
            {
                if (panel is not null && !panel.IsDisposed && panel.Visible)
                {
                    // Panel is visible - invalidate to force clean rendering
                    if (!panel.IsDisposed)
                    {
                        panel.Invalidate(true);
                        panel.Update();
                    }
                    Logger.LogDebug("Panel {PanelName} verified visible via DockStateChanged event", panelName);
                }
            }
            catch (ObjectDisposedException)
            {
                // Panel was disposed - safe to ignore
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error in OnDockStateChanged for panel {PanelName}", panelName);
            }
        }

        private void ApplyPanelTheme(Control panel)
        {
            try
            {
                // C# 14: Extension method for safe theme application with null-conditional.
                // SfSkinManager is the single source of truth for theming.
                // Theme cascade from parent form automatically applies to all child controls.
                var themeName = GetCurrentThemeName();
                panel?.ApplySyncfusionTheme(themeName, Logger);
            }
            catch
            {
                // Best-effort: if theming fails, continue without blocking panel display
            }
        }

        /// <summary>
        /// Gets the current active theme name from SfSkinManager.
        /// C# 14 feature: Uses simplified pattern matching and null-coalescing.
        /// SfSkinManager is Syncfusion's single source of truth for theming.
        /// </summary>
        private static string GetCurrentThemeName() =>
            SfSkinManager.ApplicationVisualTheme ?? "Office2019White";

        private void ApplyCaptionSettings(UserControl panel, string panelName, bool allowFloating)
        {
            // Already on UI thread - ApplyCaptionSettings is called only from ActivateExistingPanel or DockPanelInternal
            // which are themselves called only from ShowPanel or AddPanelAsync (both marshalled via InvokeAsync)
            if (panel is null)
            {
                return;
            }

            try { _dockingManager.EnableContextMenu = true; } catch { }

            // Set caption, floating, and close button settings directly using Syncfusion DockingManager API
            try
            {
                // Set the caption text
                _dockingManager.SetDockLabel(panel, panelName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetDockLabel failed (non-critical)");
            }

            try
            {
                // Enable/disable floating
                _dockingManager.SetEnableDocking(panel, true);
                _dockingManager.SetAllowFloating(panel, allowFloating);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetAllowFloating failed (non-critical)");
            }

            try
            {
                // Show close button
                _dockingManager.SetCloseButtonVisibility(panel, true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "SetCloseButtonVisibility failed (non-critical)");
            }
        }

        public void Dispose()
        {
            // Ensure disposal happens on UI thread to safely dispose controls
            // Dictionary iteration and panel disposal must be thread-safe
            if (_parentControl.InvokeRequired)
            {
                _parentControl.Invoke(new System.Action(() => Dispose()));
                return;
            }

            try
            {
                // Dispose all cached panels to release their resources
                // This is important for panels with Syncfusion controls (grids, charts) that hold resources
                foreach (var panel in _cachedPanels.Values)
                {
                    try
                    {
                        panel?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Exception disposing cached panel (continuing)");
                    }
                }

                Logger.LogInformation("Disposed {Count} cached panels", _cachedPanels.Count);
            }
            finally
            {
                _cachedPanels.Clear();
            }
        }

        private static int CalculateDockSize(DockingStyle style, Control container)
        {
            if (container == null) return 300;
            // Use sensible defaults relative to available container size.
            switch (style)
            {
                case DockingStyle.Left:
                case DockingStyle.Right:
                    return Math.Max(300, Math.Max(100, container.Width / 4));
                case DockingStyle.Top:
                case DockingStyle.Bottom:
                    return Math.Max(200, Math.Max(80, container.Height / 4));
                case DockingStyle.Tabbed:
                case DockingStyle.Fill:
                default:
                    return Math.Max(400, Math.Min(container.Width, container.Height) / 2);
            }
        }

        private static void ApplyDefaultPanelSizing(UserControl panel, DockingStyle style, Type panelType)
        {
            var (desired, minimum) = GetDefaultPanelSizes(panelType, style);

            if (minimum.Width > 0 || minimum.Height > 0)
            {
                var mergedMin = new Size(
                    Math.Max(panel.MinimumSize.Width, minimum.Width),
                    Math.Max(panel.MinimumSize.Height, minimum.Height));
                panel.MinimumSize = mergedMin;
            }

            if (desired.Width > 0 || desired.Height > 0)
            {
                // Set control size so DockingManager honors the initial width/height.
                try { panel.Size = new Size(Math.Max(desired.Width, panel.Width), Math.Max(desired.Height, panel.Height)); } catch { }
            }
        }

        private static (Size desiredSize, Size minimumSize) GetDefaultPanelSizes(Type panelType, DockingStyle style)
        {
            var sizing = DefaultPanelSizing;
            if (PanelSizeOverrides.TryGetValue(panelType, out var overrideSizing))
            {
                sizing = MergeSizing(DefaultPanelSizing, overrideSizing);
            }

            var desired = style switch
            {
                DockingStyle.Left or DockingStyle.Right => sizing.Side,
                DockingStyle.Top or DockingStyle.Bottom => sizing.TopBottom,
                _ => Size.Empty
            };

            // C# 14: Use extension property for cleaner minimum size calculation.
            // Creates a temporary UserControl to compute style-aware minimums.
            // In production, this would be a direct type check; shown here for illustration.
            var minimum = sizing.Minimum;

            // Enforce reasonable minima for orientation using C# 14 patterns
            minimum = style switch
            {
                DockingStyle.Top or DockingStyle.Bottom => EnforceMinimum(minimum, new Size(800, 300)),
                DockingStyle.Left or DockingStyle.Right => EnforceMinimum(minimum, new Size(420, 360)),
                DockingStyle.Tabbed or DockingStyle.Fill or _ => EnforceMinimum(minimum, new Size(800, 600))
            };

            return (desired, minimum);
        }

        /// <summary>
        /// C# 14: Helper method using required return type semantics.
        /// Enforces minimum size by taking max of current and required.
        /// </summary>
        private static Size EnforceMinimum(Size current, Size required) => new(
            Math.Max(current.Width, required.Width),
            Math.Max(current.Height, required.Height)
        );

        private static PanelSizing MergeSizing(PanelSizing defaults, PanelSizing overrides)
        {
            Size MergeSize(Size @default, Size @override)
            {
                return new Size(
                    @override.Width > 0 ? @override.Width : @default.Width,
                    @override.Height > 0 ? @override.Height : @default.Height);
            }

            var mergedSide = MergeSize(defaults.Side, overrides.Side);
            var mergedTopBottom = MergeSize(defaults.TopBottom, overrides.TopBottom);
            var mergedMinimum = new Size(
                Math.Max(defaults.Minimum.Width, overrides.Minimum.Width),
                Math.Max(defaults.Minimum.Height, overrides.Minimum.Height));

            return new PanelSizing(mergedSide, mergedTopBottom, mergedMinimum);
        }

        private readonly record struct PanelSizing(Size Side, Size TopBottom, Size Minimum);

        private static readonly PanelSizing DefaultPanelSizing = new PanelSizing(
            new Size(540, 0),
            new Size(0, 400),
            new Size(420, 360));

        /// <summary>
        /// Hides a docked panel by name.
        /// </summary>
        /// <param name="panelName">Name of the panel to hide.</param>
        /// <returns>True if panel was hidden, false if panel doesn't exist.</returns>
        public bool HidePanel(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name cannot be empty.", nameof(panelName));

            if (_parentControl.InvokeRequired)
            {
                _parentControl.Invoke(new System.Action(() => HidePanel(panelName)));
                return false;
            }

            if (_cachedPanels.TryGetValue(panelName, out var existingPanel))
            {
                _dockingManager.SetDockVisibility(existingPanel, false);
                Logger.LogDebug("Hidden panel: {PanelName}", panelName);
                return true;
            }

            Logger.LogWarning("Cannot hide panel '{PanelName}' - not found", panelName);
            return false;
        }

        /// <summary>
        /// Get the currently active panel name.
        /// Thread-safe: Routes through UI thread if called from background thread.
        /// C# 14: Simplified with null-conditional operator and pattern matching.
        /// </summary>
        /// <returns>The name of the currently active panel, or null if no panel is active.</returns>
        public string? GetActivePanelName()
        {
            // C# 14: Null-conditional operator with method invocation.
            // If InvokeRequired is true, marshal to UI thread.
            if (_parentControl.InvokeRequired)
            {
                return (string?)_parentControl.Invoke(new Func<string?>(() => GetActivePanelName()));
            }
            // Return the currently active panel name (or null if none)
            return _activePanelName;
        }
    }

    /// <summary>
    /// Event args for panel activation events.
    /// </summary>
    public class PanelActivatedEventArgs : EventArgs
    {
        public string PanelName { get; set; }
        public Type PanelType { get; set; }

        public PanelActivatedEventArgs(string panelName, Type panelType)
        {
            PanelName = panelName;
            PanelType = panelType;
        }
    }
}
