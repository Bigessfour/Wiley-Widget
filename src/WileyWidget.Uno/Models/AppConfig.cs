namespace WileyWidget.Uno.Models;

/// <summary>
/// Application configuration model.
/// </summary>
public record AppConfig
{
    /// <summary>
    /// Current environment (Development, Staging, Production).
    /// </summary>
    public string Environment { get; init; } = "Development";

    /// <summary>
    /// Application name.
    /// </summary>
    public string AppName { get; init; } = "WileyWidget";

    /// <summary>
    /// Application version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";
}
