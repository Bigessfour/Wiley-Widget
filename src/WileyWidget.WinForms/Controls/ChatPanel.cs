using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.BlazorComponents;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Scoped panel hosting the Blazor-based JARVIS AI Assistant.
/// Uses BlazorWebView for a rich, modern conversational experience while
/// maintaining WinForms docking and lifestyle compatibility.
/// </summary>
public partial class ChatPanel : ScopedPanelBase<ChatPanelViewModel>
{
    private PanelHeader? _panelHeader;
    private BlazorWebView? _blazorWebView;
    private Panel? _containerPanel;

    public ChatPanel(IServiceScopeFactory scopeFactory, ILogger<ChatPanel> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Name = "ChatPanel";
        this.Size = new Size(400, 600);

        // Panel header (JARVIS title and close button)
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "JARVIS AI Assistant",
            ShowRefreshButton = false,
            ShowHelpButton = false
        };
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        this.Controls.Add(_panelHeader);

        _containerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Name = "ChatContainer"
        };

        this.Controls.Add(_containerPanel);
        
        this.PerformLayout();
        this.Refresh();
        
        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    protected override void OnViewModelResolved(ChatPanelViewModel viewModel)
    {
        _logger.LogInformation("Initializing BlazorWebView in ChatPanel");

        try
        {
            // Create the BlazorWebView
            _blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html",
                Name = "JarvisWebView"
            };

            // CRITICAL: Use the scoped service provider from ScopedPanelBase
            if (ServiceProvider != null)
            {
                _blazorWebView.Services = ServiceProvider;
                _logger.LogDebug("BlazorWebView assigned to scoped service provider");
            }

            // Target the #app element in index.html and render JARVISAssist component
            _blazorWebView.RootComponents.Add(new RootComponent("#app", typeof(JARVISAssist), null));

            _containerPanel?.Controls.Add(_blazorWebView);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize BlazorWebView in ChatPanel");
            
            // Fallback display
            var errorLabel = new Label
            {
                Text = $"Error loading JARVIS AI: {ex.Message}",
                ForeColor = Color.Red,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _containerPanel?.Controls.Add(errorLabel);
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

    private void ClosePanel()
    {
        try
        {
            var parentForm = FindForm();
            if (parentForm == null) return;

            var closePanelMethod = parentForm.GetType().GetMethod(
                "ClosePanel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            closePanelMethod?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ChatPanel: Failed to close panel via parent form");
        }
    }
}
