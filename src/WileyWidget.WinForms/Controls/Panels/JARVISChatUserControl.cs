using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Hosted JARVIS chat control using Blazor WebView for rich component integration.
    /// This control is managed by ScopedPanelBase and initialized via IAsyncInitializable.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase<CommunityToolkit.Mvvm.ComponentModel.ObservableObject>, IAsyncInitializable, IParameterizedPanel
    {
        private BlazorWebView? _blazorWebView;
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private IChatBridgeService? _chatBridge;
        private IThemeService? _themeService;
        private EventHandler<string>? _themeChangedHandler;

        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        public JARVISChatUserControl(
            IServiceScopeFactory scopeFactory,
            IServiceProvider serviceProvider,
            ILogger<JARVISChatUserControl> logger)
            : base(scopeFactory, logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            this.Dock = DockStyle.Fill;
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
            if (_isInitialized || IsDisposed) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_isInitialized || IsDisposed) return;

                Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing BlazorWebView panel host...");
                await InitializeBlazorWebViewAsync(ct);

                // Ensure AI Service is initialized in this scope
                var aiService = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(ServiceProvider) : null)
                                ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(_serviceProvider);

                if (aiService is IAsyncInitializable asyncInitAI)
                {
                    Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing Grok AI service...");
                    await asyncInitAI.InitializeAsync(ct);
                }

                // Activate the bridge handler by resolving it from the scoped provider
                _ = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(ServiceProvider) : null)
                    ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(_serviceProvider);

                // Mark diagnostics as completed for automation
                var automationState = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Automation.JarvisAutomationState>(ServiceProvider) : null)
                    ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Automation.JarvisAutomationState>(_serviceProvider);
                automationState?.MarkDiagnosticsCompleted();

                // Add automation status TextBox if JarvisAutomationState is available (for UI automation testing)
                if (automationState != null)
                {
                    Logger?.LogInformation("[JARVIS-AUTOMATION] Adding automation status TextBox for UI testing");
                    var statusTextBox = new System.Windows.Forms.TextBox
                    {
                        Name = "JarvisAutomationStatus",
                        AccessibleName = "JarvisAutomationStatus",
                        ReadOnly = true,
                        BorderStyle = System.Windows.Forms.BorderStyle.None,
                        // BackColor/ForeColor/Font inherited from parent via SfSkinManager cascade
                        Text = automationState.Snapshot.ToStatusString(),
                        Dock = System.Windows.Forms.DockStyle.Bottom,
                        Height = 20,
                        Visible = true
                    };

                    // Subscribe to automation state changes
                    automationState.Changed += (s, e) =>
                    {
                        if (InvokeRequired)
                        {
                            BeginInvoke(() => statusTextBox.Text = e.Snapshot.ToStatusString());
                        }
                        else
                        {
                            statusTextBox.Text = e.Snapshot.ToStatusString();
                        }
                    };

                    this.Controls.Add(statusTextBox);
                    Logger?.LogDebug("[JARVIS-AUTOMATION] Automation status TextBox added");
                }

                _isInitialized = true;

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            ForceFullLayout();
                            PerformLayout();
                            Invalidate(true);
                            Update();
                        }
                    }));
                }

                Logger?.LogInformation("[JARVIS-LIFECYCLE] JARVIS BlazorWebView panel initialization successful");
            }
            catch (Exception ex)
            {
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
                BeginInvoke(new Action(async () =>
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

            // Create BlazorWebView
            _blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html"
            };

            this.Controls.Clear();
            this.Controls.Add(_blazorWebView);
            _blazorWebView.BringToFront();
            PerformLayout();
            Invalidate(true);
            Update();

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
                automationState?.MarkAssistViewReady();

                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        _blazorWebView?.BringToFront();
                        ForceFullLayout();
                        PerformLayout();
                        Invalidate(true);
                        Update();
                        Logger?.LogDebug("[JARVIS-LIFECYCLE] BlazorWebView presented at {W}x{H}", Width, Height);
                    }
                }));

                // Send initial prompt if provided
                if (!string.IsNullOrWhiteSpace(InitialPrompt))
                {
                    var promptToSend = InitialPrompt;
                    InitialPrompt = null;
                    Logger?.LogInformation("[JARVIS-INIT] Sending initial prompt: {Length} chars", promptToSend.Length);
                    _ = _chatBridge?.RequestExternalPromptAsync(promptToSend);
                }
            };

            _blazorWebView.RootComponents.Add<WileyWidget.WinForms.BlazorComponents.App>("#app");

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
                    BeginInvoke(new Action(() => SyncThemeWithBlazor(themeName, isDark)));
                    return;
                }

                Logger?.LogInformation("[JARVIS-THEME] Synced theme {Theme} (IsDark: {IsDark}) to Blazor", themeName, isDark);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-THEME] Failed to sync theme to BlazorWebView");
            }
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowError(message)));
                return;
            }

            this.Controls.Clear();
            this.Controls.Add(new Label
            {
                Text = $"Error: {message}",
                ForeColor = Color.Red,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "JARVISChatUserControl";
            this.Size = new Size(400, 600);
            this.MinimumSize = new Size(760, 520);
            this.AutoScroll = false;
            this.Padding = Padding.Empty;
            this.Margin = Padding.Empty;
            try
            {
                var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
                this.ApplySyncfusionTheme(theme, Logger);
            }
            catch { /* best-effort */ }
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_themeService != null && _themeChangedHandler != null)
                {
                    _themeService.ThemeChanged -= _themeChangedHandler;
                    _themeChangedHandler = null;
                }

                _blazorWebView?.Dispose();
                _initLock?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class JARVISChatViewModel { }
}
