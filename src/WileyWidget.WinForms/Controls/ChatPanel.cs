using System;
using System.Diagnostics.CodeAnalysis;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using System.Windows.Forms;
using WileyWidget.WinForms.ViewModels;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.AspNetCore.Components;
using WileyWidget.WinForms.BlazorComponents;
using WileyWidget.Services.Abstractions;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// WinForms panel wrapper for the JARVIS AI Chat Blazor component.
/// Provides docking support and lifecycle management for the AI chat interface.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ChatPanel : ScopedPanelBase<ChatPanelViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private GradientPanelExt? _chatContainer;
    private BlazorWebView? _blazorView;
    private IChatBridgeService? _chatBridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatPanel"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scopes.</param>
    /// <param name="logger">Logger instance for diagnostic logging.</param>
    public ChatPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<ChatPanelViewModel>> logger)
        : base(scopeFactory, logger)
    {
    }

    /// <summary>
    /// Called after initialization. Sets up the panel UI.
    /// </summary>
    /// <param name="viewModel">The resolved ChatPanelViewModel instance.</param>
    protected override void OnViewModelResolved(ChatPanelViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);
        InitializeControls();
        WireUpEventHandlers();
        Logger.LogDebug("ChatPanel initialized");
    }

    private void InitializeControls()
    {
        SuspendLayout();

        Name = "ChatPanel";
        AccessibleName = "AI Chat Panel";
        Size = new Size(600, 600);
        MinimumSize = new Size(400, 400);
        AutoScroll = false;  // No need with Dock.Fill children; prevents odd scrolling/clipping
        Padding = new Padding(0, 0, 0, 4);  // Reduce top/bottom if header overlaps; let container handle sides
        // DockingManager will handle docking; do not set Dock here.

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "AI Chat";
        _panelHeader.RefreshClicked += (s, e) =>
        {
            Logger.LogDebug("ChatPanel refresh requested");
        };
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Chat container
        _chatContainer = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4),  // Less padding to maximize Blazor space
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)  // Theme cascade from SfSkinManager handles all colors
        };
        SfSkinManager.SetVisualStyle(_chatContainer, "Office2019Colorful");

        // Initialize BlazorWebView for JARVIS Chat
        try
        {
            InitializeBlazorWebView();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize BlazorWebView");
            // Fallback to error message
            var errorLabel = new Label
            {
                Text = $"⚠️ Failed to load AI Chat\n\n{ex.Message}\n\nEnsure WebView2 Runtime is installed.",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular),
                ForeColor = System.Drawing.Color.DarkRed
            };
            _chatContainer.Controls.Add(errorLabel);
        }

        Controls.Add(_chatContainer);

        ResumeLayout(false);
        PerformLayout();

        Logger.LogDebug("ChatPanel controls initialized");
    }

    private void InitializeBlazorWebView()
    {
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("Service scope not initialized");
        }

        Logger.LogDebug("Initializing BlazorWebView");

        _blazorView = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html"
        };

        // Use scoped service provider (NOT Program.Services)
        _blazorView.Services = ServiceProvider;

        // Add root component - JARVISAssist.razor
        _blazorView.RootComponents.Add(new RootComponent("#app", typeof(JARVISAssist), null));

        _chatContainer?.Controls.Add(_blazorView);

        Logger.LogInformation("BlazorWebView initialized successfully");
    }

    private void WireUpEventHandlers()
    {
        if (ServiceProvider == null || ViewModel == null)
        {
            Logger.LogWarning("Cannot wire up event handlers - service scope or ViewModel is null");
            return;
        }

        try
        {
            // Get ChatBridgeService from scoped provider
            _chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IChatBridgeService>(ServiceProvider);

            if (_chatBridge == null)
            {
                Logger.LogWarning("IChatBridgeService not available in service provider");
                return;
            }

            // Subscribe to PromptSubmitted event
            _chatBridge.PromptSubmitted += async (s, e) =>
            {
                try
                {
                    Logger.LogDebug("Processing prompt from Blazor: {Prompt}", e.Prompt);
                    await ProcessPromptAsync(e.Prompt);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing prompt");
                    await _chatBridge.SendResponseChunkAsync($"❌ Error: {ex.Message}");
                }
            };

            Logger.LogInformation("ChatBridgeService event handlers wired up successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to wire up event handlers");
        }
    }

    private async Task ProcessPromptAsync(string prompt)
    {
        if (ViewModel == null || _chatBridge == null)
        {
            return;
        }

        // Delegate to ViewModel with streaming callback
        await ViewModel.ProcessUserPromptAsync(prompt, async (chunk) =>
        {
            // Stream each chunk back to Blazor component
            await _chatBridge.SendResponseChunkAsync(chunk);
        });
    }

    private void ClosePanel()
    {
        try
        {
            // Find parent form and locate DockingManager
            var form = FindForm();
            if (form != null)
            {
                var dockingManager = FindDockingManager(form);
                if (dockingManager != null)
                {
                    dockingManager.SetEnableDocking(this, false);
                    Logger.LogDebug("ChatPanel closed via DockingManager");
                }
                else
                {
                    // Fallback: just hide the panel
                    Visible = false;
                    Logger.LogDebug("ChatPanel hidden (DockingManager not found)");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error closing ChatPanel");
        }
    }

    private static DockingManager? FindDockingManager(Form form)
    {
        // DockingManager is a component, not a control - search form's components
        if (form.Site?.Container != null)
        {
            foreach (System.ComponentModel.IComponent component in form.Site.Container.Components)
            {
                if (component is DockingManager dm)
                {
                    return dm;
                }
            }
        }

        // Fallback: search via reflection for private _dockingManager field
        var dockingManagerField = form.GetType()
            .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (dockingManagerField != null)
        {
            var value = dockingManagerField.GetValue(form);
            if (value is DockingManager dm)
            {
                return dm;
            }
        }

        return null;
    }

    /// <summary>
    /// Disposes the panel and all managed resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Logger.LogDebug("Disposing ChatPanel");

                // Unsubscribe from events
                if (_chatBridge != null)
                {
                    _chatBridge.PromptSubmitted -= null; // Clear all subscribers
                }

                _blazorView?.SafeDispose();
                _panelHeader?.SafeDispose();
                _chatContainer?.SafeDispose();

                Logger.LogDebug("ChatPanel disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error during ChatPanel disposal");
            }
        }

        base.Dispose(disposing);
    }
}
