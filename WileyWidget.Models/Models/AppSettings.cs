using System;

namespace WileyWidget.Models;

/// <summary>
/// Persisted user-facing settings. Contains only values that must survive restarts.
/// QBO (QuickBooks Online) tokens are stored to allow silent refresh on next launch.
/// Legacy QuickBooks* properties retained temporarily for migration; new canonical names use Qbo* prefix.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Primary key for the settings entity
    /// </summary>
    public int Id { get; set; }

    // Theme + window geometry
    public string Theme { get; set; } = "FluentDark";
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool? WindowMaximized { get; set; }

    // Grid column preferences
    public bool UseDynamicColumns { get; set; } = false;

    // Advanced settings
    public bool EnableDataCaching { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 30;
    public string SelectedLogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/wiley-widget.log";

    // Legacy QuickBooks token/property names (kept for one migration cycle)
    public string? QuickBooksAccessToken { get; set; }
    public string? QuickBooksRefreshToken { get; set; }
    public string? QuickBooksRealmId { get; set; }
    public string QuickBooksEnvironment { get; set; } = "sandbox"; // or "production"
    public DateTime? QuickBooksTokenExpiresUtc { get; set; }

    // Canonical QBO properties going forward
    public string? QboAccessToken { get; set; }
    public string? QboRefreshToken { get; set; }
    public DateTime QboTokenExpiry { get; set; } // UTC absolute expiry of access token
    public string? QboClientId { get; set; }
    public string? QboClientSecret { get; set; }

    // Report settings
    public string? LastSelectedReportType { get; set; }
    public string? LastSelectedFormat { get; set; }
    public DateTime? LastReportStartDate { get; set; }
    public DateTime? LastReportEndDate { get; set; }
    public bool IncludeChartsInReports { get; set; } = true;
    public int LastSelectedEnterpriseId { get; set; }
}
