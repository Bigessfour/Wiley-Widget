using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace WileyWidget.Models;

/// <summary>
/// Strongly-typed configuration options for connection strings
/// </summary>
/// <summary>
/// Represents a class for connectionstringsoptions.
/// </summary>
public class ConnectionStringsOptions
{
    /// <summary>
    /// Default database connection string (required)
    /// </summary>
    [Required(ErrorMessage = "DefaultConnection is required")]
    [ConnectionStringValidation]
    /// <summary>
    /// Gets or sets the defaultconnection.
    /// </summary>
    public string DefaultConnection { get; set; } = string.Empty;

}

/// <summary>
/// Strongly-typed configuration options for QuickBooks integration
/// </summary>
/// <summary>
/// Represents a class for quickbooksoptions.
/// </summary>
public class QuickBooksOptions
{
    /// <summary>
    /// QuickBooks OAuth2 Client ID
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.ClientId is required for QuickBooks integration")]
    /// <summary>
    /// Gets or sets the clientid.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks OAuth2 Client Secret
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.ClientSecret is required for QuickBooks integration")]
    /// <summary>
    /// Gets or sets the clientsecret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks OAuth2 Redirect URI
    /// </summary>
    [Required(ErrorMessage = "QuickBooks.RedirectUri is required for QuickBooks integration")]
    [Url(ErrorMessage = "QuickBooks.RedirectUri must be a valid URL")]
    /// <summary>
    /// Gets or sets the redirecturi.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// QuickBooks environment (sandbox or production)
    /// </summary>
    [RegularExpression("^(sandbox|production)$", ErrorMessage = "QuickBooks.Environment must be either 'sandbox' or 'production'")]
    /// <summary>
    /// Gets or sets the environment.
    /// </summary>
    public string Environment { get; set; } = "sandbox";
}

/// <summary>
/// Custom validation attribute for connection strings
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
/// <summary>
/// Represents a class for connectionstringvalidationattribute.
/// </summary>
public class ConnectionStringValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Whether empty strings are allowed
    /// </summary>
    /// <summary>
    /// Gets or sets the allowempty.
    /// </summary>
    public bool AllowEmpty { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext == null) throw new ArgumentNullException(nameof(validationContext));

        var connectionString = value as string;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return AllowEmpty ? ValidationResult.Success : new ValidationResult($"{validationContext.DisplayName} cannot be empty");
        }

        // Basic validation for SQL Server connection string patterns
        var sqlServerPattern = @"^(Server|Data Source)=[^;]+;";

        if (!Regex.IsMatch(connectionString, sqlServerPattern, RegexOptions.IgnoreCase))
        {
            return new ValidationResult($"{validationContext.DisplayName} must be a valid SQL Server connection string");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Configuration options for database settings.
/// </summary>
/// <summary>
/// Represents a class for databaseoptions.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// Gets or sets the enablesensitivedatalogging.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;
    /// <summary>
    /// Gets or sets the enabledetailederrors.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    [System.ComponentModel.DataAnnotations.Range(10, 300, ErrorMessage = "CommandTimeout must be between 10 and 300 seconds")]
    /// <summary>
    /// Gets or sets the commandtimeout.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    [System.ComponentModel.DataAnnotations.Range(0, 10, ErrorMessage = "MaxRetryCount must be between 0 and 10")]
    /// <summary>
    /// Gets or sets the maxretrycount.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;
    /// <summary>
    /// Gets or sets the maxretrydelay.
    /// </summary>

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Application settings options loaded from configuration and database
/// </summary>
/// <summary>
/// Represents a class for appoptions.
/// </summary>
public class AppOptions
{
    // General Settings
    [Category("General")]
    [Display(Name = "Theme")]
    /// <summary>
    /// Gets or sets the theme.
    /// </summary>
    public string Theme { get; set; } = "FluentDark";

    [Category("General")]
    [Display(Name = "Window Width")]
    [Range(800, 3840, ErrorMessage = "Window width must be between 800 and 3840 pixels")]
    /// <summary>
    /// Gets or sets the windowwidth.
    /// </summary>
    public int WindowWidth { get; set; } = 1200;

    [Category("General")]
    [Display(Name = "Window Height")]
    [Range(600, 2160, ErrorMessage = "Window height must be between 600 and 2160 pixels")]
    /// <summary>
    /// Gets or sets the windowheight.
    /// </summary>
    public int WindowHeight { get; set; } = 800;

    [Category("General")]
    [Display(Name = "Maximize on Startup")]
    /// <summary>
    /// Gets or sets the maximizeonstartup.
    /// </summary>
    public bool MaximizeOnStartup { get; set; } = false;

    [Category("General")]
    [Display(Name = "Show Splash Screen")]
    /// <summary>
    /// Gets or sets the showsplashscreen.
    /// </summary>
    public bool ShowSplashScreen { get; set; } = true;

    // Database Settings
    [Category("Database")]
    [Display(Name = "Connection String")]
    /// <summary>
    /// Gets or sets the databaseconnectionstring.
    /// </summary>
    public string DatabaseConnectionString { get; set; } = string.Empty;

    // QuickBooks Settings
    [Category("QuickBooks")]
    [Display(Name = "Client ID")]
    /// <summary>
    /// Gets or sets the quickbooksclientid.
    /// </summary>
    public string QuickBooksClientId { get; set; } = string.Empty;

    [Category("QuickBooks")]
    [Display(Name = "Client Secret")]
    /// <summary>
    /// Gets or sets the quickbooksclientsecret.
    /// </summary>
    public string QuickBooksClientSecret { get; set; } = string.Empty;

    [Category("QuickBooks")]
    [Display(Name = "Redirect URI")]
    [Url(ErrorMessage = "Redirect URI must be a valid URL")]
    /// <summary>
    /// Gets or sets the quickbooksredirecturi.
    /// </summary>
    public string QuickBooksRedirectUri { get; set; } = string.Empty;

    [Category("QuickBooks")]
    [Display(Name = "Environment")]
    [RegularExpression("^(Sandbox|Production)$", ErrorMessage = "Environment must be either 'Sandbox' or 'Production'")]
    /// <summary>
    /// Gets or sets the quickbooksenvironment.
    /// </summary>
    public string QuickBooksEnvironment { get; set; } = "Sandbox";

    // XAI Settings
    [Category("AI")]
    [Display(Name = "API Key")]
    /// <summary>
    /// Gets or sets the xaiapikey.
    /// </summary>
    public string XaiApiKey { get; set; } = string.Empty;

    [Category("AI")]
    [Display(Name = "Base URL")]
    [Url(ErrorMessage = "Base URL must be a valid URL")]
    /// <summary>
    /// Gets or sets the xaibaseurl.
    /// </summary>
    public string XaiBaseUrl { get; set; } = "https://api.x.ai/v1/";

    [Category("AI")]
    [Display(Name = "Timeout (seconds)")]
    [Range(5, 300, ErrorMessage = "Timeout must be between 5 and 300 seconds")]
    /// <summary>
    /// Gets or sets the xaitimeoutseconds.
    /// </summary>
    public int XaiTimeoutSeconds { get; set; } = 15;

    [Category("AI")]
    [Display(Name = "Model")]
    /// <summary>
    /// Gets or sets the xaimodel.
    /// </summary>
    public string XaiModel { get; set; } = "grok-4-0709";

    [Category("AI")]
    [Display(Name = "Response Style")]
    /// <summary>
    /// Gets or sets the responsestyle.
    /// </summary>
    public string ResponseStyle { get; set; } = "Balanced";

    [Category("AI")]
    [Display(Name = "Personality")]
    /// <summary>
    /// Gets or sets the personality.
    /// </summary>
    public string Personality { get; set; } = "Professional";

    [Category("AI")]
    [Display(Name = "Context Window Size")]
    [Range(1024, 32768, ErrorMessage = "Context window size must be between 1024 and 32768 tokens")]
    /// <summary>
    /// Gets or sets the contextwindowsize.
    /// </summary>
    public int ContextWindowSize { get; set; } = 4096;

    [Category("AI")]
    [Display(Name = "Enable Safety Filters")]
    /// <summary>
    /// Gets or sets the enablesafetyfilters.
    /// </summary>
    public bool EnableSafetyFilters { get; set; } = true;

    [Category("AI")]
    [Display(Name = "Temperature")]
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0")]
    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    [Category("AI")]
    [Display(Name = "Max Tokens")]
    [Range(1, 4096, ErrorMessage = "Max tokens must be between 1 and 4096")]
    /// <summary>
    /// Gets or sets the maxtokens.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    [Category("AI")]
    [Display(Name = "Enable Streaming")]
    /// <summary>
    /// Gets or sets the enablestreaming.
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    // Fiscal Year Settings
    [Category("Fiscal Year")]
    [Display(Name = "Start Month")]
    [Range(1, 12, ErrorMessage = "Fiscal year start month must be between 1 and 12")]
    /// <summary>
    /// Gets or sets the fiscalyearstartmonth.
    /// </summary>
    public int FiscalYearStartMonth { get; set; } = 7;

    [Category("Fiscal Year")]
    [Display(Name = "Start Day")]
    [Range(1, 31, ErrorMessage = "Fiscal year start day must be between 1 and 31")]
    /// <summary>
    /// Gets or sets the fiscalyearstartday.
    /// </summary>
    public int FiscalYearStartDay { get; set; } = 1;

    // Advanced Settings
    [Category("Advanced")]
    [Display(Name = "Enable Dynamic Columns")]
    /// <summary>
    /// Gets or sets the enabledynamiccolumns.
    /// </summary>
    public bool EnableDynamicColumns { get; set; } = true;

    [Category("Advanced")]
    [Display(Name = "Enable Data Caching")]
    /// <summary>
    /// Gets or sets the enabledatacaching.
    /// </summary>
    public bool EnableDataCaching { get; set; } = true;

    [Category("Advanced")]
    [Display(Name = "Cache Expiration (minutes)")]
    [Range(5, 1440, ErrorMessage = "Cache expiration must be between 5 and 1440 minutes")]
    /// <summary>
    /// Gets or sets the cacheexpirationminutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;

    // Reporting and analytics thresholds
    [Category("Reporting")]
    [Display(Name = "Budget Variance High Threshold (%)")]
    [Range(-1000, 1000, ErrorMessage = "Variance threshold must be within -1000% to 1000%")]
    /// <summary>
    /// Gets or sets the budgetvariancehighthresholdpercent.
    /// </summary>
    public decimal BudgetVarianceHighThresholdPercent { get; set; } = 10.0m;

    [Category("Reporting")]
    [Display(Name = "Budget Variance Low Threshold (%)")]
    [Range(-1000, 1000, ErrorMessage = "Variance threshold must be within -1000% to 1000%")]
    /// <summary>
    /// Gets or sets the budgetvariancelowthresholdpercent.
    /// </summary>
    public decimal BudgetVarianceLowThresholdPercent { get; set; } = -5.0m;

    [Category("Reporting")]
    [Display(Name = "AI High Confidence Score")]
    [Range(0, 100, ErrorMessage = "Confidence must be between 0 and 100")]
    /// <summary>
    /// Gets or sets the aihighconfidence.
    /// </summary>
    public int AIHighConfidence { get; set; } = 85;

    [Category("Reporting")]
    [Display(Name = "AI Low Confidence Score")]
    [Range(0, 100, ErrorMessage = "Confidence must be between 0 and 100")]
    /// <summary>
    /// Gets or sets the ailowconfidence.
    /// </summary>
    public int AILowConfidence { get; set; } = 65;

    [Category("Caching")]
    [Display(Name = "Enterprise Data Cache (seconds)")]
    [Range(5, 600, ErrorMessage = "Cache seconds must be between 5 and 600")]
    /// <summary>
    /// Gets or sets the enterprisedatacacheseconds.
    /// </summary>
    public int EnterpriseDataCacheSeconds { get; set; } = 60;

    [Category("Advanced")]
    [Display(Name = "Log Level")]
    /// <summary>
    /// Gets or sets the loglevel.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    [Category("Advanced")]
    [Display(Name = "Enable File Logging")]
    /// <summary>
    /// Gets or sets the enablefilelogging.
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    [Category("Advanced")]
    [Display(Name = "Log File Path")]
    /// <summary>
    /// Gets or sets the logfilepath.
    /// </summary>
    public string LogFilePath { get; set; } = "logs/wiley-widget.log";

    // Computed Properties
    [Category("General")]
    [Display(Name = "Is Dark Mode")]
    public bool IsDarkMode => Theme?.Contains("Dark", StringComparison.OrdinalIgnoreCase) == true;
}
