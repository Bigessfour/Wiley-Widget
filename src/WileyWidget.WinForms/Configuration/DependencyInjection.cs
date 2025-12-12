using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.Services.Telemetry;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        public static ServiceCollection CreateServiceCollection()
        {
            var services = new ServiceCollection();
            ConfigureServicesInternal(services);
            return services;
        }

        public static IServiceProvider ConfigureServices()
        {
            var services = CreateServiceCollection();
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        }

        private static void ConfigureServicesInternal(IServiceCollection services)
        {
            // Cross-cutting HTTP client factory for services
            services.AddHttpClient();

            // Core Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => ServiceProviderServiceExtensions.GetRequiredService<SettingsService>(sp));
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            services.AddSingleton<HealthCheckService>();
            services.AddSingleton<ErrorReportingService>();

            // Data Services
            services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            services.AddSingleton<IQuickBooksService, QuickBooksService>();
            services.AddTransient<IDashboardService, DashboardService>();

            // Feature Services
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();
            services.AddScoped<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddScoped<IAuditRepository, AuditRepository>();
            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IBoldReportService, BoldReportService>();
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddSingleton<IDiValidationService, DiValidationService>();
            services.AddScoped<WileyWidget.Business.Interfaces.IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<WileyWidget.Business.Interfaces.IEnterpriseRepository, EnterpriseRepository>();

            // Infrastructure
            services.AddMemoryCache();
            // services.AddDbContextFactory<WileyWidget.Data.AppDbContext>(); // Moved to Program.cs for proper configuration
            services.AddSingleton<ITelemetryService, SigNozTelemetryService>();

            // Repositories
            services.AddScoped<IBudgetRepository, BudgetRepository>();
            services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();

            // ViewModels
            services.AddTransient<ChartViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AccountsViewModel>();
            services.AddTransient<DashboardViewModel>();

            // Forms
            services.AddTransient<MainForm>();
            services.AddTransient<ChartForm>();
            services.AddTransient<SettingsForm>();
            services.AddTransient<AccountsForm>();
            services.AddTransient<DashboardForm>();
        }
    }
}
