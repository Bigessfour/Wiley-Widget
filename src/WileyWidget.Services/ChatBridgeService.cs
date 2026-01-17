using System.Threading;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of chat bridge service for Blazor-to-WinForms communication.
/// Handles bidirectional event flow between Blazor chat components and backend services.
/// </summary>
public class ChatBridgeService : IChatBridgeService
{
    private readonly ILogger<ChatBridgeService> _logger;

    public event EventHandler<ChatMessage> OnMessageReceived;
    public event EventHandler<ChatPromptSubmittedEventArgs> PromptSubmitted;
    public event EventHandler<ChatResponseChunkEventArgs> ResponseChunkReceived;
    public event EventHandler<ChatSuggestionSelectedEventArgs> SuggestionSelected;
    public event EventHandler<ChatExternalPromptEventArgs> ExternalPromptRequested;

    /// <summary>
    /// Notify that a new message has been received.
    /// </summary>
    public Task NotifyMessageReceivedAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            _logger.LogWarning("Attempted to notify null message");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Message received: {MessageLength} characters", message.Content.Length);

        OnMessageReceived?.Invoke(this, message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public ChatBridgeService(ILogger<ChatBridgeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Requests that a prompt be submitted from an external source (e.g. from an insight card).
    /// </summary>
    public Task RequestExternalPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            _logger.LogWarning("Attempted to request empty external prompt");
            return Task.CompletedTask;
        }

        _logger.LogInformation("External prompt requested: {PromptLength} characters", prompt.Length);

        var args = new ChatExternalPromptEventArgs { Prompt = prompt };
        ExternalPromptRequested?.Invoke(this, args);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Submit a prompt from the Blazor component to the backend.
    /// </summary>
    public Task SubmitPromptAsync(string prompt, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            _logger.LogWarning("Attempted to submit empty prompt");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Prompt submitted: {PromptLength} characters (ConversationId: {ConversationId})", prompt.Length, conversationId ?? "N/A");

        var args = new ChatPromptSubmittedEventArgs
        {
            Prompt = prompt,
            ConversationId = conversationId
        };
        PromptSubmitted?.Invoke(this, args);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Send a response chunk back to the Blazor component.
    /// </summary>
    public Task SendResponseChunkAsync(string chunk, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            _logger.LogDebug("Response chunk is empty");
            return Task.CompletedTask;
        }

        _logger.LogDebug("Response chunk sent: {ChunkLength} characters", chunk.Length);

        var args = new ChatResponseChunkEventArgs { Chunk = chunk };
        ResponseChunkReceived?.Invoke(this, args);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Notify that a suggestion has been selected.
    /// </summary>
    public Task NotifySuggestionSelectedAsync(string suggestion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            _logger.LogWarning("Attempted to notify empty suggestion selection");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Suggestion selected: {Suggestion}", suggestion);

        var args = new ChatSuggestionSelectedEventArgs { Suggestion = suggestion };
        SuggestionSelected?.Invoke(this, args);

        return Task.CompletedTask;
    }
}
