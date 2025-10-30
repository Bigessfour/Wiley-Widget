using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Persisted user-facing settings. Keep only values that must survive restarts; transient UI state stays in memory.
/// Nullable primitives used to distinguish 'not yet set' from legitimate 0 values (e.g., window geometry).
/// </summary>
// AppSettings moved to Models/AppSettings.cs

/// <summary>
/// Simple service for loading/saving <see cref="AppSettings"/> as JSON in AppData.
/// Handles corruption by renaming the bad file and regenerating defaults.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    /// <summary>
    /// Gets the singleton instance from DI container.
    /// </summary>
    // Provide a best-effort static instance accessor to maintain compatibility with
    // existing call-sites that expect SettingsService.Instance. Prefer resolving
    // from DI in the application startup; if not available, fall back to a
    // process-wide lazy singleton to keep behavior working in unit tests and
    // simple execution scenarios.
    private static SettingsService? _singleton;
    private static readonly object _singletonLock = new();
    // Use a Lazy fallback for the default instance creation and a single atomic
    // assignment to avoid double-checked-locking patterns that can confuse analyzers.
    private static readonly Lazy<SettingsService> _defaultSingleton = new(() => new SettingsService(), true);

    public static SettingsService Instance
    {
        get
        {
            // Fast-path: if a DI-provided instance has been set, return it.
            if (_singleton != null) return _singleton;

            // Ensure a single assignment of the default instance in a thread-safe way
            // without relying on a second null-check that static analyzers may consider dead.
            var created = _defaultSingleton.Value;
            System.Threading.Interlocked.CompareExchange(ref _singleton, created, null);
            return _singleton!;
        }
    }

    /// <summary>
    /// Allows the application (typically during startup) to provide the DI-constructed
    /// instance so call-sites that use the static accessor get the container-managed object.
    /// </summary>
    public static void SetInstance(SettingsService instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        lock (_singletonLock)
        {
            _singleton = instance;
        }
    }

    private readonly IConfiguration? _configuration;
    private readonly ILogger<SettingsService> _logger;

    private string _root = string.Empty;
    private string _file = string.Empty;
    /// <summary>
    /// Parameterless constructor for legacy or test scenarios only.
    /// Prefer resolving via DI to ensure dependencies are injected.
    /// </summary>
    internal SettingsService()
        : this(null, NullLogger<SettingsService>.Instance)
    {
    }

    /// <summary>
    /// The in-memory settings instance.
    /// Implemented to satisfy <see cref="ISettingsService"/> and used throughout the app.
    /// </summary>
    public AppSettings Current { get; private set; } = new AppSettings();

    public SettingsService(IConfiguration? configuration, ILogger<SettingsService>? logger)
    {
        _configuration = configuration;
        _logger = logger ?? NullLogger<SettingsService>.Instance;
        InitializePaths();
    }

    private void InitializePaths()
    {
        var overrideDir = _configuration?["Settings:Directory"]
                          ?? Environment.GetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR");

        _root = string.IsNullOrWhiteSpace(overrideDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget")
            : overrideDir;
        _file = Path.Combine(_root, "settings.json");

        _logger.LogDebug("Settings directory resolved to {SettingsDirectory}.", _root);
    }

    public void ResetForTests()
    {
        // Don't call InitializePaths() as it would overwrite the test directory set via reflection
        Current = new AppSettings();
    }

    /// <summary>
    /// Loads the persisted application settings and returns the in-memory instance for fluent usage.
    /// </summary>
    public AppSettings LoadSettings()
    {
        Load();
        return Current;
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
                _logger.LogInformation("Settings file not found. Created default settings at {SettingsFile}.", _file);
                return;
            }
            var json = File.ReadAllText(_file);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null)
                Current = loaded;
            // Migration: populate new Qbo* from legacy QuickBooks* if empty (first-run after upgrade).
            if (string.IsNullOrWhiteSpace(Current.QboAccessToken) && !string.IsNullOrWhiteSpace(Current.QuickBooksAccessToken))
            {
                Current.QboAccessToken = Current.QuickBooksAccessToken;
                Current.QboRefreshToken = Current.QuickBooksRefreshToken;
                if (Current.QuickBooksTokenExpiresUtc.HasValue)
                    Current.QboTokenExpiry = Current.QuickBooksTokenExpiresUtc.Value;
            }
            _logger.LogDebug("Settings loaded successfully from {SettingsFile}.", _file);
        }
        catch (Exception ex)
        {
            // rename corrupted file and recreate
            try
            {
                if (File.Exists(_file))
                {
                    var bad = _file + ".bad_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    File.Move(_file, bad);
                    _logger.LogWarning(ex, "Settings file corrupt; moved to {BackupFile} and regenerating defaults.", bad);
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
            _logger.LogDebug("Settings saved successfully to {SettingsFile}.", _file);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist settings to {SettingsFile}.", _file);
        }
    }

    public string Get(string key)
    {
        // Not implemented for AppSettings based service
        throw new NotImplementedException();
    }

    public void Set(string key, string value)
    {
        // Not implemented for AppSettings based service
        throw new NotImplementedException();
    }
}
