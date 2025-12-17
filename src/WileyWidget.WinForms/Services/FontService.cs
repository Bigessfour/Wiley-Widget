using System;
using System.Drawing;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Singleton service for centralized font management across the application.
/// Provides real-time font updates with event-driven notifications.
/// </summary>
public sealed class FontService : IDisposable
{
    private static readonly Lazy<FontService> _instance = new(() => new FontService());
    private Font _currentFont;

    /// <summary>
    /// Gets the singleton instance of the FontService.
    /// </summary>
    public static FontService Instance => _instance.Value;

    /// <summary>
    /// Gets the current application font.
    /// </summary>
    public Font CurrentFont => _currentFont;

    /// <summary>
    /// Event raised when the application font changes.
    /// </summary>
    public event EventHandler<FontChangedEventArgs>? FontChanged;

    private FontService()
    {
        // Default font matching modern .NET defaults
        _currentFont = new Font("Segoe UI", 9f, FontStyle.Regular);
    }

    /// <summary>
    /// Sets the application font and notifies all subscribers.
    /// </summary>
    /// <param name="newFont">The new font to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when newFont is null.</exception>
    public void SetApplicationFont(Font newFont)
    {
        if (newFont == null)
        {
            throw new ArgumentNullException(nameof(newFont));
        }

        if (newFont.Equals(_currentFont))
        {
            return;
        }

        var oldFont = _currentFont;
        _currentFont = newFont;

        FontChanged?.Invoke(this, new FontChangedEventArgs(oldFont, newFont));

        // Dispose the old font if it's different
        oldFont?.Dispose();
    }

    /// <summary>
    /// Disposes the current font resource.
    /// </summary>
    public void Dispose()
    {
        _currentFont?.Dispose();
    }
}

/// <summary>
/// Event arguments for font change notifications.
/// </summary>
public class FontChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous font.
    /// </summary>
    public Font OldFont { get; }

    /// <summary>
    /// Gets the new font.
    /// </summary>
    public Font NewFont { get; }

    /// <summary>
    /// Initializes a new instance of FontChangedEventArgs.
    /// </summary>
    /// <param name="oldFont">The previous font.</param>
    /// <param name="newFont">The new font.</param>
    public FontChangedEventArgs(Font oldFont, Font newFont)
    {
        OldFont = oldFont ?? throw new ArgumentNullException(nameof(oldFont));
        NewFont = newFont ?? throw new ArgumentNullException(nameof(newFont));
    }
}