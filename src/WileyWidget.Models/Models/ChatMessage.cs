using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Lightweight chat message model that can be shared across UI and service layers.
/// Text is mirrored to the <see cref="Message"/> property so existing WPF bindings keep working.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Initializes a new message and assigns the default AI author when the sender is not the user.
    /// </summary>
    public ChatMessage()
    {
    }

    /// <summary>
    /// Indicates if this message was provided by an end user.
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// Primary message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// alias for WPF bindings that expect a Text property (e.g., Syncfusion chat controls).
    /// </summary>
    public string Text
    {
        get => Message;
        set => Message = value ?? string.Empty;
    }

    /// <summary>
    /// Optional author metadata; the WPF layer assigns Syncfusion Author instances here.
    /// </summary>
    public object? Author { get; set; }

    /// <summary>
    /// Timestamp recorded when the message was created or received.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Alias for legacy code that previously relied on TextMessage.DateTime.
    /// </summary>
    public DateTime DateTime
    {
        get => Timestamp;
        set => Timestamp = value;
    }

    /// <summary>
    /// Arbitrary metadata that callers can attach to the message payload.
    /// </summary>
    public IDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// Creates a message authored by the user.
    /// </summary>
    public static ChatMessage CreateUserMessage(string content)
    {
        var utcNow = DateTime.UtcNow;
        return new ChatMessage
        {
            Message = content ?? string.Empty,
            IsUser = true,
            Timestamp = utcNow
        };
    }

    /// <summary>
    /// Creates a message authored by the AI assistant.
    /// </summary>
    public static ChatMessage CreateAIMessage(string content)
    {
        var utcNow = DateTime.UtcNow;
        return new ChatMessage
        {
            Message = content ?? string.Empty,
            IsUser = false,
            Timestamp = utcNow
        };
    }
}