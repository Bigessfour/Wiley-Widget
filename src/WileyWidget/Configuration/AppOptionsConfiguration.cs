using Microsoft.Extensions.Options;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace WileyWidget.Models;

/// <summary>
/// Configures AppOptions by loading from database and configuration
/// </summary>
public class AppOptionsConfigurator : IConfigureOptions<AppOptions>
{
    private readonly AppDbContext _dbContext;
    private readonly ISecretVaultService _secretVaultService;
    private readonly ILogger<AppOptionsConfigurator> _logger;
    private readonly IConfiguration _configuration;

    public AppOptionsConfigurator(
        AppDbContext dbContext,
        ISecretVaultService secretVaultService,
        ILogger<AppOptionsConfigurator> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _secretVaultService = secretVaultService ?? throw new ArgumentNullException(nameof(secretVaultService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Configure(AppOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        try
        {
            _logger.LogInformation("Configuring AppOptions from database and secrets");

            // Load from database settings (tolerate DB unavailability)
            try
            {
                // IMPORTANT: use projection to select only needed columns so queries succeed even if
                // additive columns (e.g., QboClientId/QboClientSecret) are not yet present.
                var s = _dbContext.AppSettings
                    // Do not assume Id = 1; pick the earliest row if present
                    .OrderBy(s => s.Id)
                    .Select(s => new { s.Theme, s.WindowWidth, s.WindowHeight, s.WindowMaximized })
                    .AsNoTracking()
                    .FirstOrDefault();

                if (s != null)
                {
                    options.Theme = s.Theme ?? options.Theme;
                    options.WindowWidth = (int)(s.WindowWidth ?? options.WindowWidth);
                    options.WindowHeight = (int)(s.WindowHeight ?? options.WindowHeight);
                    options.MaximizeOnStartup = s.WindowMaximized ?? options.MaximizeOnStartup;
                }
            }
            catch (InvalidOperationException ex)
            {
                // EF Core/DbContext not ready or misconfigured – fall back to configuration-based options
                _logger.LogWarning("Failed to load AppOptions from DB: {Message}. Using defaults.", ex.Message);
                // Populate from configuration as a safe fallback
                try
                {
                    _configuration.GetSection("App").Bind(options);
                }
                catch (Exception bindEx)
                {
                    _logger.LogWarning(bindEx, "Failed to bind AppOptions from configuration during DB fallback");
                }
            }
            catch (Exception dbEx)
            {
                // Any other DB/query issue – warn and fall back to configuration values to keep app running
                _logger.LogWarning(dbEx, "Failed to load AppSettings from database; using configuration-backed AppOptions");
                try
                {
                    _configuration.GetSection("App").Bind(options);
                }
                catch (Exception bindEx)
                {
                    _logger.LogWarning(bindEx, "Failed to bind AppOptions from configuration during DB fallback");
                }
            }

            // Load secrets deterministically (synchronously) to avoid timing races and unobserved exceptions
            try
            {
                // QuickBooks settings
                options.QuickBooksClientId = _secretVaultService.GetSecret("QuickBooks-ClientId") ?? options.QuickBooksClientId;
                options.QuickBooksClientSecret = _secretVaultService.GetSecret("QuickBooks-ClientSecret") ?? options.QuickBooksClientSecret;
                options.QuickBooksRedirectUri = _secretVaultService.GetSecret("QuickBooks-RedirectUri") ?? options.QuickBooksRedirectUri;
                options.QuickBooksEnvironment = _secretVaultService.GetSecret("QuickBooks-Environment") ?? options.QuickBooksEnvironment;

                // Syncfusion settings
                options.SyncfusionLicenseKey = _secretVaultService.GetSecret("Syncfusion-LicenseKey") ?? options.SyncfusionLicenseKey;

                // XAI settings
                options.XaiApiKey = _secretVaultService.GetSecret("XAI-ApiKey") ?? options.XaiApiKey;
                options.XaiBaseUrl = _secretVaultService.GetSecret("XAI-BaseUrl") ?? options.XaiBaseUrl;

                _logger.LogInformation("AppOptions secrets loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load secrets for AppOptions");
            }

            _logger.LogInformation("AppOptions configured successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure AppOptions");
            // Continue with default values
        }
    }
}

/// <summary>
/// Validates AppOptions configuration
/// </summary>
public class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var failures = new List<string>();

        // Validate window dimensions
        if (options.WindowWidth < 800 || options.WindowWidth > 3840)
            failures.Add("WindowWidth must be between 800 and 3840");

        if (options.WindowHeight < 600 || options.WindowHeight > 2160)
            failures.Add("WindowHeight must be between 600 and 2160");

        // Validate AI settings
        if (options.XaiTimeoutSeconds < 5 || options.XaiTimeoutSeconds > 300)
            failures.Add("XaiTimeoutSeconds must be between 5 and 300");

        if (options.Temperature < 0.0 || options.Temperature > 2.0)
            failures.Add("Temperature must be between 0.0 and 2.0");

        if (options.MaxTokens < 1 || options.MaxTokens > 4096)
            failures.Add("MaxTokens must be between 1 and 4096");

        if (options.ContextWindowSize < 1024 || options.ContextWindowSize > 32768)
            failures.Add("ContextWindowSize must be between 1024 and 32768");

        // Validate fiscal year settings
        if (options.FiscalYearStartMonth < 1 || options.FiscalYearStartMonth > 12)
            failures.Add("FiscalYearStartMonth must be between 1 and 12");

        if (options.FiscalYearStartDay < 1 || options.FiscalYearStartDay > 31)
            failures.Add("FiscalYearStartDay must be between 1 and 31");

        // Validate cache settings
        if (options.CacheExpirationMinutes < 5 || options.CacheExpirationMinutes > 1440)
            failures.Add("CacheExpirationMinutes must be between 5 and 1440");

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
