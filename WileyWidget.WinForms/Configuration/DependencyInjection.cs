using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // === CRITICAL INFRASTRUCTURE (MUST BE FIRST) ===

            // HTTP Client Factory
            services.AddHttpClient();

            // Memory Cache
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();

            // DbContext (SCOPED - NOT SINGLETON! DbContext is NOT thread-safe)
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");

            // Validate and expand connection string
            // If the connection string contains environment variable placeholders (${VAR_NAME}),
            // attempt to resolve them from the environment
            connectionString = ExpandConnectionStringVariables(connectionString, configuration);

            // Validate connection string format before passing to Entity Framework
            ValidateConnectionString(connectionString);

            // Output connection string info to debug output
            System.Diagnostics.Debug.WriteLine("✓ Connection string validated successfully");
            System.Diagnostics.Debug.WriteLine($"  Server: {ExtractConnectionStringPart(connectionString, "Server") ?? ExtractConnectionStringPart(connectionString, "Data Source") ?? "(unknown)"}");
            System.Diagnostics.Debug.WriteLine($"  Database: {ExtractConnectionStringPart(connectionString, "Database") ?? ExtractConnectionStringPart(connectionString, "Initial Catalog") ?? "(unknown)"}");

            // IMPORTANT: Use ONLY AddDbContextFactory without AddDbContext.
            // Repositories will use the factory to create contexts on-demand.
            // This avoids the Singleton DbContextFactory -> Scoped DbContextOptions violation.
            // NOTE: Factory itself is registered as SINGLETON (thread-safe, reusable).
            // Only the DbContext instances it creates are scoped (lifetime managed by consumers).
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging(configuration.GetValue<bool>("DB:EnableSensitiveDataLogging", false));
                options.EnableDetailedErrors(configuration.GetValue<bool>("DB:EnableDetailedErrors", true));
            }); // Default: Singleton factory (thread-safe)

            // === CORE SERVICES ===
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<SettingsService>(sp => (SettingsService)sp.GetRequiredService<ISettingsService>());
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();

            // Configure HealthCheckConfiguration from appsettings.json using Options pattern
            // This follows Microsoft-recommended best practices for configuration management:
            // https://learn.microsoft.com/en-us/dotnet/core/extensions/options
            services.AddOptions<HealthCheckConfiguration>()
                .Bind(configuration.GetSection("HealthChecks"))
                .ValidateOnStart();

            services.AddSingleton<HealthCheckService>();

            // === DATA SERVICES ===
            services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // === REPOSITORIES (SCOPED - aligned with DbContext pattern) ===
            // These repositories use IDbContextFactory<AppDbContext> to create context instances
            // on-demand, allowing proper scope isolation and preventing "tracked by another instance" errors.
            // Scoped lifetime ensures each dialog/form request gets consistent data access.
            services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<IAuditRepository, AuditRepository>();
            services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();

            // === FEATURE SERVICES ===
            // XAIService needs DbContextFactory, so it cannot be Singleton.
            // Changed to Scoped to allow proper DI chain resolution.
            services.AddScoped<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();

            // Register both Excel export implementations for flexibility
            // ClosedXmlExportService and ExcelExportService both implement IExcelExportService
            // Default is ExcelExportService above; applications can override as needed
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddSingleton<IDiValidationService, DiValidationService>();

            // === VIEWMODELS (SCOPED to match DbContext lifetime) ===
            // Using Scoped instead of Transient ensures ViewModels share the same DbContext instance
            // within a dialog/form lifetime, preventing "tracked by another instance" EF Core errors
            services.AddScoped<MainViewModel>();
            services.AddScoped<ChartViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<AccountsViewModel>();
            services.AddScoped<BudgetOverviewViewModel>();

            // === FORMS (SCOPED to get fresh instances per dialog) ===
            services.AddScoped<MainForm>();
            services.AddScoped<ChartForm>();
            services.AddScoped<SettingsForm>();
            services.AddScoped<AccountsForm>();
        }

        /// <summary>
        /// Expands environment variable placeholders in connection strings.
        /// Supports ${VAR_NAME} syntax. If expansion fails, returns the original string.
        /// </summary>
        private static string ExpandConnectionStringVariables(string connectionString, IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            // Check for environment variable placeholder pattern: ${VAR_NAME}
            if (!connectionString.Contains("${", StringComparison.Ordinal))
                return connectionString;

            var expanded = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"\$\{(\w+)\}",
                match =>
                {
                    var varName = match.Groups[1].Value;
                    var value = Environment.GetEnvironmentVariable(varName);
                    return string.IsNullOrEmpty(value) ? match.Value : value;
                });

            return expanded;
        }

        /// <summary>
        /// Validates that a connection string is properly formatted and not just a placeholder.
        /// Throws InvalidOperationException if validation fails.
        /// </summary>
        private static void ValidateConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string is empty or whitespace.");

            // Reject unexpanded placeholder variables
            if (connectionString.Contains("${", StringComparison.Ordinal) || connectionString.Contains("}", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Connection string contains unexpanded environment variable placeholders: {connectionString}. " +
                    "Set the corresponding environment variables (e.g., DB_CONNECTION_STRING) before running the application.");

            // Basic validation: connection strings should contain key parts
            // For SQL Server: should have 'Server' or 'Data Source'
            var lowerCs = connectionString.ToLowerInvariant();
            if (!lowerCs.Contains("server", StringComparison.Ordinal) && !lowerCs.Contains("data source", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Connection string does not appear to be a valid SQL Server connection string. " +
                    "It should contain 'Server' or 'Data Source' parameter.");
        }

        /// <summary>
        /// Extracts a specific key's value from a connection string
        /// </summary>
        private static string? ExtractConnectionStringPart(string connectionString, string key)
        {
            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(key))
                return null;

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kvp = part.Split('=');
                if (kvp.Length == 2 && kvp[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp[1].Trim();
                }
            }

            return null;
        }
    }
}
