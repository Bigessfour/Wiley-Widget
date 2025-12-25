using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.Integration.Tests.Shared
{
    /// <summary>
    /// Base class for integration tests that need a DI container and an isolated in-memory database.
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IServiceScope TestScope;
        protected readonly string DatabaseName;

        public IntegrationTestBase(Action<IServiceCollection>? configureServices = null)
        {
            DatabaseName = "IntegrationTest_" + Guid.NewGuid().ToString("N");

            // Start from the production service registrations (minus default DB when includeDefaults=false)
            var services = DependencyInjection.CreateServiceCollection(includeDefaults: false);

            // Ensure minimal IConfiguration is present for singletons that require configuration
            if (!services.Any(sd => sd.ServiceType == typeof(Microsoft.Extensions.Configuration.IConfiguration)))
            {
                // Provide a minimal test configuration including XAI API key to avoid constructor throws in test DI
                var configDict = new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                    ["Logging:LogLevel:Default"] = "Information",
                    ["XAI:ApiKey"] = "test-xai-key-01234567890123456789"
                };
                var json = System.Text.Json.JsonSerializer.Serialize(configDict);
                var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                var defaultConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonStream(ms)
                    .Build();
                services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(defaultConfig);
            }

            // Register a unique in-memory database per test instance
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(DatabaseName));

            // Register DbContextOptions as singleton to avoid lifetime conflicts when also registering a DbContextFactory (factory is singleton)
            services.AddSingleton(sp =>
            {
                var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>();
                builder.UseInMemoryDatabase(DatabaseName);
                return builder.Options;
            });

            services.AddDbContextFactory<AppDbContext>((sp, options) => options.UseInMemoryDatabase(DatabaseName));

            // Allow test-specific configuration (mocks, overrides)
            configureServices?.Invoke(services);

            ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

            // Create a per-test scope so scoped services can be resolved safely and live for the duration of the test
            TestScope = ServiceProvider.CreateScope();

            // Ensure DB is created using the test scope's DbContext
            var ctx = TestScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Database.EnsureCreated();
        }

        protected T GetRequiredService<T>() where T : notnull => TestScope.ServiceProvider.GetRequiredService<T>();

        public IServiceScope CreateScope() => ServiceProvider.CreateScope();

        public async Task ResetDatabaseAsync()
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            try
            {
                if (TestScope is IDisposable ts)
                    ts.Dispose();
            }
            catch
            {
                // Swallow exceptions during disposal in test cleanup
            }

            if (ServiceProvider is IDisposable d)
                d.Dispose();
        }
    }
}
