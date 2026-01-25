using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    public static class WileyWidgetServicesExtensions
    {
        /// <summary>
        /// Registers core services moved from WinForms into the Services project.
        /// </summary>
        public static IServiceCollection AddWileyWidgetCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            // App-level event bus
            services.AddSingleton<IAppEventBus, AppEventBus>();

            // File import functionality
            services.AddTransient<IFileImportService, FileImportService>();

            // User preference persistence
            services.AddSingleton<UserPreferencesService>();

            return services;
        }
    }
}