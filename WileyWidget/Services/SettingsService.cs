using System;
using System.IO;
using System.Text.Json;

namespace WileyWidget.Services;

/// <summary>
/// Persisted user-facing settings. Keep only values that must survive restarts; transient UI state stays in memory.
/// Nullable primitives used to distinguish 'not yet set' from legitimate 0 values (e.g., window geometry).
/// </summary>
public class AppSettings
{
    // Stored theme uses Syncfusion canonical name (e.g., FluentDark, FluentLight)
    public string Theme { get; set; } = "FluentDark";
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool? WindowMaximized { get; set; }
}

/// <summary>
/// Simple singleton service for loading/saving <see cref="AppSettings"/> as JSON in AppData.
/// Handles corruption by renaming the bad file and regenerating defaults.
/// </summary>
public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _lazy = new(() => new SettingsService());
    public static SettingsService Instance => _lazy.Value;

    private string _root;
    private string _file;

    public AppSettings Current { get; private set; } = new();

    private SettingsService()
    {
        InitializePaths();
    }

    private void InitializePaths()
    {
        var overrideDir = Environment.GetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR");
        _root = string.IsNullOrWhiteSpace(overrideDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget")
            : overrideDir;
        _file = Path.Combine(_root, "settings.json");
    }

    public void ResetForTests()
    {
        InitializePaths();
        Current = new AppSettings();
    }

    /// <summary>
    /// Loads settings from disk or creates a new file if absent. If deserialization fails, the corrupt
    /// file is renamed with a timestamp suffix to aid post-mortem diagnostics.
    /// </summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                Directory.CreateDirectory(_root);
                Save();
                return;
            }
            var json = File.ReadAllText(_file);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null)
                Current = loaded;
        }
        catch
        {
            // rename corrupted file and recreate
            try
            {
                if (File.Exists(_file))
                {
                    var bad = _file + ".bad_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Move(_file, bad);
                }
            }
            catch { }
            Current = new AppSettings();
            Save();
        }
    }

    /// <summary>
    /// Writes current settings to disk (indented JSON). Failures are swallowed intentionally: user
    /// experience should not degrade due to IO issues—consider surfacing via telemetry later.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_root);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        catch { }
    }
}
