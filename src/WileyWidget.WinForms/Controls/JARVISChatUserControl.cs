using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.BlazorComponents;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Dockable UserControl for the JARVIS AI Assistant (Blazor UI).
    /// Inherits from ScopedPanelBase for automatic ViewModel resolution, theme cascade, and resource disposal.
    /// Designed to be docked as a TabPage in the right panel alongside Activity Log.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase<JARVISChatViewModel>
    {
        private readonly IServiceProvider _serviceProvider;
        private BlazorWebView? _blazorWebView;

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

            Logger?.LogInformation("Initializing JARVISChatUserControl (Docked Chat) — BlazorWebView.Services injected (Option A pattern)");

            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // UserControl settings
            this.Name = "JARVISChatUserControl";
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

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
                this.ResumeLayout(false);
                Logger?.LogInformation("JARVISChatUserControl initialized in headless test mode (BlazorWebView skipped)");
                return;
            }

            _blazorWebView = new BlazorWebView();

            // BlazorWebView settings
            _blazorWebView.Dock = DockStyle.Fill;
            _blazorWebView.HostPage = "wwwroot/index.html";
            _blazorWebView.Services = _serviceProvider;
            _blazorWebView.AccessibleName = "JARVIS AI Chat Interface";
            _blazorWebView.RootComponents.Add<JARVISAssist>("#app");
            _blazorWebView.Name = "JARVISChatBlazorView";

            this.Controls.Add(_blazorWebView);

            this.ResumeLayout(false);

            Logger?.LogDebug("JARVISChatUserControl components initialized");
        }

        private static bool IsHeadlessTestMode()
        {
            return string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                   || !Environment.UserInteractive;
        }

        /// <summary>
        /// Called when the control handle is created (WinForms lifecycle).
        /// Defers initial prompt sending to ensure BlazorWebView is fully initialized.
        /// Personality service is resolved lazily in GrokAgentService.InitializeAsync() post-Semantic Kernel setup.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Schedule initial prompt send after a delay to ensure Blazor WebView is fully initialized
            var prompt = InitialPrompt?.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                Logger?.LogDebug("JARVISChatUserControl.OnHandleCreated — No initial prompt configured");
                return;
            }

            try
            {
                BeginInvoke(new Func<Task>(async () =>
                {
                    try
                    {
                        Logger?.LogInformation("Scheduling initial prompt to JARVIS: {PromptLength} chars (lazy personality init)", prompt.Length);

                        // Small delay to ensure Blazor WebView is fully ready and JARVISAssist is initialized
                        await Task.Delay(1500).ConfigureAwait(true);

                        if (IsDisposed || Disposing)
                        {
                            Logger?.LogDebug("JARVISChatUserControl disposed before initial prompt could be sent");
                            return;
                        }

                        var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                            .GetRequiredService<IChatBridgeService>(_serviceProvider);
                        await chatBridge.RequestExternalPromptAsync(prompt).ConfigureAwait(true);

                        Logger?.LogInformation("Initial prompt sent to JARVIS successfully");
                    }
                    catch (ObjectDisposedException)
                    {
                        Logger?.LogDebug("JARVISChatUserControl disposed before initial prompt could be sent");
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Failed to send initial prompt to JARVIS");
                    }
                }));
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to schedule initial prompt send");
            }
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
                _blazorWebView?.Dispose();
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



