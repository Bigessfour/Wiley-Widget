using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget
{
    public static class AppDependencyInjection
    {
        public static void AddWileyServices(this IServiceCollection services)
        {
            // Add logging with Serilog
            services.AddLogging(builder =>
            {
                builder.AddSerilog();
            });

            // Register UI pages and viewmodels for DI
            services.AddTransient<Views.BudgetOverviewPage>();
            services.AddTransient<ViewModels.BudgetOverviewViewModel>();

            // Register memory cache and cache service
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();

            // Register backend data services
            services.AddScoped<IBudgetRepository, BudgetRepository>();
        }

        public static void AddWileyConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConfiguration>(configuration);

            // Register DbContextFactory
            var dbFactory = new AppDbContextFactory(configuration);
            services.AddSingleton<IDbContextFactory<AppDbContext>>(dbFactory);
        }
    }
}