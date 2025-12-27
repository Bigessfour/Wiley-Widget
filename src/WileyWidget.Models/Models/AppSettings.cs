using System;

namespace WileyWidget.Models;

/// <summary>
/// Persisted user-facing settings. Contains only values that must survive restarts.
/// QBO (QuickBooks Online) tokens are stored to allow silent refresh on next launch.
/// Legacy QuickBooks* properties retained temporarily for migration; new canonical names use Qbo* prefix.
/// </summary>
/// <summary>
/// Represents a class for appsettings.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Primary key for the settings entity
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    // Theme + window geometry
    /// <summary>
    /// Gets or sets the theme.
    /// </summary>
    public string Theme { get; set; } = "FluentDark";
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool? WindowMaximized { get; set; }

    // Database settings
    /// <summary>
    /// Gets or sets the databaseserver.
    /// </summary>
    public string DatabaseServer { get; set; } = "localhost";
    /// <summary>
    /// Gets or sets the databasename.
    /// </summary>
    public string DatabaseName { get; set; } = "WileyWidget";

    // QuickBooks settings
    public string? QuickBooksCompanyFile { get; set; }
    /// <summary>
    /// Gets or sets the enablequickbookssync.
    /// </summary>
    public bool EnableQuickBooksSync { get; set; } = false;
    /// <summary>
    /// Gets or sets the syncintervalminutes.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 30;
    public string? QuickBooksRedirectUri { get; set; }

    // Application settings
    /// <summary>
    /// Gets or sets the enableautosave.
    /// </summary>
    public bool EnableAutoSave { get; set; } = true;
    /// <summary>
    /// Gets or sets the autosaveintervalminutes.
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    // Grid column preferences
    /// <summary>
    /// Gets or sets the usedynamiccolumns.
    /// </summary>
    public bool UseDynamicColumns { get; set; } = false;

    // Advanced settings
    /// <summary>
    /// Gets or sets the enabledatacaching.
    /// </summary>
    public bool EnableDataCaching { get; set; } = true;
    /// <summary>
    /// Gets or sets the cacheexpirationminutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;
    /// <summary>
    /// Gets or sets the selectedloglevel.
    /// </summary>
    public string SelectedLogLevel { get; set; } = "Information";
    /// <summary>
    /// Gets or sets the enablefilelogging.
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;
    /// <summary>
    /// Gets or sets the logfilepath.
    /// </summary>
    public string LogFilePath { get; set; } = "logs/wiley-widget.log";

    // Legacy QuickBooks token/property names (kept for one migration cycle)
    public string? QuickBooksAccessToken { get; set; }
    public string? QuickBooksRefreshToken { get; set; }
    public string? QuickBooksRealmId { get; set; }
    /// <summary>
    /// Gets or sets the quickbooksenvironment.
    /// </summary>
    public string QuickBooksEnvironment { get; set; } = "sandbox"; // or "production"
    public DateTime? QuickBooksTokenExpiresUtc { get; set; }

    // Canonical QBO properties going forward
    public string? QboAccessToken { get; set; }
    public string? QboRefreshToken { get; set; }
    /// <summary>
    /// Gets or sets the qbotokenexpiry.
    /// </summary>
    public DateTime QboTokenExpiry { get; set; } // UTC absolute expiry of access token

    /// <summary>
    /// Policy controlling how QuickBooks invoice amount conflicts are resolved.
    /// - PreferQBO (default): accept QBO change and update local.
    /// - KeepLocal: retain local amount, log conflict for review.
    /// - PromptUser: leave local unchanged and flag for manual resolution.
    /// </summary>
    public QuickBooksConflictPolicy QuickBooksConflictPolicy { get; set; } = QuickBooksConflictPolicy.PreferQBO;

    public string? QboClientId { get; set; }
    public string? QboClientSecret { get; set; }

    // AI settings
    /// <summary>
    /// Gets or sets the enableai.
    /// </summary>
    public bool EnableAI { get; set; } = false;
    public string? XaiApiKey { get; set; }
    /// <summary>
    /// Gets or sets the xaimodel.
    /// </summary>
    public string XaiModel { get; set; } = "grok-4-0709";
    /// <summary>
    /// Gets or sets the xaiapiendpoint.
    /// </summary>
    public string XaiApiEndpoint { get; set; } = "https://api.x.ai/v1";
    /// <summary>
    /// Gets or sets the xaitimeout.
    /// </summary>
    public int XaiTimeout { get; set; } = 30;
    /// <summary>
    /// Gets or sets the xaimaxtokens.
    /// </summary>
    public int XaiMaxTokens { get; set; } = 2000;
    /// <summary>
    /// Gets or sets the xaitemperature.
    /// </summary>
    public double XaiTemperature { get; set; } = 0.7;

    // Notification settings
    /// <summary>
    /// Gets or sets the enablenotifications.
    /// </summary>
    public bool EnableNotifications { get; set; } = true;
    /// <summary>
    /// Gets or sets the enablesounds.
    /// </summary>
    public bool EnableSounds { get; set; } = true;

    // Localization settings
    /// <summary>
    /// Gets or sets the defaultlanguage.
    /// </summary>
    public string DefaultLanguage { get; set; } = "en-US";
    /// <summary>
    /// Gets or sets the dateformat.
    /// </summary>
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    /// <summary>
    /// Gets or sets the currencyformat.
    /// </summary>
    public string CurrencyFormat { get; set; } = "USD";

    // Security settings
    /// <summary>
    /// Gets or sets the sessiontimeoutminutes.
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    // Fiscal year settings
    /// <summary>
    /// Gets or sets the fiscalyearstart.
    /// </summary>
    public string FiscalYearStart { get; set; } = "July 1";
    /// <summary>
    /// Gets or sets the fiscalyearstartmonth.
    /// </summary>
    public int FiscalYearStartMonth { get; set; } = 7; // July
    /// <summary>
    /// Gets or sets the fiscalyearstartday.
    /// </summary>
    public int FiscalYearStartDay { get; set; } = 1;
    /// <summary>
    /// Gets or sets the fiscalyearend.
    /// </summary>
    public string FiscalYearEnd { get; set; } = "June 30";
    /// <summary>
    /// Gets or sets the currentfiscalyear.
    /// </summary>
    public string CurrentFiscalYear { get; set; } = "2024-2025";
    /// <summary>
    /// Gets or sets the usefiscalyearforreporting.
    /// </summary>
    public bool UseFiscalYearForReporting { get; set; } = true;
    /// <summary>
    /// Gets or sets the fiscalquarter.
    /// </summary>
    public int FiscalQuarter { get; set; } = 1;
    /// <summary>
    /// Gets or sets the fiscalperiod.
    /// </summary>
    public string FiscalPeriod { get; set; } = "Q1";

    // Report settings
    public string? LastSelectedReportType { get; set; }
    public string? LastSelectedFormat { get; set; }
    public DateTime? LastReportStartDate { get; set; }
    public DateTime? LastReportEndDate { get; set; }
    /// <summary>
    /// Gets or sets the includechartsinreports.
    /// </summary>
    public bool IncludeChartsInReports { get; set; } = true;
    /// <summary>
    /// Gets or sets the lastselectedenterpriseid.
    /// </summary>
    public int LastSelectedEnterpriseId { get; set; }
}
