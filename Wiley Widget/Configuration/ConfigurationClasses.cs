namespace WileyWidget.Configuration;

/// <summary>
/// Application settings configuration.
/// </summary>
public class AppSettings
{
    public string ApplicationName { get; set; }
    public string Version { get; set; }
    public string Environment { get; set; }
    public LoggingSettings Logging { get; set; }
    public UiSettings UI { get; set; }
}

/// <summary>
/// Database configuration settings.
/// </summary>
public class DatabaseSettings
{
    public string ConnectionString { get; set; }
    public string Provider { get; set; }
    public int CommandTimeout { get; set; }
    public bool EnableMigrations { get; set; }
}

/// <summary>
/// Security configuration settings.
/// </summary>
public class SecuritySettings
{
    public bool EnableSecurityAuditing { get; set; }
    public string EncryptionKey { get; set; }
    public int SessionTimeoutMinutes { get; set; }
}

/// <summary>
/// Logging configuration settings.
/// </summary>
public class LoggingSettings
{
    public string Level { get; set; }
    public bool EnableFileLogging { get; set; }
    public bool EnableConsoleLogging { get; set; }
    public string LogDirectory { get; set; }
}

/// <summary>
/// UI configuration settings.
/// </summary>
public class UiSettings
{
    public string DefaultTheme { get; set; }
    public bool EnableAnimations { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
}
