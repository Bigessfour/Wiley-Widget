using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Data.Repositories;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        public static IServiceProvider ConfigureServices()
        {
            var services = CreateServiceCollection();
            return services.BuildServiceProvider();
        }

        public static IServiceCollection CreateServiceCollection()
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(configure => configure.AddConsole().AddDebug());

            // Memory Cache for dashboard repository
            services.AddMemoryCache();

            // Ensure HttpClientFactory is available for services that need IHttpClientFactory
            services.AddHttpClient();

            // Core Services
            services.AddSingleton<ISettingsService, SettingsService>();
            // Also register concrete SettingsService so components depending on concrete type can resolve
            services.AddSingleton<SettingsService>(sp => (SettingsService)sp.GetService(typeof(ISettingsService))!);

            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            services.AddSingleton<HealthCheckService>();

            // Data Services
            services.AddSingleton<IQuickBooksService, QuickBooksService>();
            // QuickBooks API client implementation
            services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();

            // Database Context (factory so repositories can request IDbContextFactory)
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=wileywidget.db"));
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite("Data Source=wileywidget.db"));

            // Repositories
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<Business.Interfaces.IEnterpriseRepository, EnterpriseRepository>();
            services.AddScoped<Business.Interfaces.IMunicipalAccountRepository, MunicipalAccountRepository>();
            services.AddScoped<Business.Interfaces.IBudgetRepository, BudgetRepository>();

            // Error reporting and telemetry support
            services.AddSingleton<ErrorReportingService>();

            // Feature Services
            services.AddSingleton<IAIService, XAIService>();
            services.AddSingleton<IAILoggingService, AILoggingService>();
            services.AddSingleton<IAuditService, AuditService>();

            // Context service + audit repository needed for AI context building
            services.AddSingleton<IWileyWidgetContextService, WileyWidgetContextService>();
            services.AddScoped<Business.Interfaces.IAuditRepository, AuditRepository>();

            services.AddSingleton<IReportExportService, ReportExportService>();
            services.AddSingleton<IBoldReportService, BoldReportService>();
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            services.AddSingleton<IDiValidationService, DiValidationService>();

            // Dashboard Services
            services.AddScoped<IDashboardService, DashboardService>();

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

            return services;
        }
    }
}
