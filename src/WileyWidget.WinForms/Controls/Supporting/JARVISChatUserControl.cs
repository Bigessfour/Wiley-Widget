using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Syncfusion.Blazor;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.BlazorComponents;
using WileyWidget.WinForms.Automation;

using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Controls.Supporting
{
    /// <summary>
    /// Dockable UserControl for the JARVIS AI Assistant (Blazor UI).
    /// Inherits from ScopedPanelBase for automatic ViewModel resolution, theme cascade, and resource disposal.
    /// Designed to be docked as a TabPage in the right panel alongside Activity Log.
    /// Implements IAsyncInitializable to defer heavy WebView2 initialization off the UI thread.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase<JARVISChatViewModel>, IAsyncInitializable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JarvisAutomationState? _automationState;
        private BlazorWebView? _blazorWebView;
        private LoadingOverlay? _loadingOverlay;
        private TextBox? _automationStatusText;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS when the control is shown.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        /// <summary>
        /// Initializes a new instance with required DI dependencies.
        /// </summary>
        /// <param name="scopeFactory">Service scope factory for ViewModel resolution</param>
        /// <param name="serviceProvider">Service provider for dependency resolution (BlazorWebView services)</param>
        /// <param name="logger">Logger instance for diagnostics</param>
        public JARVISChatUserControl(
            IServiceScopeFactory scopeFactory,
            IServiceProvider serviceProvider,
            ILogger<JARVISChatUserControl> logger)
            : base(scopeFactory, logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _automationState = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<JarvisAutomationState>(serviceProvider);

            Logger?.LogInformation("Initializing JARVISChatUserControl (Docked Chat) — BlazorWebView deferred to async init");

            InitializeComponent();
            ApplyTheme();
            InitializeAutomationStatusPanel();
        }

        /// <summary>
        /// IAsyncInitializable implementation: Defers heavy WebView2 initialization off the UI thread.
        /// Called by StartupOrchestrator after MainForm is shown to avoid blocking UI during startup.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the initialization operation.</param>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Logger?.LogInformation("[JARVIS-INIT] InitializeAsync CALLED - IsInitialized={IsInit}, IsDisposed={IsDisposed}, HasHandle={HasHandle}, Thread={Thread}",
                _isInitialized, IsDisposed, IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);

            if (_isInitialized || IsDisposed)
            {
                Logger?.LogWarning("[JARVIS-INIT] Early return - already initialized or disposed");
                return;
            }

            await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isInitialized || IsDisposed)
                {
                    Logger?.LogWarning("[JARVIS-INIT] Early return after lock - already initialized or disposed");
                    return;
                }

                Logger?.LogInformation("[JARVIS-INIT] Initializing BlazorWebView asynchronously (off UI thread optimization)");

                if (IsHeadlessTestMode())
                {
                    Logger?.LogDebug("Skipping BlazorWebView initialization in headless test mode");
                    _isInitialized = true;
                    return;
                }

                // Check WebView2 runtime availability before initialization
                try
                {
                    string? version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    Logger?.LogInformation("WebView2 runtime detected: {Version}", version);
                }
                catch (WebView2RuntimeNotFoundException ex)
                {
                    Logger?.LogError(ex, "WebView2 runtime not found - JARVIS Chat requires Microsoft Edge WebView2 Runtime");
                    ShowErrorPlaceholder("WebView2 Runtime Required",
                        "Microsoft Edge WebView2 Runtime is not installed.\n\n" +
                        "Please download and install from:\n" +
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    _isInitialized = true; // Mark as initialized to prevent retries
                    return;
                }

                // Schedule BlazorWebView creation on the UI thread (WinForms requirement)
                // Optimized: Use BeginInvoke instead of Task.Run wrapper
                Logger?.LogInformation("[JARVIS-INIT] About to schedule BlazorWebViewCore init - InvokeRequired={InvokeReq}", InvokeRequired);

                if (InvokeRequired)
                {
                    Logger?.LogDebug("[JARVIS-INIT] Using BeginInvoke to marshal to UI thread");
                    BeginInvoke(new Action(() =>
                    {
                        Logger?.LogDebug("[JARVIS-INIT] BeginInvoke action executing on thread {Thread}", System.Threading.Thread.CurrentThread.ManagedThreadId);
                        InitializeBlazorWebViewCore();
                    }));
                }
                else
                {
                    Logger?.LogDebug("[JARVIS-INIT] Calling InitializeBlazorWebViewCore directly (already on UI thread)");
                    InitializeBlazorWebViewCore();
                }

                // Send initial prompt after BlazorWebView is ready (if configured)
                if (!string.IsNullOrWhiteSpace(InitialPrompt))
                {
                    await SendInitialPromptAsync(cancellationToken).ConfigureAwait(false);
                }

                _isInitialized = true;
                Logger?.LogInformation("[JARVIS-INIT] ✅ BlazorWebView initialization sequence completed successfully");
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("BlazorWebView initialization cancelled");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize BlazorWebView asynchronously");
                ShowErrorPlaceholder();
            }
            finally
            {
                _initLock.Release();
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // UserControl settings
            this.Name = "JARVISChatUserControl";
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // Show loading overlay during async initialization
            if (IsHeadlessTestMode())
            {
                var placeholder = new Label
                {
                    Name = "JARVISChatPlaceholder",
                    Text = "JARVIS Chat is disabled in test mode.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                this.Controls.Add(placeholder);
            }
            else
            {
                _loadingOverlay = new LoadingOverlay
                {
                    Message = "Loading JARVIS Chat...",
                    Visible = true,
                    Dock = DockStyle.Fill
                };
                this.Controls.Add(_loadingOverlay);
            }

            this.ResumeLayout(false);

            Logger?.LogDebug("JARVISChatUserControl components initialized (BlazorWebView deferred)");
        }

        private void InitializeAutomationStatusPanel()
        {
            if (_automationState == null)
            {
                return;
            }

            _automationStatusText = new TextBox
            {
                Name = "JarvisAutomationStatus",
                AccessibleName = "JarvisAutomationStatus",
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                TabStop = false,
                Multiline = false,
                Size = new Size(1, 1),
                Location = new Point(-10000, -10000),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Text = string.Empty
            };

            Controls.Add(_automationStatusText);

            _automationState.Changed += AutomationStateChanged;
            UpdateAutomationStatus(_automationState.Snapshot);
        }

        private void AutomationStateChanged(object? sender, JarvisAutomationStateChangedEventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateAutomationStatus(e.Snapshot)));
                return;
            }

            UpdateAutomationStatus(e.Snapshot);
        }

        private void UpdateAutomationStatus(JarvisAutomationSnapshot snapshot)
        {
            if (_automationStatusText == null)
            {
                return;
            }

            var status = snapshot.ToStatusString();
            _automationStatusText.Text = status;
            _automationStatusText.Tag = status;
        }

        /// <summary>
        /// Initializes the BlazorWebView control on the UI thread.
        /// This is called from InitializeAsync after the form is shown to avoid blocking startup.
        /// </summary>
        private void InitializeBlazorWebViewCore()
        {
            Logger?.LogInformation("[JARVIS-BLAZOR] InitializeBlazorWebViewCore STARTING on thread {Thread}",
                System.Threading.Thread.CurrentThread.ManagedThreadId);
            Logger?.LogInformation("[JARVIS-BLAZOR] Panel state: IsDisposed={IsDisposed}, IsHandleCreated={HasHandle}, Name={Name}",
                IsDisposed, IsHandleCreated, Name);

            if (IsDisposed || IsHeadlessTestMode())
            {
                Logger?.LogWarning("[JARVIS-BLAZOR] ⚠️ Skipping BlazorWebView initialization: Disposed={IsDisposed}, HeadlessMode={IsHeadless}",
                    IsDisposed, IsHeadlessTestMode());
                return;
            }

            try
            {
                this.SuspendLayout();

                // Hide and remove loading overlay
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = false;
                    this.Controls.Remove(_loadingOverlay);
                    _loadingOverlay.Dispose();
                    _loadingOverlay = null;
                }

                Logger?.LogDebug("Creating BlazorWebView with HostPage='wwwroot/index.html'");

                // Use the main _serviceProvider directly (already has AddWindowsFormsBlazorWebView  and AddSyncfusionBlazor registered)
                // Creating a new ServiceCollection and trying to register all required services causes BlazorWebView initialization to fail
                // The main provider has all infrastructure services properly initialized
                Logger?.LogDebug("Using main IServiceProvider for BlazorWebView (has AddWindowsFormsBlazorWebView + AddSyncfusionBlazor)");

                // Validate that required services are available in main provider
                Logger?.LogDebug("Verifying application services for JARVISAssist component");
                bool hasBlazorWebViewService = false;
                try
                {
                    var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IChatBridgeService>(_serviceProvider);
                    var aiService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(_serviceProvider);
                    var conversationRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConversationRepository>(_serviceProvider);
                    var automationState = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisAutomationState>(_serviceProvider);
                    var userContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IUserContext>(_serviceProvider);

                    // Critical: Check if AddWindowsFormsBlazorWebView was registered (manifests as BlazorWebViewServiceProvider availability)
                    var blazorProvider = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.AspNetCore.Components.WebView.WindowsForms.BlazorWebView>(_serviceProvider);
                    hasBlazorWebViewService = blazorProvider != null;

                    var registeredCount = new object?[] { chatBridge, aiService, conversationRepo, automationState, userContext }
                        .Count(s => s != null);
                    Logger?.LogDebug("Verified {Count} application services available in main provider (BlazorWebView registered: {HasBlazor})",
                        registeredCount, hasBlazorWebViewService);
                }
                catch (Exception serviceEx)
                {
                    Logger?.LogWarning(serviceEx, "Failed to verify application services availability - proceeding with initialization");
                    // Continue - some services may be optional or still initializing
                }

                // Create and configure BlazorWebView using main service provider
                // CRITICAL: Do NOT create a new ServiceCollection - BlazorWebView needs fully-initialized infrastructure
                _blazorWebView = new BlazorWebView
                {
                    Dock = DockStyle.Fill,
                    HostPage = "wwwroot/index.html",
                    Services = _serviceProvider,
                    AccessibleName = "JARVIS AI Chat Interface",
                    Name = "JARVISChatBlazorView"
                };

                _blazorWebView.RootComponents.Add<JARVISAssist>("#app");
                Logger?.LogDebug("Added JARVISAssist root component to BlazorWebView");

#if DEBUG
                // Enable WebView2 Developer Tools for debugging Blazor console errors
                _blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
#endif

                // Add WebView2 error handler to catch fatal initialization failures
                _blazorWebView.BlazorWebViewInitialized += (sender, e) =>
                {
                    try
                    {
                        Logger?.LogInformation("BlazorWebView initialized, subscribing to CoreWebView2InitializationCompleted");

                        // Capture console messages from JavaScript/Blazor
                        e.WebView.CoreWebView2InitializationCompleted += async (s, args) =>
                        {
                            if (!args.IsSuccess)
                            {
                                Logger?.LogError(args.InitializationException,
                                    "CoreWebView2 initialization failed: {Message}",
                                    args.InitializationException?.Message);
                                ShowErrorPlaceholder("WebView2 Initialization Failed",
                                    $"Failed to initialize WebView2:\n{args.InitializationException?.Message}");
                            }
                            else
                            {
                                Logger?.LogInformation("CoreWebView2 initialization succeeded - Blazor render should begin now");
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Failed to attach CoreWebView2 error handler");
                    }
                };

                this.Controls.Add(_blazorWebView);
                this.ResumeLayout(false);

                Logger?.LogInformation("BlazorWebView core initialization completed successfully");
            }
            catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("AddWindowsFormsBlazorWebView"))
            {
                Logger?.LogError(ioEx, "CRITICAL: BlazorWebView services not registered. Missing 'AddWindowsFormsBlazorWebView()' in DI configuration.");
                ShowErrorPlaceholder("BlazorWebView Configuration Error",
                    "BlazorWebView services not registered in dependency injection container.\n\n" +
                    "Ensure DependencyInjection.ConfigureServicesInternal() calls:\n" +
                    "services.AddWindowsFormsBlazorWebView()\n\n" +
                    "This is required before any BlazorWebView control initializes.");
                this.ResumeLayout(false);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize BlazorWebView. WebView2 runtime may be missing or corrupted. Details: {Message}", ex.Message);
                ShowErrorPlaceholder("BlazorWebView Runtime Error",
                    $"Failed to initialize BlazorWebView:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                    "Verify WebView2 runtime is installed and application has system permissions.\n\n" +
                    "Check logs for detailed error trace.");
                this.ResumeLayout(false);
            }
        }

#if DEBUG
        /// <summary>
        /// Event handler for BlazorWebView initialization. Enables Chrome DevTools in Debug builds.
        /// </summary>
        private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
            try
            {
                // Enable Chrome DevTools (right-click → Inspect, or F12)
                e.WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Disable zoom control to prevent accidental Ctrl+Scroll zoom in docked panel
                e.WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                Logger?.LogDebug("WebView2 Developer Tools enabled for JARVIS panel");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to enable WebView2 Developer Tools");
            }
        }
#endif

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryInitializeWhenVisible();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            TryInitializeWhenVisible();
        }

        private void TryInitializeWhenVisible()
        {
            if (IsDisposed || _isInitialized)
            {
                return;
            }

            if (!Visible && !IsAutomationInitializationEnabled())
            {
                return;
            }

            _ = InitializeAsync();
        }

        private static bool IsAutomationInitializationEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS"), "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shows an error placeholder when BlazorWebView initialization fails.
        /// </summary>
        private void ShowErrorPlaceholder(string? title = null, string? message = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowErrorPlaceholder(title, message)));
                return;
            }

            try
            {
                this.SuspendLayout();

                // Remove any existing controls
                this.Controls.Clear();

                var errorLabel = new Label
                {
                    Text = message ?? "Error: WebView2 Runtime could not be initialized.\nChat features are disabled.\n\nPlease install the WebView2 Runtime.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 9.0f, FontStyle.Regular)
                };
                this.Controls.Add(errorLabel);

                // Add clickable link for WebView2 download if this is a runtime error
                if (title == "WebView2 Runtime Required")
                {
                    var linkLabel = new LinkLabel
                    {
                        Text = "Download WebView2 Runtime",
                        Dock = DockStyle.Bottom,
                        TextAlign = ContentAlignment.MiddleCenter,
                        LinkColor = Color.Blue,
                        Height = 30
                    };
                    linkLabel.LinkClicked += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning(ex, "Failed to open WebView2 download link");
                        }
                    };
                    this.Controls.Add(linkLabel);
                }

                this.ResumeLayout(false);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to show error placeholder");
            }
        }

        /// <summary>
        /// Sends the initial prompt to JARVIS after BlazorWebView is fully initialized.
        /// Waits for the chat bridge service to be ready before sending.
        /// </summary>
        private async Task SendInitialPromptAsync(CancellationToken cancellationToken)
        {
            var prompt = InitialPrompt?.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            try
            {
                Logger?.LogInformation("Scheduling initial prompt to JARVIS: {PromptLength} chars", prompt.Length);

                // Give BlazorWebView a brief moment to fully initialize the Blazor runtime
                // This is a minimal delay; the actual readiness is confirmed by IChatBridgeService availability
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                if (IsDisposed || Disposing)
                {
                    Logger?.LogDebug("JARVISChatUserControl disposed before initial prompt could be sent");
                    return;
                }

                var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<IChatBridgeService>(_serviceProvider);
                await chatBridge.RequestExternalPromptAsync(prompt).ConfigureAwait(false);

                Logger?.LogInformation("Initial prompt sent to JARVIS successfully");
            }
            catch (ObjectDisposedException)
            {
                Logger?.LogDebug("JARVISChatUserControl disposed before initial prompt could be sent");
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("Initial prompt cancelled");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to send initial prompt to JARVIS");
            }
        }

        private static bool IsHeadlessTestMode()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                   || !Environment.UserInteractive;
        }

        /// <summary>
        /// Applies theme to the control via SfSkinManager (Validation #4: Theme Cascade Enforcement).
        /// ScopedPanelBase.ApplyThemeCascade() is called during OnHandleCreated,
        /// ensuring SfSkinManager theme is cascaded to BlazorWebView and all children.
        /// CRITICAL: No manual BackColor/ForeColor assignments per SfSkinManager theme authority rule.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                var currentTheme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(currentTheme))
                {
                    SfSkinManager.SetVisualStyle(this, currentTheme);
                    Logger?.LogDebug("Theme '{Theme}' applied to JARVISChatUserControl via SfSkinManager (NO manual colors)", currentTheme);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to apply SfSkinManager theme to JARVISChatUserControl");
            }
        }

        /// <summary>
        /// Cleans up resources when the control is disposed.
        /// ScopedPanelBase handles disposal of scoped ViewModel and services.
        /// BlazorWebView is disposed safely; tab is removed from TabControl by factory.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Logger?.LogDebug("JARVISChatUserControl disposing resources — BlazorWebView cleanup");
                if (_automationState != null)
                {
                    _automationState.Changed -= AutomationStateChanged;
                }
#if DEBUG
                if (_blazorWebView != null)
                {
                    _blazorWebView.BlazorWebViewInitialized -= OnBlazorWebViewInitialized;
                }
#endif
                _blazorWebView?.Dispose();
                _loadingOverlay?.Dispose();
                _automationStatusText?.Dispose();
                _initLock?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Placeholder ViewModel for JARVIS Chat UserControl.
    /// Extends ScopedPanelBase pattern to provide automatic ViewModel resolution.
    /// Can be expanded with properties and methods for chat state management.
    /// </summary>
    public class JARVISChatViewModel
    {
        // Placeholder for future expansion
        // - Message history
        // - Chat state
        // - Configuration options
    }
}



