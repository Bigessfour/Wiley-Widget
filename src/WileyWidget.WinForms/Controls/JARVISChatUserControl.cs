using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.BlazorComponents;
using WileyWidget.WinForms.Services;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// UserControl hosting the JARVIS AI Assistant Blazor UI.
    /// Can be docked in the right panel or embedded anywhere in the application.
    /// Supports initial prompts for programmatic interaction.
    /// </summary>
    public partial class JARVISChatUserControl : UserControl
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IThemeService _themeService;
        private readonly ILogger<JARVISChatUserControl>? _logger;
        private BlazorWebView? _blazorWebView;

        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS when the control is shown.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        /// <summary>
        /// Initializes a new instance of JARVISChatUserControl with DI dependencies.
        /// </summary>
        public JARVISChatUserControl(IServiceProvider serviceProvider, IThemeService themeService, ILogger<JARVISChatUserControl>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _logger = logger;

            _logger?.LogInformation("Initializing JARVISChatUserControl");

            InitializeComponent();
            ApplyTheme();
        }

        /// <summary>
        /// Initializes a new instance of JARVISChatUserControl using Program.Services.
        /// </summary>
        public JARVISChatUserControl(IServiceProvider serviceProvider)
            : this(
                serviceProvider,
                ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(serviceProvider),
                ServiceProviderServiceExtensions.GetService<ILogger<JARVISChatUserControl>>(serviceProvider))
        {
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // UserControl settings
            this.Name = "JARVISChatUserControl";
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;

            // Create BlazorWebView
            _blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html",
                Services = _serviceProvider
            };

            // Add Blazor component
            _blazorWebView.RootComponents.Add<JARVISAssist>("#app");

            this.Controls.Add(_blazorWebView);

            this.ResumeLayout(false);

            _logger?.LogDebug("JARVISChatUserControl components initialized");
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!string.IsNullOrEmpty(InitialPrompt))
            {
                try
                {
                    _logger?.LogInformation("Sending initial prompt to JARVIS: {PromptLength} chars", InitialPrompt.Length);

                    // Delay to ensure Blazor WebView is fully ready
                    await System.Threading.Tasks.Task.Delay(1500);

                    var chatBridge = ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(_serviceProvider);
                    await chatBridge.RequestExternalPromptAsync(InitialPrompt);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send initial prompt to JARVIS");
                }
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var themeName = _themeService.CurrentTheme;
                // Apply theme via SfSkinManager since UserControl doesn't have AppThemeColors.ApplyTheme support
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, themeName);
                _logger?.LogDebug("Theme '{Theme}' applied to JARVISChatUserControl", themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to apply theme to JARVISChatUserControl");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _blazorWebView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
