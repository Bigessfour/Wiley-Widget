using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Telemetry;
using WileyWidget.Services.Startup; // central resilience pipeline registration helpers
using WileyWidget.Services.Export;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Configure application services.
        /// </summary>
        /// <param name="configuration">Configuration instance</param>
        public static IServiceProvider ConfigureServices(IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var services = new ServiceCollection();

            // Register configuration instance
            services.AddSingleton(configuration);

            // Configure logging (Serilog) — ensure Serilog reads from configuration at startup
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Database registration: require configured connection string (production-only)
            var connectionString = configuration.GetConnectionString("WileyWidgetDb");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: new int[] { 4060, 18456 });
                    });

                    // Log EF Core "pending model changes" warnings instead of allowing stricter diagnostics to bubble as errors.
                    options.ConfigureWarnings(warnings =>
                    {
                        warnings.Log(RelationalEventId.PendingModelChangesWarning);
                    });

                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging(false);
                });

                // Also register the factory for scoped consumer scenarios
                services.AddDbContextFactory<AppDbContext>(options =>
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: new int[] { 4060, 18456 });
                    });

                    options.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning));
                });
            }
            else
            {
                // Enforce production-only behavior — require a valid connection string
                Log.Fatal("Missing or empty connection string 'WileyWidgetDb'. Application requires a SQL Server connection and will not start without one.");
                throw new InvalidOperationException("Missing connection string 'WileyWidgetDb' in configuration. Add a valid SQL Server connection string to appsettings.json.");
            }

            // Core Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            // Theme / Icon service for runtime icon lookups
            services.AddSingleton<WileyWidget.WinForms.Services.IThemeIconService, WileyWidget.WinForms.Services.ThemeIconService>();
            services.AddSingleton<HealthCheckService>();

            // Telemetry & diagnostics
            // Register a lightweight telemetry service (SigNoz fallback) and a central ErrorReportingService.
            services.AddSingleton<ITelemetryService, SigNozTelemetryService>();
            services.AddSingleton<ErrorReportingService>();

            // Register centralized resilience pipelines (HTTP: XAI, QuickBooks)
            services.AddWileyResiliencePolicies(configuration);

            // Ensure IHttpClientFactory is available for services that rely on named clients
            services.AddHttpClient();

            // Data Services
            services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // Feature Services
            services.AddSingleton<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IBoldReportService, BoldReportService>();
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddSingleton<IDiValidationService, DiValidationService>();

            // ViewModels
            services.AddTransient<ChartViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AccountsViewModel>();

            // Forms
            services.AddTransient<MainForm>();
            services.AddTransient<ChartForm>();
            services.AddTransient<SettingsForm>();
            services.AddTransient<AccountsForm>();

            return services.BuildServiceProvider();
        }
    }
}
