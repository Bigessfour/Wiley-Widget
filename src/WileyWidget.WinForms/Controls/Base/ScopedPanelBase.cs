#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Factories;
using System.Diagnostics;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.Controls.Supporting;

namespace WileyWidget.WinForms.Controls.Base
{
    /// <summary>
    /// Non-generic base for type-erased runtime access (e.g., QuickAccess toolbar reflection).
    /// </summary>
    public abstract class ScopedPanelBase : UserControl, IThemable
    {
        // ── Canonical layout constants (Sacred Panel Skeleton §3) ──────────────────────────
        // Const int dimensions allow compile-time use; Size fields provide a convenient composite.

        /// <summary>Logical pixel width floor for docked / hero panels at 96 DPI.</summary>
        public const int RecommendedDockedPanelMinimumLogicalWidth = 1024;
        /// <summary>Logical pixel height floor for docked / hero panels at 96 DPI.</summary>
        public const int RecommendedDockedPanelMinimumLogicalHeight = 720;

        /// <summary>Logical pixel width floor for dialog-hosted panels at 96 DPI.</summary>
        public const int RecommendedDialogPanelMinimumLogicalWidth = 760;
        /// <summary>Logical pixel height floor for dialog-hosted panels at 96 DPI.</summary>
        public const int RecommendedDialogPanelMinimumLogicalHeight = 640;

        /// <summary>Logical pixel width floor for embedded / tab panels at 96 DPI.</summary>
        public const int RecommendedEmbeddedPanelMinimumLogicalWidth = 960;
        /// <summary>Logical pixel height floor for embedded / tab panels at 96 DPI.</summary>
        public const int RecommendedEmbeddedPanelMinimumLogicalHeight = 600;

        /// <summary>Minimum top padding (logical px) to prevent content clipping under docking captions.</summary>
        public const int RecommendedTopInsetLogical = 8;

        /// <summary>Full logical minimum size for docked / root panels.
        /// Derived from <see cref="RecommendedDockedPanelMinimumLogicalWidth"/> ×
        /// <see cref="RecommendedDockedPanelMinimumLogicalHeight"/>.</summary>
        public static readonly Size RecommendedDockedPanelMinimumLogicalSize =
            new(RecommendedDockedPanelMinimumLogicalWidth, RecommendedDockedPanelMinimumLogicalHeight);

        /// <summary>Full logical minimum size for dialog-hosted panels.
        /// Derived from <see cref="RecommendedDialogPanelMinimumLogicalWidth"/> ×
        /// <see cref="RecommendedDialogPanelMinimumLogicalHeight"/>.</summary>
        public static readonly Size RecommendedDialogPanelMinimumLogicalSize =
            new(RecommendedDialogPanelMinimumLogicalWidth, RecommendedDialogPanelMinimumLogicalHeight);

        /// <summary>Full logical minimum size for embedded / tab panels.
        /// Derived from <see cref="RecommendedEmbeddedPanelMinimumLogicalWidth"/> ×
        /// <see cref="RecommendedEmbeddedPanelMinimumLogicalHeight"/>.</summary>
        public static readonly Size RecommendedEmbeddedPanelMinimumLogicalSize =
            new(RecommendedEmbeddedPanelMinimumLogicalWidth, RecommendedEmbeddedPanelMinimumLogicalHeight);

        private bool _initialLayoutSuspended;
        private bool _applyingProfessionalLayout;
        protected bool _initialLoadStabilizationQueued;
        private const string ProfessionalContentHostName = "ScopedPanelContentHost";

        /// <summary>Type-erased ViewModel for runtime reflection access.</summary>
        public virtual object? UntypedViewModel => null;

        /// <summary>Content host panel for consistent layout. Created by ApplyProfessionalPanelLayout.</summary>
        protected Panel? ContentHost { get; private set; }

        protected ScopedPanelBase()
        {
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            if (this.AccessibleRole == AccessibleRole.Default)
            {
                this.AccessibleRole = AccessibleRole.Pane;
            }

            if (string.IsNullOrWhiteSpace(this.AccessibleName))
            {
                this.AccessibleName = GetType().Name;
            }

            SuspendLayout();
            _initialLayoutSuspended = true;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            if (_initialLayoutSuspended)
            {
                _initialLayoutSuspended = false;
                ResumeLayout(performLayout: false);
                PerformLayout();
            }
        }

        /// <summary>
        /// Applies professional panel layout: header, padding, content host, and theme.
        /// Called automatically from OnHandleCreated. Can be called manually for dynamic updates.
        /// </summary>
        protected virtual void ApplyProfessionalPanelLayout()
        {
            this.SuspendLayout();
            try
            {
                var controlFactory = GetControlFactory();
                var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;

                // Apply consistent padding using LayoutTokens
                this.Padding = LayoutTokens.GetScaled(LayoutTokens.PanelOuterPadding);
                this.Margin = Padding.Empty;
                this.AutoScroll = true;
                this.Dock = DockStyle.Fill;
                SfSkinManager.SetVisualStyle(this, themeName);

                // Create or normalize header
                var header = Controls.OfType<PanelHeader>().FirstOrDefault();
                if (header == null)
                {
                    header = FindNestedPanelHeader(this);
                    if (header != null)
                    {
                        PromoteNestedHeaderToShell(header);
                    }
                }

                if (header == null)
                {
                    header = controlFactory?.CreatePanelHeader(h =>
                    {
                        h.Title = this.Text ?? this.Name;
                        h.ShowRefreshButton = false;
                        h.ShowHelpButton = false;
                        h.ShowPinButton = false;
                        h.ShowCloseButton = true;
                    }) ?? new PanelHeader
                    {
                        Title = this.Text ?? this.Name,
                        ShowRefreshButton = false,
                        ShowHelpButton = false,
                        ShowPinButton = false,
                        ShowCloseButton = true,
                    };

                    header.CloseClicked += (s, e) =>
                    {
                        var navigationService = GetNavigationService();
                        navigationService?.HidePanel(this.GetType().Name);
                    };

                    this.Controls.Add(header);
                }

                header.Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);
                header.Dock = DockStyle.Top;
                SfSkinManager.SetVisualStyle(header, themeName);
                if (string.IsNullOrWhiteSpace(header.Title))
                {
                    header.Title = this.Text ?? this.Name;
                }

                // Create or normalize content host for consistent layout
                var contentHost = ContentHost;
                if (contentHost == null || contentHost.IsDisposed || !Controls.Contains(contentHost))
                {
                    contentHost = Controls
                        .OfType<Panel>()
                        .FirstOrDefault(panel => string.Equals(panel.Name, ProfessionalContentHostName, StringComparison.Ordinal));
                }

                if (contentHost == null || contentHost.IsDisposed)
                {
                    contentHost = controlFactory?.CreatePanel() ?? new Panel();
                }

                contentHost.Name = ProfessionalContentHostName;
                contentHost.Dock = DockStyle.Fill;
                contentHost.Padding = LayoutTokens.GetScaled(LayoutTokens.ContentInnerPadding);
                contentHost.Margin = Padding.Empty;
                SfSkinManager.SetVisualStyle(contentHost, themeName);

                if (!this.Controls.Contains(contentHost))
                {
                    this.Controls.Add(contentHost);
                }

                ContentHost = contentHost;

                // Move existing controls into contentHost (migration aid)
                var controlsToMove = Controls.Cast<Control>()
                    .Where(c => c != header && c != contentHost)
                    .ToList();

                foreach (var ctrl in controlsToMove)
                {
                    Controls.Remove(ctrl);
                    contentHost.Controls.Add(ctrl);
                }

                header.BringToFront();
            }
            finally
            {
                this.ResumeLayout(true);
                this.PerformLayout();
            }
        }

        private static PanelHeader? FindNestedPanelHeader(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is PanelHeader panelHeader)
                {
                    return panelHeader;
                }

                var nestedHeader = FindNestedPanelHeader(child);
                if (nestedHeader != null)
                {
                    return nestedHeader;
                }
            }

            return null;
        }

        private void PromoteNestedHeaderToShell(PanelHeader header)
        {
            var parent = header.Parent;
            if (parent == null || parent == this)
            {
                return;
            }

            if (parent is TableLayoutPanel tableLayout)
            {
                var position = tableLayout.GetPositionFromControl(header);
                tableLayout.Controls.Remove(header);

                if (position.Row >= 0 && position.Row < tableLayout.RowStyles.Count)
                {
                    tableLayout.RowStyles[position.Row].SizeType = SizeType.Absolute;
                    tableLayout.RowStyles[position.Row].Height = 0;
                }
            }
            else
            {
                parent.Controls.Remove(header);
            }

            header.Dock = DockStyle.Top;
            header.Margin = Padding.Empty;
            Controls.Add(header);
            header.BringToFront();
        }

        /// <summary>Attempts to resolve SyncfusionControlFactory from DI container.</summary>
        protected virtual SyncfusionControlFactory? GetControlFactory()
        {
            try
            {
                var provider = GetServiceProvider();
                if (provider == null)
                {
                    return null;
                }

                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<SyncfusionControlFactory>(provider);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Attempts to resolve IPanelNavigationService from DI container.</summary>
        protected virtual IPanelNavigationService? GetNavigationService()
        {
            try
            {
                var provider = GetServiceProvider();
                if (provider == null)
                {
                    return null;
                }

                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(provider);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Attempts to get IServiceProvider from the current context.</summary>
        protected virtual IServiceProvider? GetServiceProvider()
        {
            // Try to get from parent form if it's MainForm
            var mainForm = FindForm() as MainForm;
            if (mainForm != null)
            {
                return mainForm.ServiceProvider;
            }

            // Fallback: try to get from application context
            var mainFormFromApp = Application.OpenForms.Cast<Form>()
                .FirstOrDefault(f => f is MainForm) as MainForm;
            return mainFormFromApp?.ServiceProvider;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (Dock == DockStyle.None)
            {
                Dock = DockStyle.Fill;
            }

            if (MinimumSize == Size.Empty)
            {
                MinimumSize = GetRecommendedMinimumSize();
            }

            // Apply theme first
            var activeTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            ApplyTheme(activeTheme);
            SyncfusionControlFactory.ApplyThemeToAllControls(this, activeTheme);

            EnforceProfessionalLayoutContract();
            ResetAutoScrollOffsets(this);

            PerformLayout();
            Invalidate(true);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            EnforceProfessionalLayoutContract();
        }

        private void EnforceProfessionalLayoutContract()
        {
            if (IsDisposed || Disposing || _applyingProfessionalLayout)
            {
                return;
            }

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            try
            {
                _applyingProfessionalLayout = true;
                ApplyProfessionalPanelLayout();
                EnsureMinimumTopInset();
            }
            finally
            {
                _applyingProfessionalLayout = false;
            }
        }

        /// <inheritdoc/>
        public virtual void ApplyTheme(string themeName)
        {
            if (IsDisposed || Disposing || string.IsNullOrWhiteSpace(themeName))
            {
                return;
            }

            void ApplyCore()
            {
                try
                {
                    ThemeColors.EnsureThemeAssemblyLoadedForTheme(themeName);
                    SyncfusionControlFactory.ApplyThemeToAllControls(this, themeName);
                    Invalidate(true);
                    Update();
                }
                catch
                {
                    // Best-effort: theme reapply should never crash panel rendering.
                }
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)ApplyCore);
            }
            else
            {
                ApplyCore();
            }
        }

        protected virtual int GetMinimumTopInsetLogical() => RecommendedTopInsetLogical;

        protected void EnsureMinimumTopInset()
        {
            var minimumTopInsetLogical = GetMinimumTopInsetLogical();
            if (minimumTopInsetLogical <= 0)
            {
                return;
            }

            var minimumTopInset = ScaleLogicalToDevice(new Size(0, minimumTopInsetLogical)).Height;
            if (Padding.Top >= minimumTopInset)
            {
                return;
            }

            Padding = new Padding(Padding.Left, minimumTopInset, Padding.Right, Padding.Bottom);
        }

        protected static void ResetAutoScrollOffsets(Control root)
        {
            if (root is ScrollableControl scrollable && scrollable.AutoScroll)
            {
                try
                {
                    scrollable.AutoScrollPosition = Point.Empty;
                }
                catch
                {
                    // Best-effort reset for controls that reject AutoScrollPosition assignment.
                }
            }

            foreach (Control child in root.Controls)
            {
                ResetAutoScrollOffsets(child);
            }
        }

        protected virtual Size GetRecommendedMinimumSize()
        {
            var hostForm = FindForm();
            var isDialogHost = hostForm?.Modal == true ||
                               hostForm?.FormBorderStyle == FormBorderStyle.FixedDialog ||
                               hostForm?.FormBorderStyle == FormBorderStyle.FixedToolWindow ||
                               hostForm?.FormBorderStyle == FormBorderStyle.SizableToolWindow;

            var logicalSize = isDialogHost
                ? RecommendedDialogPanelMinimumLogicalSize
                : RecommendedDockedPanelMinimumLogicalSize;

            return ScaleLogicalToDevice(logicalSize);
        }

        protected Size ScaleLogicalToDevice(Size logicalSize)
        {
            var dpi = DeviceDpi <= 0 ? 96 : DeviceDpi;
            if (dpi == 96)
            {
                return logicalSize;
            }

            var scale = dpi / 96f;
            return new Size(
                (int)Math.Ceiling(logicalSize.Width * scale),
                (int)Math.Ceiling(logicalSize.Height * scale));
        }

        /// <summary>
        /// Canonical layout-suspension wrapper mandated by the Sacred Panel Skeleton
        /// (WileyWidgetUIStandards §1 and §3).  Call once from the panel constructor—
        /// typically via a private <c>InitializeLayout()</c> delegate—to batch all
        /// control creation inside a single <c>SuspendLayout</c> / <c>ResumeLayout</c>
        /// + <c>PerformLayout</c> cycle.  This is the only approved way to initialise
        /// panel controls; do not call <c>SuspendLayout</c>/<c>ResumeLayout</c> manually.
        /// </summary>
        /// <param name="buildAction">The layout-build delegate. Must not be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buildAction"/> is <see langword="null"/>.</exception>
        protected void SafeSuspendAndLayout(Action buildAction)
        {
            if (buildAction is null)
                throw new ArgumentNullException(nameof(buildAction));

            SuspendLayout();
            try
            {
                buildAction();
            }
            finally
            {
                ResumeLayout(performLayout: false);
                PerformLayout();
            }
        }

        /// <summary>
        /// Public entry point for external callers (e.g.
        /// main form docking/layout callbacks) to trigger a post-dock layout
        /// refresh on this panel (Standards Req 3).
        /// Derived generic panels override this to call the richer <c>ForceFullLayout()</c>.
        /// </summary>
        public virtual void TriggerForceFullLayout()
        {
            if (IsDisposed || !IsHandleCreated) return;
            SuspendLayout();
            try
            {
                PerformLayout();
                Invalidate(true);
                Update();
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }
        }
    }

    /// <summary>
    /// Generic DI-scoped panel base class. Manages a single DI scope, resolves the ViewModel,
    /// and provides lifecycle helpers (load, save, validate, close, operation tracking).
    /// </summary>
    public abstract class ScopedPanelBase<TViewModel> : ScopedPanelBase, ICompletablePanel
        where TViewModel : class, INotifyPropertyChanged
    {
        protected readonly IServiceScope? _scope;
        protected readonly ILogger _logger;

        /// <summary>Logger property alias for backward compatibility with panels using PascalCase Logger.</summary>
        protected ILogger Logger => _logger;

        protected TViewModel? ViewModel { get; set; }

        /// <inheritdoc/>
        public override object? UntypedViewModel => ViewModel;

        protected bool IsLoaded { get; set; }
        protected bool IsBusy { get; set; }
        protected bool HasUnsavedChanges { get; private set; }

        /// <summary>Provides access to the DI service provider from the panel's scope.</summary>
        protected IServiceProvider ServiceProvider => _scope?.ServiceProvider
            ?? WileyWidget.WinForms.Program.ServicesOrNull
            ?? throw new InvalidOperationException($"No service provider is available for {GetType().Name}.");

        /// <summary>Factory for creating Syncfusion controls with mandatory properties pre-set.</summary>
        protected SyncfusionControlFactory ControlFactory =>
            ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(ServiceProvider);

        /// <summary>Accumulated validation errors — cleared and repopulated by ValidateAsync overrides.</summary>
        protected List<ValidationItem> ValidationErrors { get; } = new();

        /// <inheritdoc/>
        protected override SyncfusionControlFactory? GetControlFactory()
        {
            try
            {
                var provider = GetServiceProvider();
                return provider == null
                    ? null
                    : ServiceProviderServiceExtensions.GetService<SyncfusionControlFactory>(provider);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        protected override IPanelNavigationService? GetNavigationService()
        {
            var provider = GetServiceProvider();
            return provider == null
                ? null
                : ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(provider);
        }

        /// <inheritdoc/>
        protected override IServiceProvider? GetServiceProvider() =>
            _scope?.ServiceProvider ?? WileyWidget.WinForms.Program.ServicesOrNull;

        private CancellationTokenSource? _operationCts;
        private System.Windows.Forms.Timer? _finalLayoutTimer;
        private bool _shownFired;
        private Form? _hostFormForClosing;
        private IThemeService? _themeService;
        private EventHandler<string>? _themeChangedHandler;

        protected ScopedPanelBase(IServiceScopeFactory scopeFactory, ILogger logger)
        {
            _scope = scopeFactory.CreateScope();
            _logger = logger;

            // Resolve ViewModel once from scope
            ViewModel = ServiceProviderServiceExtensions.GetService<TViewModel>(_scope.ServiceProvider);

            // Notify derived panels that the ViewModel is available
            OnViewModelResolved(ViewModel);
            _logger?.LogDebug("[{Panel}] Initialized — ViewModel: {VmType}",
                GetType().Name, ViewModel?.GetType().Name ?? $"{typeof(TViewModel).Name} (null)");

            AttachThemeService(_scope.ServiceProvider);
        }

        /// <summary>
        /// Direct-injection constructor: accepts a pre-built ViewModel and optional logger.
        /// Used by the Sacred Panel Skeleton (WileyWidgetUIStandards §1) when the DI lifetime
        /// is managed externally and a <see cref="SyncfusionControlFactory"/> is injected
        /// directly into the panel constructor.  The scope is not created in this path;
        /// <see cref="ControlFactory"/> is unavailable — panels must use the injected factory.
        /// </summary>
        /// <param name="vm">Pre-built ViewModel instance supplied by the DI container.</param>
        /// <param name="logger">Optional logger; defaults to <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance"/>.</param>
        protected ScopedPanelBase(TViewModel vm, ILogger? logger = null)
        {
            _scope = null;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            ViewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            OnViewModelResolved(ViewModel);
            _logger.LogDebug("[{Panel}] Initialized (direct-injection) — ViewModel: {VmType}",
                GetType().Name, ViewModel.GetType().Name);

            AttachThemeService(WileyWidget.WinForms.Program.ServicesOrNull);
        }

        protected sealed override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AttachHostFormClosingHandler();
            OnPanelLoaded(e);
            QueueInitialLoadAndLayoutStabilization();
        }

        protected virtual void OnPanelLoaded(EventArgs e)
        {
        }

        private void QueueInitialLoadAndLayoutStabilization()
        {
            if (_initialLoadStabilizationQueued || IsDisposed || Disposing || DesignMode || ViewModel == null)
            {
                return;
            }

            _initialLoadStabilizationQueued = true;

            try
            {
                BeginInvoke((MethodInvoker)(() => _ = StabilizeInitialLoadAndLayoutAsync()));
            }
            catch (Exception ex)
            {
                _initialLoadStabilizationQueued = false;
                _logger?.LogDebug(ex, "[{Panel}] Failed to queue initial load/layout stabilization", GetType().Name);
            }
        }

        private async Task StabilizeInitialLoadAndLayoutAsync()
        {
            try
            {
                if (!IsLoaded && !IsBusy)
                {
                    await Task.Delay(50);
                    await LoadAsync(CancellationToken.None);
                }

                await Task.Delay(150);

                if (IsDisposed || Disposing)
                {
                    return;
                }

                ApplyProfessionalPanelLayout();
                ForceFullLayout();
                PerformLayout();
                Invalidate(true);
                Update();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[{Panel}] Initial load/layout stabilization failed", GetType().Name);
            }
        }

        protected virtual void OnViewModelResolved(TViewModel? vm) { }
        protected virtual void OnViewModelResolved(object? vm)
        {
            if (vm is TViewModel typed) OnViewModelResolved(typed);
        }
        protected virtual void OnThemeChanged(string themeName) { }

        private void AttachThemeService(IServiceProvider? provider)
        {
            try
            {
                _themeService = provider?.GetService(typeof(IThemeService)) as IThemeService
                    ?? WileyWidget.WinForms.Program.ServicesOrNull?.GetService(typeof(IThemeService)) as IThemeService;

                if (_themeService == null)
                {
                    return;
                }

                _themeChangedHandler ??= HandleThemeServiceChanged;
                _themeService.ThemeChanged -= _themeChangedHandler;
                _themeService.ThemeChanged += _themeChangedHandler;

                ApplyTheme(_themeService.CurrentTheme);
                OnThemeChanged(_themeService.CurrentTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[{Panel}] Theme service subscription failed", GetType().Name);
            }
        }

        private void HandleThemeServiceChanged(object? sender, string themeName)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            InvokeOnUiThread(() =>
            {
                ApplyTheme(themeName);
                OnThemeChanged(themeName);
            });
        }

        /// <summary>
        /// Cancels any in-flight operation and starts a new one, returning its cancellation token.
        /// </summary>
        protected CancellationToken RegisterOperation()
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();
            return _operationCts.Token;
        }

        /// <summary>
        /// Hides this panel via the navigation service (panel stays alive in memory, DI scope preserved).
        /// Falls back to <c>Visible = false</c> when MainForm is not reachable (e.g. design-time).
        /// </summary>
        protected virtual void ClosePanel()
        {
            // GetType().Name is always registered as a panel alias by CachePanelAliases, so it
            // is the most reliable key regardless of the DisplayName used at ShowPanel time.
            var panelName = GetType().Name;

            // The panel can be hosted directly by MainForm or inside a floating wrapper Form
            // whose Owner is MainForm.  Walk both levels.
            var hostForm = FindForm();
            var mainForm = hostForm as MainForm
                        ?? hostForm?.Owner as MainForm;

            // Walk up the parent control chain — handles DockingManager + nested hosting in 32.2.3
            if (mainForm == null)
            {
                Control? p = Parent;
                while (p != null)
                {
                    if (p is MainForm mf) { mainForm = mf; break; }
                    p = p.Parent;
                }
            }

            if (mainForm != null)
            {
                // Check if this is a floating form (not the main form itself)
                if (hostForm != null && hostForm != mainForm)
                {
                    // For floating forms, close the host form directly
                    hostForm.Close();
                    _logger?.LogDebug("[{Panel}] ClosePanel → HostForm.Close() for floating panel", GetType().Name);
                }
                else
                {
                    // For docked panels, use MainForm.ClosePanel
                    mainForm.ClosePanel(panelName);
                    _logger?.LogDebug("[{Panel}] ClosePanel → MainForm.ClosePanel({Name})", GetType().Name, panelName);
                }
            }
            else
            {
                Visible = false;
                _logger?.LogDebug("[{Panel}] ClosePanel fallback: MainForm not reachable, set Visible=false", GetType().Name);
            }
        }

        private void AttachHostFormClosingHandler()
        {
            var hostForm = FindForm();
            if (ReferenceEquals(_hostFormForClosing, hostForm))
            {
                return;
            }

            if (_hostFormForClosing != null)
            {
                _hostFormForClosing.FormClosing -= HostForm_FormClosing;
            }

            _hostFormForClosing = hostForm;
            if (_hostFormForClosing != null)
            {
                _hostFormForClosing.FormClosing += HostForm_FormClosing;
            }
        }

        private void HostForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Allow host forms to close normally and avoid recursive ClosePanel calls.
            // PanelNavigationService cleans up hosts on FormClosed.
            if (sender is MainForm && !e.Cancel)
            {
                // For MainForm closures, do not re-route; let the application close naturally.
                return;
            }
        }

        /// <summary>
        /// Forces a recursive layout pass on this panel and its first two levels of children.
        /// Called via <see cref="BeginInvoke"/> after the Syncfusion DockingManager finishes
        /// sizing the hosted <see cref="UserControl"/>, so that content controls render at their
        /// correct positions instead of collapsing to 0×0 during the initial docking pass.
        /// Override in derived panels to target deeper containers (e.g., SplitContainerAdv inner panels).
        /// </summary>
        protected virtual void ForceFullLayout()
        {
            if (IsDisposed || !IsHandleCreated) return;

            SuspendLayout();
            try
            {
                PerformLayoutRecursive(this);
                Invalidate(true);
                Update();
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }

            _logger?.LogDebug("[{Panel}] ForceFullLayout completed ({W}x{H})", GetType().Name, Width, Height);
        }

        /// <summary>
        /// Public bridge that allows external callers — such as
        /// main form docking/layout callbacks — to invoke
        /// <see cref="ForceFullLayout"/> after docking/resize operations
        /// complete (Standards Req 3).
        /// </summary>
        /// <inheritdoc/>
        public override void TriggerForceFullLayout() => ForceFullLayout();

        private static void PerformLayoutRecursive(Control control)
        {
            control.PerformLayout();
            foreach (Control child in control.Controls)
            {
                PerformLayoutRecursive(child);
            }
        }

        /// <summary>
        /// Clears all accumulated validation errors from <see cref="ValidationErrors"/>.
        /// </summary>
        protected void ClearValidationErrors() => ValidationErrors.Clear();

        /// <summary>
        /// Replaces the current validation errors with the provided items.
        /// </summary>
        protected void SetValidationErrors(IEnumerable<ValidationItem> errors)
        {
            ValidationErrors.Clear();
            ValidationErrors.AddRange(errors);
        }

        /// <summary>
        /// Runs <see cref="LoadAsync"/> in a fire-and-forget context, swallowing exceptions to
        /// the logger so callers are not blocked.
        /// </summary>
        protected async void LoadAsyncSafe()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await LoadAsync(CancellationToken.None);
                _logger?.LogDebug("[{Panel}] LoadAsync completed in {Ms}ms", GetType().Name, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LoadAsyncSafe failed in {Panel} after {Ms}ms", GetType().Name, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Dispatches <paramref name="action"/> to the UI thread; safe to call from any thread.
        /// </summary>
        protected void InvokeOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        public virtual async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsLoaded) return;
            IsBusy = true;
            try
            {
                if (ViewModel is IAsyncInitializable init)
                    await init.InitializeAsync(ct);

                IsLoaded = true;
            }
            finally { IsBusy = false; }
        }

        public virtual Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public virtual Task<ValidationResult> ValidateAsync(CancellationToken ct = default) => Task.FromResult(ValidationResult.Success);
        public virtual void FocusFirstError() { }

        protected void SetHasUnsavedChanges(bool value) => HasUnsavedChanges = value;

        /// <summary>
        /// Called the first time this panel becomes visible inside the docking host.
        /// Starts a one-shot 180ms timer that fires <see cref="ForceFullLayout"/> after
        /// the host finishes its resize pass.  Override in derived panels to add
        /// panel-specific splitter configuration inside a <c>BeginInvoke</c> callback
        /// — always call <c>base.OnShown(e)</c> first to start the timer.
        /// </summary>
        protected virtual void OnShown(EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;

            _finalLayoutTimer?.Stop();
            _finalLayoutTimer?.Dispose();
            _finalLayoutTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _finalLayoutTimer.Tick += (s, _) =>
            {
                _finalLayoutTimer?.Stop();
                _finalLayoutTimer?.Dispose();
                _finalLayoutTimer = null;
                if (!IsDisposed && IsHandleCreated)
                    ForceFullLayout();
            };
            _finalLayoutTimer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _logger?.LogTrace("[{Panel}] Visibility → {Visible} ({W}x{H})", GetType().Name, Visible, Width, Height);
            if (Visible)
            {
                EnsureMinimumTopInset();
                ResetAutoScrollOffsets(this);
            }
            QueueDeferredLayoutPass(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Visible)
            {
                QueueDeferredLayoutPass(e);
            }
        }

        private void QueueDeferredLayoutPass(EventArgs e)
        {
            // Queue a deferred layout pass so the docking host has already finished resizing
            // this UserControl before we force children to lay out at the correct dimensions.
            if (!Visible || IsDisposed || !IsHandleCreated || Width <= 0 || Height <= 0)
            {
                return;
            }

            if (!_shownFired)
            {
                _shownFired = true;
                OnShown(e);
            }

            // ✅ PERF FIX: Remove immediate ForceFullLayout call to prevent layout thrashing
            // The OnShown timer (180ms) will handle the final layout pass after all resize operations complete
            // This prevents 7+ redundant layout calls within 200ms (one per Visible/SizeChanged event)
        }

        /// <summary>
        /// Emits a structured lifecycle debug landmark: <c>[PanelType] EventName: Detail</c>.
        /// Panels call this to add consistent log entries without repeating the message format.
        /// </summary>
        protected void LogPanelLifecycle(string eventName, string? detail = null)
        {
            if (detail is null)
                _logger?.LogDebug("[{Panel}] {Event}", GetType().Name, eventName);
            else
                _logger?.LogDebug("[{Panel}] {Event}: {Detail}", GetType().Name, eventName, detail);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _logger?.LogDebug("[{Panel}] Disposing", GetType().Name);

                    if (_themeService != null && _themeChangedHandler != null)
                    {
                        _themeService.ThemeChanged -= _themeChangedHandler;
                    }

                    if (_hostFormForClosing != null)
                    {
                        _hostFormForClosing.FormClosing -= HostForm_FormClosing;
                        _hostFormForClosing = null;
                    }

                    _operationCts?.Cancel();
                    _operationCts?.Dispose();
                    _finalLayoutTimer?.Stop();
                    _finalLayoutTimer?.Dispose();
                    _finalLayoutTimer = null;

                    if (ContextMenuStrip != null)
                    {
                        ContextMenuStrip.Dispose();
                        ContextMenuStrip = null;
                    }

                    _scope?.Dispose();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _logger?.LogWarning(ex, "[{Panel}] Dispose encountered a non-fatal exception", GetType().Name);
                }
                catch
                {
                    // Never allow dispose logging failures to bubble up.
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
