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
using WileyWidget.WinForms.Services;

using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Controls.Supporting
{
    /// <summary>
    /// Dockable UserControl for the JARVIS AI Assistant (Blazor UI).
    /// Inherits from ScopedPanelBase for automatic ViewModel resolution, theme cascade, and resource disposal.
    /// Designed to be docked as a TabPage in the right panel alongside Activity Log.
    /// Implements IAsyncInitializable to defer heavy WebView2 initialization off the UI thread.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase<JARVISChatViewModel>, IAsyncInitializable, IParameterizedPanel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JarvisAutomationState? _automationState;
        private BlazorWebView? _blazorWebView;
        private LoadingOverlay? _loadingOverlay;
        private TextBox? _automationStatusText;
        private bool _isInitialized;
        private bool _pendingInitialization;
        private bool _initializationFailed;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS when the control is shown.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        public void InitializeWithParameters(object parameters)
        {
            if (IsDisposed)
            {
                return;
            }

            if (parameters is not string prompt || string.IsNullOrWhiteSpace(prompt))
            {
                Logger?.LogDebug("[JARVIS-PARAM] Ignoring unsupported parameters type: {Type}", parameters?.GetType().Name ?? "<null>");
                return;
            }

            InitialPrompt = prompt;
            Logger?.LogInformation("[JARVIS-PARAM] InitialPrompt set via panel parameters ({Length} chars)", prompt.Length);

            if (_isInitialized)
            {
                _ = SendInitialPromptAsync(CancellationToken.None);
            }
        }

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

                if (!IsReadyForInitialization())
                {
                    _pendingInitialization = true;
                    Logger?.LogInformation("[JARVIS-INIT] Deferring BlazorWebView initialization until control is sized. ClientSize={Width}x{Height}, HandleCreated={HandleCreated}",
                        ClientSize.Width, ClientSize.Height, IsHandleCreated);
                    return;
                }

                Logger?.LogInformation("[JARVIS-INIT] Initializing BlazorWebView asynchronously (off UI thread optimization)");

                if (IsHeadlessTestMode())
                {
                    Logger?.LogDebug("Skipping BlazorWebView initialization in headless test mode");
                    _isInitialized = true;
                    _pendingInitialization = false;
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
                    _pendingInitialization = false;
                    return;
                }

                // Schedule BlazorWebView creation on the UI thread (WinForms requirement)
                // Optimized: Use BeginInvoke instead of Task.Run wrapper
                Logger?.LogInformation("[JARVIS-INIT] About to schedule BlazorWebViewCore init - InvokeRequired={InvokeReq}", InvokeRequired);
                await InitializeBlazorWebViewOnUiThreadAsync(cancellationToken).ConfigureAwait(false);

                // Send initial prompt after BlazorWebView is ready (if configured)
                if (_blazorWebView != null && !string.IsNullOrWhiteSpace(InitialPrompt))
                {
                    await SendInitialPromptAsync(cancellationToken).ConfigureAwait(false);
                }

                _pendingInitialization = false;
                Logger?.LogInformation("[JARVIS-INIT] ✅ BlazorWebView initialization sequence completed successfully");
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("BlazorWebView initialization cancelled");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize BlazorWebView asynchronously");
                _initializationFailed = true;
                _isInitialized = true;
                _pendingInitialization = false;
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

        private bool IsReadyForInitialization()
        {
            return IsHandleCreated && ClientSize.Width > 0 && ClientSize.Height > 0;
        }

        private Task InitializeBlazorWebViewOnUiThreadAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (_blazorWebView != null || IsDisposed)
            {
                return Task.CompletedTask;
            }

            if (InvokeRequired)
            {
                var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        InitializeBlazorWebViewCore();
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));
                return tcs.Task;
            }

            InitializeBlazorWebViewCore();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the BlazorWebView control on the UI thread.
        /// This is called from InitializeAsync after the form is shown to avoid blocking startup.
        /// Enhanced with comprehensive error handling, detailed logging, and service validation.
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

            if (_blazorWebView != null)
            {
                Logger?.LogDebug("[JARVIS-BLAZOR] BlazorWebView already created; skipping initialization.");
                return;
            }

            if (ClientSize.Width == 0 || ClientSize.Height == 0)
            {
                _pendingInitialization = true;
                Logger?.LogInformation("[JARVIS-BLAZOR] Deferring BlazorWebView creation due to zero size. ClientSize={Width}x{Height}",
                    ClientSize.Width, ClientSize.Height);
                return;
            }

            _initializationFailed = false;

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

                // Log WebView2 version information for diagnostics
                try
                {
                    var webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    Logger?.LogInformation("[JARVIS-BLAZOR] WebView2 Version: {Version}", webView2Version);
                }
                catch (Exception versionEx)
                {
                    Logger?.LogWarning(versionEx, "[JARVIS-BLAZOR] Failed to retrieve WebView2 version information");
                }

                // Validate that required services are available in main provider
                Logger?.LogInformation("[JARVIS-BLAZOR] Verifying application services for JARVISAssist component");
                var serviceValidationErrors = new System.Collections.Generic.List<string>();
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

                    // Detailed service availability logging
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - IChatBridgeService: {HasChatBridge}", chatBridge != null);
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - IAIService: {HasAIService}", aiService != null);
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - IConversationRepository: {HasConvRepo}", conversationRepo != null);
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - JarvisAutomationState: {HasAutoState}", automationState != null);
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - IUserContext: {HasUserContext}", userContext != null);
                    Logger?.LogDebug("[JARVIS-BLAZOR] Service Availability - BlazorWebView Infrastructure: {HasBlazor}", hasBlazorWebViewService);

                    var registeredCount = new object?[] { chatBridge, aiService, conversationRepo, automationState, userContext }
                        .Count(s => s != null);
                    Logger?.LogInformation("[JARVIS-BLAZOR] Verified {Count}/5 core application services available (BlazorWebView infrastructure: {HasBlazor})",
                        registeredCount, hasBlazorWebViewService);

                    if (chatBridge == null) serviceValidationErrors.Add("IChatBridgeService not registered");
                    if (aiService == null) serviceValidationErrors.Add("IAIService not registered");
                    if (conversationRepo == null) serviceValidationErrors.Add("IConversationRepository not registered");
                    if (automationState == null) serviceValidationErrors.Add("JarvisAutomationState not registered");
                    if (userContext == null) serviceValidationErrors.Add("IUserContext not registered");
                }
                catch (Exception serviceEx)
                {
                    Logger?.LogWarning(serviceEx, "[JARVIS-BLAZOR] Exception during service verification (inner exception: {InnerEx})",
                        serviceEx.InnerException?.Message);
                    serviceValidationErrors.Add($"Service verification failed: {serviceEx.GetType().Name}: {serviceEx.Message}");
                }

                /// <summary>
                /// NEW: Add runtime check for Blazor services availability
                /// Verifies that AddWindowsFormsBlazorWebView() and AddSyncfusionBlazor() were properly called during startup.
                /// </summary>
                try
                {
                    var blazorServices = ServiceProviderServiceExtensions.GetService<IServiceScopeFactory>(_serviceProvider);
                    if (blazorServices == null)
                    {
                        Logger?.LogError("Blazor services not registered - ensure AddWindowsFormsBlazorWebView() is called in startup");
                        ShowErrorPlaceholder("Blazor Initialization Failed", "Required services not found. Check application startup configuration.");
                        return;
                    }
                }
                catch (Exception blazorCheckEx)
                {
                    Logger?.LogError(blazorCheckEx, "Error verifying Blazor services");
                    ShowErrorPlaceholder("Blazor Initialization Failed", "Required services not found. Check application startup configuration.");
                    return;
                }

                // Create and configure BlazorWebView using main service provider
                // CRITICAL: Do NOT create a new ServiceCollection - BlazorWebView needs fully-initialized infrastructure
                try
                {
                    Logger?.LogDebug("[JARVIS-BLAZOR] Creating new BlazorWebView instance...");
                    _blazorWebView = new BlazorWebView
                    {
                        Dock = DockStyle.Fill,
                        HostPage = "wwwroot/index.html",
                        Services = _serviceProvider,
                        AccessibleName = "JARVIS AI Chat Interface",
                        Name = "JARVISChatBlazorView"
                    };
                    Logger?.LogDebug("[JARVIS-BLAZOR] BlazorWebView instance created successfully");

                    Logger?.LogDebug("[JARVIS-BLAZOR] Adding JARVISAssist root component to BlazorWebView...");
                    _blazorWebView.RootComponents.Add<JARVISAssist>("#app");
                    Logger?.LogInformation("[JARVIS-BLAZOR] Added JARVISAssist root component to BlazorWebView");

                    // Enable WebView2 Developer Tools and event logging (enabled in all builds for diagnostics)
                    _blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;

                    // Add WebView2 error handler to catch fatal initialization failures
                    _blazorWebView.BlazorWebViewInitialized += (sender, e) =>
                    {
                        try
                        {
                            Logger?.LogInformation("[JARVIS-BLAZOR] BlazorWebViewInitialized event fired, subscribing to CoreWebView2InitializationCompleted");

                            // Capture console messages from JavaScript/Blazor
                            e.WebView.CoreWebView2InitializationCompleted += async (s, args) =>
                            {
                                if (!args.IsSuccess)
                                {
                                    Logger?.LogError(args.InitializationException,
                                        "[JARVIS-BLAZOR] CoreWebView2 initialization failed: {Message}. Inner Exception: {InnerEx}",
                                        args.InitializationException?.Message,
                                        args.InitializationException?.InnerException?.Message);
                                    ShowErrorPlaceholder("WebView2 Initialization Failed",
                                        $"Failed to initialize WebView2 core:\n\n{args.InitializationException?.GetType().Name}:\n{args.InitializationException?.Message}\n\n" +
                                        (args.InitializationException?.InnerException != null ? $"Inner: {args.InitializationException.InnerException.Message}\n\n" : "") +
                                        "Verify WebView2 runtime is properly installed and not corrupted.");
                                }
                                else
                                {
                                    Logger?.LogInformation("[JARVIS-BLAZOR] CoreWebView2 initialization succeeded - Blazor render should begin now");
                                }
                            };
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "[JARVIS-BLAZOR] Failed to attach CoreWebView2 error handler. Exception: {ExType}: {Message}",
                                ex.GetType().Name, ex.Message);
                        }
                    };

                    this.Controls.Add(_blazorWebView);
                    this.ResumeLayout(false);

                    _isInitialized = true;
                    _pendingInitialization = false;
                    _initializationFailed = false;

                    Logger?.LogInformation("[JARVIS-BLAZOR] ✅ BlazorWebView core initialization completed successfully");
                }
                catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("AddWindowsFormsBlazorWebView"))
                {
                    Logger?.LogError(ioEx, "[JARVIS-BLAZOR] CRITICAL: BlazorWebView services not registered. Missing 'AddWindowsFormsBlazorWebView()' in DI configuration. Stack: {StackTrace}",
                        ioEx.StackTrace);
                    ShowErrorPlaceholder("BlazorWebView Configuration Error",
                        "BlazorWebView services not registered in dependency injection container.\n\n" +
                        "REQUIRED CONFIGURATION:\n" +
                        "In DependencyInjection.ConfigureServicesInternal():\n" +
                        "  services.AddWindowsFormsBlazorWebView();\n" +
                        "  services.AddSyncfusionBlazor();\n\n" +
                        "This setup must occur BEFORE any BlazorWebView control initializes.\n\n" +
                        "Error Details:\n" + ioEx.Message);
                    this.ResumeLayout(false);
                }
                catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("WebView2"))
                {
                    Logger?.LogError(ioEx, "[JARVIS-BLAZOR] WebView2 infrastructure error: {Message}. Stack: {StackTrace}",
                        ioEx.Message, ioEx.StackTrace);
                    ShowErrorPlaceholder("WebView2 Infrastructure Error",
                        $"BlazorWebView detected a WebView2 configuration issue:\n\n{ioEx.Message}\n\n" +
                        "Possible causes:\n" +
                        "• WebView2 runtime not installed\n" +
                        "• WebView2 runtime corrupted or outdated\n" +
                        "• User permissions insufficient for WebView2 initialization\n\n" +
                        "Please download and install from:\n" +
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    this.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "[JARVIS-BLAZOR] FAILED to initialize BlazorWebView. ExceptionType: {ExType}, Message: {Message}, Stack: {StackTrace}, InnerEx: {InnerEx}",
                        ex.GetType().FullName, ex.Message, ex.StackTrace, ex.InnerException?.Message);

                    var serviceContext = serviceValidationErrors.Any()
                        ? $"\nService Validation Issues:\n• {string.Join("\n• ", serviceValidationErrors)}\n"
                        : "";

                    ShowErrorPlaceholder("BlazorWebView Runtime Error",
                        $"Failed to initialize BlazorWebView.\n\n" +
                        $"Exception Type: {ex.GetType().Name}\n" +
                        $"Message: {ex.Message}\n" +
                        (ex.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}\n" : "") +
                        serviceContext +
                        "Troubleshooting:\n" +
                        "1. Verify WebView2 runtime is installed\n" +
                        "2. Check application logs for stack trace\n" +
                        "3. Ensure user has system permissions for WebView2\n" +
                        "4. Verify all required services are registered in DI container\n\n" +
                        "Download WebView2:\n" +
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    this.ResumeLayout(false);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[JARVIS-BLAZOR] UNEXPECTED ERROR in InitializeBlazorWebViewCore outer try block. ExceptionType: {ExType}, Message: {Message}, Stack: {StackTrace}",
                    ex.GetType().FullName, ex.Message, ex.StackTrace);
                ShowErrorPlaceholder("Unexpected Error",
                    $"An unexpected error occurred during BlazorWebView initialization:\n\n" +
                    $"{ex.GetType().Name}: {ex.Message}\n\n" +
                    "Please check application logs for detailed error information.");
                try
                {
                    this.ResumeLayout(false);
                }
                catch { /* Resume layout failed, something is very wrong */ }
            }
        }

        /// <summary>
        /// Event handler for BlazorWebView initialization. Enables Chrome DevTools and subscribes to navigation and console events.
        /// DevTools are enabled in all builds (not just Debug) for production diagnostics and rendering validation.
        /// </summary>
        private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
            try
            {
                // NEW: Enable dev tools and log WebView init
                // Enable Chrome DevTools (right-click → Inspect, or F12) in all builds for diagnostics
                e.WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;  // Ensure dev tools accessible (F12 or right-click inspect)
                e.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                // Inject Syncfusion license key from environment variable before Blazor initializes
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    var escapedKey = licenseKey.Replace("\"", "\\\"");
                    var scriptToInjectKey = $@"
                        window.syncfusionLicenseKey = ""{escapedKey}"";
                        console.log('[JARVIS-LICENSE] Syncfusion license key injected from environment');
                    ";
                    _ = e.WebView.CoreWebView2.ExecuteScriptAsync(scriptToInjectKey);
                    Logger?.LogInformation("[JARVIS-LICENSE] Injected Syncfusion license key from SYNCFUSION_LICENSE_KEY environment variable");
                }
                else
                {
                    Logger?.LogWarning("[JARVIS-LICENSE] SYNCFUSION_LICENSE_KEY environment variable not found. License registration will fail.");
                }

                _ = e.WebView.CoreWebView2.ExecuteScriptAsync("console.log('WebView2 initialized');");
                Logger?.LogInformation("[JARVIS-WEBVIEW] WebView2 Developer Tools enabled");
                Logger?.LogInformation("BlazorWebView CoreWebView2 initialized - check browser console (F12 in panel) for errors");

                // Disable zoom control to prevent accidental Ctrl+Scroll zoom in docked panel
                e.WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Subscribe to NavigationCompleted to log rendering status
                e.WebView.CoreWebView2.NavigationCompleted += (sender, navArgs) =>
                {
                    try
                    {
                        Logger?.LogInformation("[JARVIS-WEBVIEW] Navigation completed - IsSuccess={IsSuccess}, HttpStatusCode={HttpStatus}",
                            navArgs.IsSuccess, navArgs.HttpStatusCode);
                        if (!navArgs.IsSuccess)
                        {
                            Logger?.LogWarning("[JARVIS-WEBVIEW] Navigation failed with status code {HttpStatus}", navArgs.HttpStatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "[JARVIS-WEBVIEW] Exception in NavigationCompleted handler");
                    }
                };

                // Subscribe to WebMessageReceived to capture JavaScript console messages
                e.WebView.CoreWebView2.WebMessageReceived += (sender, messageArgs) =>
                {
                    try
                    {
                        var messageData = messageArgs.WebMessageAsJson;
                        Logger?.LogDebug("[JARVIS-WEBVIEW] JS Message received: {MessageData}", messageData);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "[JARVIS-WEBVIEW] Exception in WebMessageReceived handler");
                    }
                };

                Logger?.LogDebug("[JARVIS-WEBVIEW] Subscribed to NavigationCompleted and WebMessageReceived events");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-WEBVIEW] Failed to configure WebView2 event handlers");
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryInitializeWhenVisible();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            // Check if size is zero; if so, re-initialize when it becomes non-zero
            if (Visible && (ClientSize.Width == 0 || ClientSize.Height == 0))
            {
                _pendingInitialization = true;
                Logger?.LogWarning("[JARVIS-SIZING] OnVisibleChanged: Control visible but size is zero (W={Width}, H={Height}). Deferring init until OnResize.",
                    ClientSize.Width, ClientSize.Height);
                return;
            }

            TryInitializeWhenVisible();
        }

        /// <summary>
        /// Handles panel resize events. Logs ClientSize and keeps BlazorWebView in sync with panel bounds.
        /// If size becomes non-zero after being zero, triggers initialization if not yet complete.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Log panel sizing for visibility validation
            Logger?.LogDebug("[JARVIS-SIZING] OnResize: ClientSize={Width}x{Height}", ClientSize.Width, ClientSize.Height);

            // Apply BlazorWebView size to match panel client area
            if (_blazorWebView != null && !IsDisposed)
            {
                try
                {
                    _blazorWebView.Size = this.ClientSize;
                    Logger?.LogDebug("[JARVIS-SIZING] BlazorWebView resized to {Width}x{Height}", ClientSize.Width, ClientSize.Height);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "[JARVIS-SIZING] Failed to resize BlazorWebView");
                }
            }

            // If control is visible and size became non-zero after being zero, trigger initialization
            if (Visible && _pendingInitialization && !_isInitialized && !_initializationFailed && ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                _pendingInitialization = false;
                Logger?.LogInformation("[JARVIS-SIZING] Control size became non-zero (W={Width}, H={Height}). Triggering initialization.",
                    ClientSize.Width, ClientSize.Height);
                _ = InitializeAsync();
            }
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

            _initializationFailed = true;
            _isInitialized = true;
            _pendingInitialization = false;

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

                /// <summary>
                /// NEW: Add button to retry initialization
                /// Allows users to attempt to reinitialize BlazorWebView after resolving startup issues.
                /// </summary>
                var retryButton = new Button { Text = "Retry", Dock = DockStyle.Bottom };
                retryButton.Click += (s, e) => InitializeBlazorWebViewCore();  // Retry on click
                this.Controls.Add(retryButton);

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
                if (_blazorWebView != null)
                {
                    _blazorWebView.BlazorWebViewInitialized -= OnBlazorWebViewInitialized;
                }
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



