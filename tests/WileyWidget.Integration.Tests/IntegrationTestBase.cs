using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using Xunit;

namespace WileyWidget.Integration.Tests
{
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected IServiceProvider Services { get; private set; } = null!;
        protected AppDbContext DbContext { get; private set; } = null!;
        private IServiceScope? _scope;
        private readonly Action<IServiceCollection>? _configureServices;

        protected IntegrationTestBase(Action<IServiceCollection>? configureServices = null)
        {
            _configureServices = configureServices;
        }

        public async Task InitializeAsync()
        {
            var databaseName = Guid.NewGuid().ToString();

            // Start with the application's DI registration (skip includeDefaults so we can provide a test DB)
            var services = DependencyInjection.CreateServiceCollection(includeDefaults: false);

            // Provide a minimal IConfiguration for services that require it
            if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
            {
                var configDict = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                    ["Logging:LogLevel:Default"] = "Information",
                    ["XAI:ApiKey"] = "test-xai-key-01234567890123456789"
                };
                var json = System.Text.Json.JsonSerializer.Serialize(configDict);
                var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                var defaultConfig = new ConfigurationBuilder()
                    .AddJsonStream(ms)
                    .Build();
                services.AddSingleton<IConfiguration>(defaultConfig);
            }

            // Add InMemory database using a unique name for isolation across tests
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors());

            // Register DbContextOptions as singleton to match app registration and avoid
            // lifetime conflicts when creating an IDbContextFactory (factory is a singleton)
            services.AddSingleton(sp =>
            {
                var builder = new DbContextOptionsBuilder<AppDbContext>();
                builder.UseInMemoryDatabase(databaseName);
                return builder.Options;
            });

            // Add DbContextFactory for repositories - use same database name
            services.AddDbContextFactory<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(databaseName));

            // Ensure logging is available for services that request ILogger<T>
            services.AddLogging();
            // Register Microsoft health checks and add a typed database health check so HealthCheckService reports database health
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("Database");

            // Allow derived classes to configure additional services
            _configureServices?.Invoke(services);

            Services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

            // Create a scope and resolve the scoped AppDbContext from it.
            _scope = Services.CreateScope();
            DbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Ensure database is created
            await DbContext.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            if (_scope != null)
            {
                var db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                _scope.Dispose();
                _scope = null;
                DbContext = null!;
                Services = null!;
                return;
            }

            if (DbContext != null)
            {
                await DbContext.Database.EnsureDeletedAsync();
                await DbContext.DisposeAsync();
            }
        }

        protected IServiceScope CreateScope()
        {
            return Services.CreateScope();
        }

        [return: NotNull]
        protected T GetRequiredService<T>() where T : notnull
        {
            try
            {
                if (_scope != null)
                {
                    return _scope.ServiceProvider.GetRequiredService<T>();
                }

                using var scope = Services.CreateScope();
                return scope.ServiceProvider.GetRequiredService<T>();
            }
            catch (InvalidOperationException)
            {
                // Attempt to resolve a matching interface if a concrete type was requested (e.g., AccountsRepository -> IAccountsRepository)
                var requested = typeof(T);
                if (requested.IsInterface) throw;

                var interfaceName = "I" + requested.Name;
                // Search loaded assemblies for the matching interface name (some interfaces may be in different assemblies)
                var interfaceType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.FullName))
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.IsInterface && t.Name == interfaceName);

                // Try resolving the interface from existing scopes/providers before giving up
                if (interfaceType != null)
                {
                    object? svc = null;

                    if (_scope != null)
                    {
                        svc = _scope.ServiceProvider.GetService(interfaceType);
                        if (svc != null)
                        {
                            if (svc is T typed) return typed;
                            if (requested.IsAssignableFrom(svc.GetType())) return (T)svc;
                        }
                    }

                    // Try the root provider
                    svc = Services.GetService(interfaceType);
                    if (svc != null)
                    {
                        if (svc is T typed2) return typed2;
                        if (requested.IsAssignableFrom(svc.GetType())) return (T)svc;
                    }

                    // Try creating/resolving via a new scope
                    using var scope = Services.CreateScope();
                    var svc2 = scope.ServiceProvider.GetService(interfaceType);
                    if (svc2 != null)
                    {
                        if (svc2 is T typed3) return typed3;
                        if (requested.IsAssignableFrom(svc2.GetType())) return (T)svc2;
                    }
                }

                // As a last resort, attempt to create the concrete type using DI (will supply constructor dependencies)
                try
                {
                    using var scope = Services.CreateScope();
                    var instance = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.GetServiceOrCreateInstance<T>(scope.ServiceProvider);
                    if (instance != null) return instance;
                }
                catch
                {
                    // swallow and rethrow below
                }

                throw;
            }
        }

        protected virtual Task SeedTestDataAsync()
        {
            // Override in derived classes to seed specific data
            return Task.CompletedTask;
        }

        public async Task ResetDatabaseAsync()
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
    }
}
