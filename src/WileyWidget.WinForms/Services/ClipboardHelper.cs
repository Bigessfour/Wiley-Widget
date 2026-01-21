using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Provides safe clipboard operations for .NET 10+ using JSON serialization.
/// Replaces deprecated BinaryFormatter-based clipboard operations.
/// 
/// Usage:
///   var budget = new BudgetEntry { Amount = 5000 };
///   await ClipboardHelper.CopyAsJsonAsync(budget, _logger);
///   var retrieved = await ClipboardHelper.PasteAsJsonAsync&lt;BudgetEntry&gt;(_logger);
/// </summary>
public static class ClipboardHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Copies an object to clipboard as formatted JSON.
    /// </summary>
    /// <typeparam name="T">Type of object to copy</typeparam>
    /// <param name="data">Object to serialize and copy</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Number of bytes copied</returns>
    /// <exception cref="ArgumentNullException">Thrown if data is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if clipboard access fails</exception>
    public static int CopyAsJson<T>(T data, ILogger? logger = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data), "Data to copy cannot be null");

        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            Clipboard.SetText(json, TextDataFormat.UnicodeText);
            
            logger?.LogInformation(
                "Copied {TypeName} to clipboard ({ByteCount} bytes)",
                typeof(T).Name,
                json.Length);

            return json.Length;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to copy {TypeName} to clipboard", typeof(T).Name);
            throw new InvalidOperationException(
                $"Failed to copy {typeof(T).Name} to clipboard: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Retrieves an object from clipboard, deserializing from JSON.
    /// </summary>
    /// <typeparam name="T">Type to deserialize into</typeparam>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Deserialized object, or null if clipboard is empty or contains invalid JSON</returns>
    /// <exception cref="InvalidOperationException">Thrown if clipboard access fails</exception>
    public static T? PasteAsJson<T>(ILogger? logger = null)
    {
        try
        {
            var json = Clipboard.GetText(TextDataFormat.UnicodeText);

            if (string.IsNullOrWhiteSpace(json))
            {
                logger?.LogWarning("Clipboard is empty; cannot paste {TypeName}", typeof(T).Name);
                return default;
            }

            var deserialized = JsonSerializer.Deserialize<T>(json, JsonOptions);
            
            logger?.LogInformation(
                "Pasted {TypeName} from clipboard ({ByteCount} bytes)",
                typeof(T).Name,
                json.Length);

            return deserialized;
        }
        catch (JsonException jex)
        {
            logger?.LogWarning(jex,
                "Clipboard contains invalid JSON for {TypeName}; returning null",
                typeof(T).Name);
            return default;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to retrieve {TypeName} from clipboard", typeof(T).Name);
            throw new InvalidOperationException(
                $"Failed to retrieve {typeof(T).Name} from clipboard: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Copies an object to clipboard as formatted JSON with indentation.
    /// Useful for debugging or user-friendly copy operations.
    /// </summary>
    /// <typeparam name="T">Type of object to copy</typeparam>
    /// <param name="data">Object to serialize and copy</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Number of bytes copied</returns>
    public static int CopyAsFormattedJson<T>(T data, ILogger? logger = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data), "Data to copy cannot be null");

        try
        {
            var options = new JsonSerializerOptions(JsonOptions) { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);
            Clipboard.SetText(json, TextDataFormat.UnicodeText);

            logger?.LogInformation(
                "Copied formatted {TypeName} to clipboard ({ByteCount} bytes)",
                typeof(T).Name,
                json.Length);

            return json.Length;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to copy formatted {TypeName} to clipboard", typeof(T).Name);
            throw new InvalidOperationException(
                $"Failed to copy formatted {typeof(T).Name} to clipboard: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Copies plain text to clipboard.
    /// </summary>
    /// <param name="text">Text to copy</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Number of characters copied</returns>
    public static int CopyText(string text, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text to copy cannot be null or empty", nameof(text));

        try
        {
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            logger?.LogDebug("Copied {CharCount} characters to clipboard", text.Length);
            return text.Length;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to copy text to clipboard");
            throw new InvalidOperationException("Failed to copy text to clipboard", ex);
        }
    }

    /// <summary>
    /// Retrieves plain text from clipboard.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Text from clipboard, or null if clipboard is empty</returns>
    public static string? PasteText(ILogger? logger = null)
    {
        try
        {
            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            
            if (string.IsNullOrEmpty(text))
            {
                logger?.LogWarning("Clipboard is empty");
                return null;
            }

            logger?.LogDebug("Retrieved {CharCount} characters from clipboard", text.Length);
            return text;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to retrieve text from clipboard");
            throw new InvalidOperationException("Failed to retrieve text from clipboard", ex);
        }
    }

    /// <summary>
    /// Checks if clipboard contains valid JSON that can be deserialized to type T.
    /// </summary>
    /// <typeparam name="T">Type to check compatibility with</typeparam>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>True if clipboard contains valid JSON for type T</returns>
    public static bool CanPasteAsJson<T>(ILogger? logger = null)
    {
        try
        {
            var json = Clipboard.GetText(TextDataFormat.UnicodeText);

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind != JsonValueKind.Null;
        }
        catch (JsonException)
        {
            logger?.LogDebug("Clipboard does not contain valid JSON for {TypeName}", typeof(T).Name);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error checking clipboard content");
            return false;
        }
    }
}
