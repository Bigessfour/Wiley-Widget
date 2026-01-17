using System.Threading;
using System;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for bridging communication between Blazor chat components and WinForms backend.
/// Carries prompts from Blazor to the backend and streams responses back.
/// </summary>
public interface IChatBridgeService
{
    /// <summary>
    /// Raised when a new message is received.
    /// </summary>
    event EventHandler<ChatMessage> OnMessageReceived;

    /// <summary>
    /// Raised when a prompt is submitted from the Blazor chat component.
    /// </summary>
    event EventHandler<ChatPromptSubmittedEventArgs> PromptSubmitted;

    /// <summary>
    /// Raised when a response chunk is received from the backend service.
    /// Used for streaming responses back to Blazor.
    /// </summary>
    event EventHandler<ChatResponseChunkEventArgs> ResponseChunkReceived;

    /// <summary>
    /// Raised when a suggestion is selected by the user.
    /// </summary>
    event EventHandler<ChatSuggestionSelectedEventArgs> SuggestionSelected;

    /// <summary>
    /// Raised when an external source requests a prompt to be submitted (e.g. from an insight card).
    /// </summary>
    event EventHandler<ChatExternalPromptEventArgs> ExternalPromptRequested;

    /// <summary>
    /// Notify that a new message has been received (e.g. from the backend).
    /// </summary>
    /// <param name="message">The received message</param>
    Task NotifyMessageReceivedAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a prompt from the Blazor component to the backend.
    /// </summary>
    /// <param name="prompt">The user prompt text</param>
    /// <param name="conversationId">The target conversation ID for persistence</param>
    Task SubmitPromptAsync(string prompt, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a response chunk back to the Blazor component.
    /// </summary>
    /// <param name="chunk">The response chunk to send</param>
    Task SendResponseChunkAsync(string chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify that a suggestion has been selected.
    /// </summary>
    /// <param name="suggestion">The selected suggestion text</param>
    Task NotifySuggestionSelectedAsync(string suggestion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests that a prompt be submitted from an external source.
    /// </summary>
    /// <param name="prompt">The prompt to submit</param>
    Task RequestExternalPromptAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments for external prompt requests
/// </summary>
public class ChatExternalPromptEventArgs : EventArgs
{
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for prompt submission
/// </summary>
public class ChatPromptSubmittedEventArgs : EventArgs
{
    public string Prompt { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for response chunk receipt
/// </summary>
public class ChatResponseChunkEventArgs : EventArgs
{
    public string Chunk { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for suggestion selection
/// </summary>
public class ChatSuggestionSelectedEventArgs : EventArgs
{
    public string Suggestion { get; set; } = string.Empty;
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
}
