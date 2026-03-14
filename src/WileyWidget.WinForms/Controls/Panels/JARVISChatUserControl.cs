using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Automation;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Hosted JARVIS chat control using Blazor WebView for rich component integration.
    /// This control is managed by ScopedPanelBase and initialized via IAsyncInitializable.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase<CommunityToolkit.Mvvm.ComponentModel.ObservableObject>, IAsyncInitializable, IParameterizedPanel
    {
        private static readonly TimeSpan StartupInitializationDeferWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan StartupInitializationDelay = TimeSpan.FromMilliseconds(1500);
        private static readonly TimeSpan StandardInitializationDelay = TimeSpan.FromMilliseconds(150);
        private const int DefaultPanelWidth = 1280;
        private const int DefaultPanelHeight = 900;
        private const int MinimumPanelWidth = 420;
        private const int MinimumPanelHeight = 480;

        private BlazorWebView? _blazorWebView;
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private IChatBridgeService? _chatBridge;
        private IThemeService? _themeService;
        private EventHandler<string>? _themeChangedHandler;
        private bool _isBlazorReady;
        private int _offlineNoticePublished;
        private string? _pendingOfflineNotice;
        private PanelHeader? _panelHeader;
        private Panel? _contentHost;
        private LoadingOverlay? _loadingOverlay;
        private System.Windows.Forms.Timer? _deferredInitializationTimer;
        private readonly DateTime _createdUtc = DateTime.UtcNow;
        private int _initializationTriggered;
        private bool _isAiWarmupCompleted;
        private JarvisAutomationState? _automationState;
        private EventHandler<JarvisAutomationStateChangedEventArgs>? _automationStateChangedHandler;

        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        public TextBoxExt? AutomationStatusBox { get; private set; }

        public JARVISChatUserControl(
            IServiceScopeFactory scopeFactory,
            IServiceProvider serviceProvider,
            ILogger<JARVISChatUserControl> logger)
            : base(scopeFactory, logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            SafeSuspendAndLayout(InitializeComponent);
            InitializeHostChrome();
            ShowLoadingState("Getting chat ready...");

            EnsureAutomationStatusBoxPresent();

            this.Dock = DockStyle.Fill;
        }

        // BlazorWebView fills the entire tab content area via Dock=Fill.
        // No top inset padding is needed — the TabControlAdv header already provides visual separation.
        protected override int GetMinimumTopInsetLogical() => 0;

        private void InitializeHostChrome()
        {
            _panelHeader = new PanelHeader(ControlFactory)
            {
                Dock = DockStyle.Top,
                Title = "JARVIS Assistant",
                MinimumSize = new Size(0, LayoutTokens.HeaderHeight),
                Height = LayoutTokens.HeaderHeight,
                ShowRefreshButton = false,
                ShowHelpButton = false,
                ShowPinButton = false,
                ShowCloseButton = true
            };
            _panelHeader.CloseClicked += (_, _) => ClosePanel();

            _contentHost = ControlFactory.CreatePanel(panel =>
            {
                panel.Name = "JarvisContentHost";
                panel.Dock = DockStyle.Fill;
                panel.Margin = Padding.Empty;
                panel.Padding = Padding.Empty;
            });

            EnsureLoadingOverlayPresent();

            if (!Controls.Contains(_contentHost))
            {
                Controls.Add(_contentHost);
            }

            if (!Controls.Contains(_panelHeader))
            {
                Controls.Add(_panelHeader);
                _panelHeader.BringToFront();
            }
        }

        private void EnsureAutomationStatusBoxPresent()
        {
            var isUiTestMode = string.Equals(
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (AutomationStatusBox == null || AutomationStatusBox.IsDisposed)
            {
                AutomationStatusBox = ControlFactory.CreateTextBoxExt(textBox =>
                {
                    textBox.Name = "JarvisAutomationStatus";
                    textBox.AccessibleName = "JarvisAutomationStatus";
                    textBox.ReadOnly = true;
                    textBox.BorderStyle = BorderStyle.None;
                    textBox.Multiline = true;
                    textBox.ScrollBars = ScrollBars.Vertical;
                    textBox.Dock = DockStyle.Bottom;
                    textBox.Height = LayoutTokens.StatusBarHeight;
                    textBox.Visible = isUiTestMode;
                    textBox.Text = "Automation state: Pending...";
                });
            }
            else
            {
                AutomationStatusBox.Visible = isUiTestMode;
            }

            if (!this.Controls.Contains(AutomationStatusBox))
            {
                this.Controls.Add(AutomationStatusBox);
            }
        }

        public void InitializeWithParameters(object parameters)
        {
            if (parameters is string prompt)
            {
                InitialPrompt = prompt;
                Logger?.LogInformation("[JARVIS-PARAM] Initial prompt set: {Length} chars", prompt.Length);
            }
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            if (_isInitialized || IsDisposed)
            {
                Logger?.LogDebug(
                    "[JARVIS-LIFECYCLE] InitializeAsync early-return — _isInitialized={IsInit}, IsDisposed={IsDisposed}",
                    _isInitialized, IsDisposed);
                return;
            }

            await _initLock.WaitAsync(ct);
            try
            {
                if (_isInitialized || IsDisposed) return;

                // Ensure automation status box exists before any initialization
                EnsureAutomationStatusBoxPresent();
                _isBlazorReady = false;
                _isAiWarmupCompleted = false;
                ShowLoadingState("Getting chat ready...");

                Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing BlazorWebView panel host...");
                await InitializeBlazorWebViewAsync(ct);

                // Ensure AI Service is initialized in this scope
                var aiService = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(ServiceProvider) : null)
                                ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(_serviceProvider);

                if (aiService == null)
                {
                    await PublishAiOfflineNoticeAsync("⚠️ Grok is currently offline. AI functions are unavailable. Type /xai-setup in chat for guided key setup.").ConfigureAwait(false);
                    MarkAiWarmupCompleted();
                }
                else
                {
                    ShowLoadingState("Connecting AI services...");
                    await WarmupAiAsync(aiService, ct).ConfigureAwait(false);
                }

                // Activate the bridge handler by resolving it from the scoped provider
                _ = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(ServiceProvider) : null)
                    ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(_serviceProvider);

                // Update status from real automation state (still works in prod)
                var automationState = GetAutomationStateService();
                if (automationState != null && AutomationStatusBox != null)
                {
                    AutomationStatusBox.Text = automationState.Snapshot.ToStatusString();

                    if (!ReferenceEquals(_automationState, automationState))
                    {
                        UnsubscribeAutomationState();
                        _automationState = automationState;
                    }

                    if (_automationStateChangedHandler == null)
                    {
                        _automationStateChangedHandler = (s, e) =>
                        {
                            if (AutomationStatusBox == null || AutomationStatusBox.IsDisposed)
                            {
                                return;
                            }

                            if (InvokeRequired)
                            {
                                BeginInvoke(() => AutomationStatusBox.Text = e.Snapshot.ToStatusString());
                            }
                            else
                            {
                                AutomationStatusBox.Text = e.Snapshot.ToStatusString();
                            }
                        };

                        _automationState.Changed += _automationStateChangedHandler;
                    }
                }

                _isInitialized = true;
                TryHideLoadingState();

                // Single layout pass after all controls added
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        if (!IsDisposed)
                        {
                            this.PerformLayout();
                            this.Invalidate(true);
                        }
                    });
                }

                Logger?.LogInformation("[JARVIS-LIFECYCLE] JARVIS BlazorWebView panel initialization successful");
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _initializationTriggered, 0);
                HideLoadingState();
                Logger?.LogError(ex, "[JARVIS-LIFECYCLE] Failed to initialize JARVIS BlazorWebView panel");
                ShowError(ex.Message);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task InitializeBlazorWebViewAsync(CancellationToken ct)
        {
            if (InvokeRequired)
            {
                var tcs = new TaskCompletionSource();
                BeginInvoke(new System.Action(async () =>
                {
                    try
                    {
                        await InitializeBlazorWebViewInternal(ct);
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                await tcs.Task;
            }
            else
            {
                await InitializeBlazorWebViewInternal(ct);
            }
        }

        private async Task InitializeBlazorWebViewInternal(CancellationToken ct)
        {
            if (IsDisposed) return;

            // Get services
            _chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(_serviceProvider);
            _themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(_serviceProvider);
            var automationState = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Automation.JarvisAutomationState>(_serviceProvider);

            // Skip BlazorWebView creation in test/headless mode
            var isTestMode = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            if (isTestMode)
            {
                Logger?.LogInformation("[JARVIS-INIT] Skipping BlazorWebView creation in test mode");
                _isBlazorReady = true;
                automationState?.MarkBlazorReady(assistViewReady: true); // Mark both as ready in test mode
                TryHideLoadingState();

                // Mark diagnostics as completed for automation
                automationState?.MarkDiagnosticsCompleted();

                return;
            }

            // Create BlazorWebView
            var hostPagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            Logger?.LogDebug("[JARVIS-INIT] wwwroot/index.html path={Path}, Exists={Exists}", hostPagePath, System.IO.File.Exists(hostPagePath));
            if (!System.IO.File.Exists(hostPagePath))
            {
                Logger?.LogError("[JARVIS-INIT] wwwroot/index.html NOT FOUND at {Path} — BlazorWebView will fail to load", hostPagePath);
            }

            _blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html"
            };

            if (_contentHost == null || _contentHost.IsDisposed)
            {
                InitializeHostChrome();
            }

            _contentHost!.SuspendLayout();
            try
            {
                _contentHost.Controls.Clear();
                _contentHost.Controls.Add(_blazorWebView);
                if (_loadingOverlay != null && !_loadingOverlay.IsDisposed)
                {
                    _contentHost.Controls.Add(_loadingOverlay);
                    _loadingOverlay.BringToFront();
                }
                _blazorWebView.SendToBack();
                EnsureAutomationStatusBoxPresent();
            }
            finally
            {
                _contentHost.ResumeLayout(performLayout: false);
                _contentHost.PerformLayout();
            }

            // Set up services and root component
            _blazorWebView.Services = ServiceProvider ?? _serviceProvider;
            _blazorWebView.RootComponents.Clear();

            Logger?.LogInformation("[JARVIS-INIT] BlazorWebView services configured, awaiting runtime component registration");

            // Theme sync
            _themeChangedHandler = (_, themeName) => SyncThemeWithBlazor(themeName, _themeService?.IsDark ?? false);
            _themeService.ThemeChanged += _themeChangedHandler;

            // Mark Blazor ready for automation
            automationState?.MarkBlazorReady(assistViewReady: false);

            // Handle Blazor initialization
            _blazorWebView.BlazorWebViewInitialized += (s, e) =>
            {
                Logger?.LogInformation("[JARVIS-LIFECYCLE] BlazorWebView initialized");
                _isBlazorReady = true;
                automationState?.MarkAssistViewReady();
                TryHideLoadingState();

                // Single layout pass after BlazorWebView ready
                BeginInvoke(() =>
                {
                    if (!IsDisposed && _blazorWebView != null)
                    {
                        if (_loadingOverlay?.Visible == true)
                        {
                            _loadingOverlay.BringToFront();
                        }
                        else
                        {
                            _blazorWebView.BringToFront();
                        }
                        this.PerformLayout();
                        Logger?.LogDebug("[JARVIS-LIFECYCLE] BlazorWebView presented at {W}x{H}", Width, Height);
                    }
                });

                // Send initial prompt if provided
                if (!string.IsNullOrWhiteSpace(InitialPrompt))
                {
                    var promptToSend = InitialPrompt;
                    InitialPrompt = null;
                    Logger?.LogInformation("[JARVIS-INIT] Sending initial prompt: {Length} chars", promptToSend.Length);
                    _ = _chatBridge?.RequestExternalPromptAsync(promptToSend);
                }

                if (!string.IsNullOrWhiteSpace(_pendingOfflineNotice) && _chatBridge != null)
                {
                    var pendingNotice = _pendingOfflineNotice;
                    _pendingOfflineNotice = null;
                    _ = _chatBridge.NotifyMessageReceivedAsync(ChatMessage.CreateAIMessage(pendingNotice));
                }
            };

            _blazorWebView.RootComponents.Add<WileyWidget.WinForms.BlazorComponents.App>("#app");
            Logger?.LogDebug("[JARVIS-INIT] RootComponents.Add<App> registered — WebView2 will begin loading");

            // Mark diagnostics as completed for automation
            automationState?.MarkDiagnosticsCompleted();

            // Apply initial theme
            SyncThemeWithBlazor(_themeService.CurrentTheme, _themeService.IsDark);

            // Wire chat bridge events for logging
            if (_chatBridge != null)
            {
                _chatBridge.ResponseChunkReceived += (s, args) =>
                {
                    Logger?.LogDebug("[JARVIS-STREAM] Received chunk: {Length} chars", args.Chunk.Length);
                };

                _chatBridge.ResponseCompleted += (s, args) =>
                {
                    Logger?.LogDebug("[JARVIS-STREAM] Response completed");
                };

                _chatBridge.OnMessageReceived += (s, msg) =>
                {
                    Logger?.LogDebug("[JARVIS-MESSAGE] Full message received: {Length} chars", msg.Content.Length);
                };
            }

            Logger?.LogInformation("[JARVIS-INIT] BlazorWebView initialization complete");
        }

        private void SyncThemeWithBlazor(string themeName, bool isDark)
        {
            if (_blazorWebView?.IsDisposed ?? true) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new System.Action(() => SyncThemeWithBlazor(themeName, isDark)));
                    return;
                }

                WileyWidget.WinForms.Factories.SyncfusionControlFactory.ApplyThemeToAllControls(this, themeName, Logger);
                if (_panelHeader != null && !_panelHeader.IsDisposed)
                {
                    WileyWidget.WinForms.Factories.SyncfusionControlFactory.ApplyThemeToAllControls(_panelHeader, themeName, Logger);
                }

                if (_contentHost != null && !_contentHost.IsDisposed)
                {
                    WileyWidget.WinForms.Factories.SyncfusionControlFactory.ApplyThemeToAllControls(_contentHost, themeName, Logger);
                }

                if (_loadingOverlay != null && !_loadingOverlay.IsDisposed)
                {
                    WileyWidget.WinForms.Factories.SyncfusionControlFactory.ApplyThemeToAllControls(_loadingOverlay, themeName, Logger);
                }

                _contentHost?.Invalidate(true);
                Invalidate(true);

                Logger?.LogInformation(
                    "[JARVIS-THEME] Synced theme {Theme} (IsDark: {IsDark}) to JARVIS host; Bounds={Bounds}; HostBackColor={BackColor}",
                    themeName,
                    isDark,
                    Bounds,
                    _contentHost?.BackColor);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-THEME] Failed to sync theme to BlazorWebView");
            }
        }

        private async Task WarmupAiAsync(IAIService aiService, CancellationToken cancellationToken)
        {
            try
            {
                if (aiService is GrokAgentService grokService && !grokService.HasApiKey)
                {
                    await PublishAiOfflineNoticeAsync("⚠️ Grok is currently offline (API key not configured). AI functions are unavailable. Type /xai-setup in chat for guided key setup.").ConfigureAwait(false);
                    return;
                }

                if (aiService is IAsyncInitializable asyncInitializable)
                {
                    using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    warmupCts.CancelAfter(TimeSpan.FromSeconds(20));

                    Logger?.LogInformation("[JARVIS-LIFECYCLE] Starting background AI warmup");
                    await asyncInitializable.InitializeAsync(warmupCts.Token).ConfigureAwait(false);

                    if (aiService is GrokAgentService warmedGrokService && !warmedGrokService.IsInitialized)
                    {
                        await PublishAiOfflineNoticeAsync("⚠️ Grok is currently offline. AI functions are unavailable right now. Type /xai-setup in chat for guided key setup.").ConfigureAwait(false);
                        return;
                    }

                    Logger?.LogInformation("[JARVIS-LIFECYCLE] Background AI warmup completed");
                }
            }
            catch (OperationCanceledException)
            {
                await PublishAiOfflineNoticeAsync("⚠️ Grok is currently offline or timed out. AI functions are unavailable right now. Type /xai-setup in chat for guided key setup.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-LIFECYCLE] Background AI warmup failed; continuing with offline mode");
                await PublishAiOfflineNoticeAsync("⚠️ Grok is currently offline. AI functions are unavailable right now. Type /xai-setup in chat for guided key setup.").ConfigureAwait(false);
            }
            finally
            {
                MarkAiWarmupCompleted();
            }
        }

        private async Task PublishAiOfflineNoticeAsync(string message)
        {
            if (Interlocked.Exchange(ref _offlineNoticePublished, 1) == 1)
            {
                return;
            }

            try
            {
                if (AutomationStatusBox != null && !AutomationStatusBox.IsDisposed)
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke((MethodInvoker)(() => AutomationStatusBox.Text = message));
                    }
                    else
                    {
                        AutomationStatusBox.Text = message;
                    }
                }

                if (_chatBridge == null || !_isBlazorReady)
                {
                    _pendingOfflineNotice = message;
                    return;
                }

                await _chatBridge.NotifyMessageReceivedAsync(ChatMessage.CreateAIMessage(message)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[JARVIS-LIFECYCLE] Failed to publish AI offline notice");
            }
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowError(message)));
                return;
            }

            if (_contentHost == null || _contentHost.IsDisposed)
            {
                InitializeHostChrome();
            }

            HideLoadingState();
            _contentHost!.Controls.Clear();
            var errorLabel = ControlFactory.CreateLabel($"Error: {message}", label =>
            {
                label.ForeColor = Color.Red;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleCenter;
            });

            _contentHost.Controls.Add(errorLabel);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "JARVISChatUserControl";
            this.Size = LayoutTokens.GetScaled(new Size(DefaultPanelWidth, DefaultPanelHeight));
            this.MinimumSize = LayoutTokens.GetScaled(new Size(MinimumPanelWidth, MinimumPanelHeight));
            this.AutoScroll = false;
            try
            {
                var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                this.ApplySyncfusionTheme(theme, Logger);
            }
            catch { /* best-effort */ }
            this.ResumeLayout(false);
        }

        private JarvisAutomationState? GetAutomationStateService()
        {
            return ServiceProvider != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisAutomationState>(ServiceProvider)
                : Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisAutomationState>(_serviceProvider);
        }

        /// <summary>
        /// Lazy init: first time the JARVIS tab becomes visible, trigger BlazorWebView setup.
        /// This saves ~400 ms at startup because the Activity Log tab is selected by default.
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !_isInitialized && !IsDisposed && IsHandleCreated)
            {
                TriggerLazyInit();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnsureAutomationStatusBoxPresent();
            // Lazy init path: if the tab was already made visible before the handle was ready.
            if (Visible && !_isInitialized && !IsDisposed)
            {
                TriggerLazyInit();
            }
        }

        /// <summary>
        /// Forces the BlazorWebView to repaint and re-layout whenever the host panel is resized.
        /// Without this, the WebView2 chromium surface can lag or show a black region after a
        /// size change (e.g. after the user moves the dock splitter).
        /// </summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (_blazorWebView == null || _blazorWebView.IsDisposed || !IsHandleCreated) return;

            _blazorWebView.Invalidate(invalidateChildren: true);
            _blazorWebView.Update();
        }

        private void TriggerLazyInit()
        {
            Logger?.LogInformation("[JARVIS-LIFECYCLE] Lazy init triggered on first visibility — scheduling BlazorWebView setup");
            ShowLoadingState("Getting chat ready...");

            if (Interlocked.CompareExchange(ref _initializationTriggered, 1, 0) != 0)
            {
                return;
            }

            QueueDeferredInitialization();
        }

        private void UnsubscribeAutomationState()
        {
            if (_automationState != null && _automationStateChangedHandler != null)
            {
                _automationState.Changed -= _automationStateChangedHandler;
            }

            _automationStateChangedHandler = null;
            _automationState = null;
        }

        private void DisposeBlazorWebViewSafely()
        {
            var blazorWebView = Interlocked.Exchange(ref _blazorWebView, null);
            if (blazorWebView == null)
            {
                return;
            }

            _isBlazorReady = false;

            if (IsHandleCreated && InvokeRequired)
            {
                try
                {
                    Invoke(new MethodInvoker(() => DisposeBlazorWebViewCore(blazorWebView)));
                    return;
                }
                catch (Exception ex) when (IsExpectedBlazorTeardownException(ex))
                {
                    Logger?.LogDebug(ex, "[JARVIS-LIFECYCLE] Suppressed expected BlazorWebView teardown exception during marshaling");
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Control handle is already going away; fall back to direct disposal.
                }
            }

            DisposeBlazorWebViewCore(blazorWebView);
        }

        private void DisposeBlazorWebViewCore(BlazorWebView blazorWebView)
        {
            try
            {
                blazorWebView.RootComponents.Clear();
            }
            catch (Exception ex) when (IsExpectedBlazorTeardownException(ex))
            {
                Logger?.LogDebug(ex, "[JARVIS-LIFECYCLE] Suppressed expected Blazor root teardown exception");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-LIFECYCLE] Failed to clear Blazor root components during teardown");
            }

            try
            {
                if (_contentHost != null && !_contentHost.IsDisposed && _contentHost.Controls.Contains(blazorWebView))
                {
                    _contentHost.Controls.Remove(blazorWebView);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[JARVIS-LIFECYCLE] Failed to detach BlazorWebView from host during teardown");
            }

            try
            {
                blazorWebView.Dispose();
            }
            catch (Exception ex) when (IsExpectedBlazorTeardownException(ex))
            {
                Logger?.LogDebug(ex, "[JARVIS-LIFECYCLE] Suppressed expected BlazorWebView disposal exception");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-LIFECYCLE] Failed to dispose BlazorWebView cleanly");
            }
        }

        private static bool IsExpectedBlazorTeardownException(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is COMException comException && (uint)comException.HResult == 0x80004004)
                {
                    return true;
                }

                var typeName = current.GetType().FullName ?? string.Empty;
                if (string.Equals(typeName, "Microsoft.JSInterop.JSDisconnectedException", StringComparison.Ordinal))
                {
                    return true;
                }

                var message = current.Message ?? string.Empty;
                var stackTrace = current.StackTrace ?? string.Empty;
                if (stackTrace.Contains("Syncfusion.Blazor", StringComparison.OrdinalIgnoreCase)
                    && (message.Contains("destroy", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("dispose", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("interop", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if ((stackTrace.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
                        || stackTrace.Contains("BlazorWebView", StringComparison.OrdinalIgnoreCase))
                    && (message.Contains("disposed", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("abort", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("controller", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _deferredInitializationTimer?.Stop();
                _deferredInitializationTimer?.Dispose();
                UnsubscribeAutomationState();
                if (_themeService != null && _themeChangedHandler != null)
                {
                    _themeService.ThemeChanged -= _themeChangedHandler;
                    _themeChangedHandler = null;
                }

                DisposeBlazorWebViewSafely();
                _loadingOverlay?.Dispose();
                _initLock?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void EnsureLoadingOverlayPresent()
        {
            if (_contentHost == null || _contentHost.IsDisposed)
            {
                return;
            }

            if (_loadingOverlay == null || _loadingOverlay.IsDisposed)
            {
                _loadingOverlay = ControlFactory.CreateLoadingOverlay(overlay =>
                {
                    overlay.Name = "JarvisLoadingOverlay";
                    overlay.Message = "Getting chat ready...";
                });
            }

            if (!_contentHost.Controls.Contains(_loadingOverlay))
            {
                _contentHost.Controls.Add(_loadingOverlay);
            }
        }

        private void ShowLoadingState(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowLoadingState(message)));
                return;
            }

            if (_contentHost == null || _contentHost.IsDisposed)
            {
                InitializeHostChrome();
            }

            EnsureLoadingOverlayPresent();
            if (_loadingOverlay == null || _loadingOverlay.IsDisposed)
            {
                return;
            }

            _loadingOverlay.Message = message;
            _loadingOverlay.Visible = true;
            _loadingOverlay.BringToFront();
        }

        private void HideLoadingState()
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(HideLoadingState));
                return;
            }

            if (_loadingOverlay == null || _loadingOverlay.IsDisposed)
            {
                return;
            }

            _loadingOverlay.Visible = false;
        }

        private void MarkAiWarmupCompleted()
        {
            _isAiWarmupCompleted = true;
            TryHideLoadingState();
        }

        private void TryHideLoadingState()
        {
            if (!_isBlazorReady || !_isAiWarmupCompleted)
            {
                return;
            }

            HideLoadingState();
        }

        private void QueueDeferredInitialization()
        {
            if (IsDisposed || Disposing || _isInitialized)
            {
                return;
            }

            if (_deferredInitializationTimer != null)
            {
                return;
            }

            var delay = DateTime.UtcNow - _createdUtc <= StartupInitializationDeferWindow
                ? StartupInitializationDelay
                : StandardInitializationDelay;

            _deferredInitializationTimer = new System.Windows.Forms.Timer
            {
                Interval = (int)Math.Max(1, delay.TotalMilliseconds)
            };

            _deferredInitializationTimer.Tick += (_, _) =>
            {
                _deferredInitializationTimer?.Stop();
                _deferredInitializationTimer?.Dispose();
                _deferredInitializationTimer = null;

                _ = InitializeAsync(CancellationToken.None).ContinueWith(
                    t => Logger?.LogError(
                        t.Exception?.InnerException ?? t.Exception,
                        "[JARVIS-LIFECYCLE] Lazy InitializeAsync failed"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.FromCurrentSynchronizationContext());
            };

            Logger?.LogDebug("[JARVIS-LIFECYCLE] Deferred first chat bootstrap by {DelayMs}ms", delay.TotalMilliseconds);
            _deferredInitializationTimer.Start();
        }
    }

    public class JARVISChatViewModel { }
}
