using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Interface for settings service
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current application settings
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Loads settings from disk
    /// </summary>
    void Load();

    /// <summary>
    /// Convenience helper that loads persisted settings and returns the current instance.
    /// </summary>
    AppSettings LoadSettings();

    /// <summary>
    /// Saves current settings to disk
    /// </summary>
    void Save();
}
