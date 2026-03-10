using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents a user-selected file attachment that can be included with a JARVIS prompt.
/// </summary>
public sealed class ChatPromptAttachment
{
    /// <summary>
    /// Gets or sets the display file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the browser-reported content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the extracted text content forwarded to the AI prompt.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the extracted content was truncated.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Gets a short human-readable size string.
    /// </summary>
    public string GetDisplaySize()
    {
        if (SizeBytes < 1024)
        {
            return $"{SizeBytes} B";
        }

        if (SizeBytes < 1024 * 1024)
        {
            return $"{Math.Round(SizeBytes / 1024d, 1)} KB";
        }

        return $"{Math.Round(SizeBytes / 1024d / 1024d, 1)} MB";
    }
}
