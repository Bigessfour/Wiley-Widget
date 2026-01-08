using System;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for bridging communication between Blazor chat components and WinForms backend.
/// Carries prompts from Blazor to the backend and streams responses back.
/// </summary>
public interface IChatBridgeService
{
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
    /// Submit a prompt from the Blazor component to the backend.
    /// </summary>
    /// <param name="prompt">The user prompt text</param>
    Task SubmitPromptAsync(string prompt);

    /// <summary>
    /// Send a response chunk back to the Blazor component.
    /// </summary>
    /// <param name="chunk">The response chunk to send</param>
    Task SendResponseChunkAsync(string chunk);

    /// <summary>
    /// Notify that a suggestion has been selected.
    /// </summary>
    /// <param name="suggestion">The selected suggestion text</param>
    Task NotifySuggestionSelectedAsync(string suggestion);
}

/// <summary>
/// Event arguments for prompt submission
/// </summary>
public class ChatPromptSubmittedEventArgs : EventArgs
{
    public string Prompt { get; set; } = string.Empty;
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
