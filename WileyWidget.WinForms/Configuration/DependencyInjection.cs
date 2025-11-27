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
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(configure => configure.AddConsole().AddDebug());

            // Memory Cache for dashboard repository
            services.AddMemoryCache();

            // Core Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            services.AddSingleton<HealthCheckService>();

            // Data Services
            services.AddSingleton<IQuickBooksService, QuickBooksService>();

            // Database Context
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=wileywidget.db"));

            // Repositories
            services.AddScoped<IDashboardRepository, DashboardRepository>();

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

            return services.BuildServiceProvider();
        }
    }
}
