using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Blazor-based ChatPanel.
/// Acts as a bridge between the WinForms host and the Blazor UI via IChatBridgeService.
/// Supports JARVIS personality and response streaming.
/// </summary>
public partial class ChatPanelViewModel : ViewModelBase, IDisposable
{
    private readonly IChatBridgeService _chatBridge;
    private readonly IJARVISPersonalityService _jarvisService;
    private readonly ILogger<ChatPanelViewModel> _logger;
    private bool _disposed;

    /// <summary>
    /// Property for binding to the UI (if needed).
    /// Most UI state is handled within the Blazor component.
    /// </summary>
    public string Status { get; set; } = "Ready";

    public ChatPanelViewModel(
        IChatBridgeService chatBridge,
        IJARVISPersonalityService jarvisService,
        ILogger<ChatPanelViewModel> logger)
        : base(logger)
    {
        _chatBridge = chatBridge ?? throw new ArgumentNullException(nameof(chatBridge));
        _jarvisService = jarvisService ?? throw new ArgumentNullException(nameof(jarvisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("ChatPanelViewModel initialized for JARVIS AI");
    }

    /// <summary>
    /// Processes a manual prompt request from WinForms code.
    /// Useful for "Ask JARVIS about this" context menus.
    /// </summary>
    public async Task RequestExternalPromptAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        
        _logger.LogInformation("Requesting external prompt via bridge: {PromptLength} chars", prompt.Length);
        await _chatBridge.RequestExternalPromptAsync(prompt);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="ChatPanelViewModel"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup managed resources if any were added at this level
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
