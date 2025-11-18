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

    // Database settings
    public string DatabaseServer { get; set; } = "localhost";
    public string DatabaseName { get; set; } = "WileyWidget";

    // QuickBooks settings
    public string? QuickBooksCompanyFile { get; set; }
    public bool EnableQuickBooksSync { get; set; } = false;
    public int SyncIntervalMinutes { get; set; } = 30;

    // Application settings
    public bool EnableAutoSave { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;

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

    // AI settings
    public bool EnableAI { get; set; } = false;
    public string? XaiApiKey { get; set; }
    public string XaiModel { get; set; } = "grok-4-0709";
    public string XaiApiEndpoint { get; set; } = "https://api.x.ai/v1";
    public int XaiTimeout { get; set; } = 30;
    public int XaiMaxTokens { get; set; } = 2000;
    public double XaiTemperature { get; set; } = 0.7;

    // Notification settings
    public bool EnableNotifications { get; set; } = true;
    public bool EnableSounds { get; set; } = true;

    // Localization settings
    public string DefaultLanguage { get; set; } = "en-US";
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    public string CurrencyFormat { get; set; } = "USD";

    // Security settings
    public int SessionTimeoutMinutes { get; set; } = 60;

    // Fiscal year settings
    public string FiscalYearStart { get; set; } = "July 1";
    public int FiscalYearStartMonth { get; set; } = 7; // July
    public int FiscalYearStartDay { get; set; } = 1;
    public string FiscalYearEnd { get; set; } = "June 30";
    public string CurrentFiscalYear { get; set; } = "2024-2025";
    public bool UseFiscalYearForReporting { get; set; } = true;
    public int FiscalQuarter { get; set; } = 1;
    public string FiscalPeriod { get; set; } = "Q1";

    // Report settings
    public string? LastSelectedReportType { get; set; }
    public string? LastSelectedFormat { get; set; }
    public DateTime? LastReportStartDate { get; set; }
    public DateTime? LastReportEndDate { get; set; }
    public bool IncludeChartsInReports { get; set; } = true;
    public int LastSelectedEnterpriseId { get; set; }
}
