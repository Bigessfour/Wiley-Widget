using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.BlazorComponents;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Modal host form for the JARVIS AI Assistant (Blazor UI).
    /// Follows Option B architecture: a dedicated modal dialog for AI chat.
    /// </summary>
    public partial class JARVISChatHostForm : global::Syncfusion.WinForms.Controls.SfForm
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IThemeService _themeService;
        private readonly ILogger<JARVISChatHostForm> _logger;
        private BlazorWebView _blazorWebView;

        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS when the form is shown.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        public JARVISChatHostForm(IServiceProvider serviceProvider, IThemeService themeService, ILogger<JARVISChatHostForm> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Initializing JARVISChatHostForm (Modal Chat)");

            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this._blazorWebView = new BlazorWebView();
            this.SuspendLayout();

            // Form settings
            this.Text = "JARVIS AI Assistant";
            this.Size = new Size(1100, 850); // Larger size for better data visualization in chat
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.ShowIcon = true;
            this.MinimumSize = new Size(800, 600);

            // BlazorWebView settings
            this._blazorWebView.Dock = DockStyle.Fill;
            this._blazorWebView.HostPage = "wwwroot/index.html";
            this._blazorWebView.Services = _serviceProvider;
            this._blazorWebView.AccessibleName = "JARVIS AI Chat Interface";
            this._blazorWebView.RootComponents.Add<JARVISAssist>("#app");

            this.Controls.Add(this._blazorWebView);

            this.ResumeLayout(false);

            _logger.LogDebug("JARVISChatHostForm components initialized");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!string.IsNullOrWhiteSpace(InitialPrompt?.Trim()))
            {
                BeginInvoke(new Func<Task>(async () => await SendInitialPromptAsync().ConfigureAwait(true)));
            }
        }

        /// <summary>
        /// Sends the initial prompt to JARVIS after a delay. Made protected for testing.
        /// </summary>
        protected async Task SendInitialPromptAsync()
        {
            var prompt = InitialPrompt?.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            try
            {
                _logger.LogInformation("Sending initial prompt to JARVIS: {PromptLength} chars", prompt.Length);

                // Small delay to ensure Blazor WebView is fully ready and JARVISAssist is initialized
                await Task.Delay(1500).ConfigureAwait(true);

                if (IsDisposed || Disposing)
                {
                    return;
                }

                var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<IChatBridgeService>(_serviceProvider);
                await chatBridge.RequestExternalPromptAsync(prompt).ConfigureAwait(true);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("JARVISChatHostForm disposed before initial prompt could be sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send initial prompt to JARVIS");
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var themeName = _themeService.CurrentTheme;
                AppThemeColors.ApplyTheme(this, themeName);
                _logger.LogDebug("Theme '{Theme}' applied to JARVISChatHostForm", themeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme to JARVISChatHostForm");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _logger.LogInformation("JARVISChatHostForm closing");
            base.OnFormClosing(e);
        }
    }
}
