using System;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Prism.Ioc;
using System.IO;
using DotNetEnv;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Small, focused bootstrapper to host future registration and hardening logic.
    /// Current responsibilities:
    /// - Provide single entrypoint for broader startup refactors
    /// - Log bootstrap lifecycle
    /// - (Future) move RegisterTypes content out of App.xaml.cs into this class
    /// </summary>
    public class Bootstrapper
    {
        private readonly string _startupId = Guid.NewGuid().ToString("N")[..8];

        public void Run()
        {
            Log.Information("Bootstrapper.Run invoked - Session: {StartupId}", _startupId);

            // No-op for now. In the larger refactor this will contain:
            // - CreateContainerExtension / Container setup
            // - RegisterTypes(IContainerRegistry) moved from App.xaml.cs
            // - Harden HttpClient (AddHttpClient + Polly policies)
            // - Harden DbContext registration with transient fault handling
            // Keeping this minimal reduces risk during incremental migration.
        }

        /// <summary>
        /// Perform early registration pieces that are safe to move out of App.xaml.cs:
        /// - Build IConfiguration
        /// - Register IConfiguration instance
        /// - Register ILoggerFactory + ILogger<> wiring
        /// Returns the built IConfiguration so callers can continue using it in their registration flow.
        /// </summary>
        public IConfiguration RegisterTypes(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null) throw new ArgumentNullException(nameof(containerRegistry));

            Log.Debug("Bootstrapper: starting RegisterTypes (configuration + logging)");

            var configuration = BuildConfiguration();
            containerRegistry.RegisterInstance<IConfiguration>(configuration);
            Log.Information("Bootstrapper: IConfiguration registered as singleton");

            // Register Serilog integration with Microsoft ILoggerFactory
#pragma warning disable CA2000
            SerilogLoggerFactory loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            containerRegistry.Register(typeof(ILogger<>), typeof(Microsoft.Extensions.Logging.Logger<>));
            Log.Information("Bootstrapper: ILoggerFactory and ILogger<> registered (Serilog)");

            // Register a default MemoryCache instance for early consumers
            var memoryCacheOptions = new MemoryCacheOptions();
#pragma warning disable CA2000 // MemoryCache registered as singleton; disposed when container disposes
            var memoryCache = new MemoryCache(memoryCacheOptions);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<IMemoryCache>(memoryCache);
            Log.Information("Bootstrapper: IMemoryCache registered");

            // Configure Microsoft DI services for HttpClient (Polly) and DbContext (with AuditInterceptor)
            // We create a small ServiceCollection, register the pieces, then expose the built IServiceProvider
            // to the Prism container so other parts of the app can consume IHttpClientFactory or create scopes.
            var services = new ServiceCollection();

            // Register HttpClient with Polly 8.x standard resilience handler
            var httpClientBuilder = services.AddHttpClient("WileyApiClient");
            WileyWidget.Configuration.Resilience.PolicyFactory.AddStandardResilienceHandler(httpClientBuilder, loggerFactory.CreateLogger("Resilience"));

            // Build and expose IServiceProvider so other parts of the app can consume IHttpClientFactory if needed
            var serviceProvider = services.BuildServiceProvider();
            containerRegistry.RegisterInstance<IServiceProvider>(serviceProvider);
            var httpFactory = serviceProvider.GetService<IHttpClientFactory>();
            if (httpFactory != null)
            {
                containerRegistry.RegisterInstance<IHttpClientFactory>(httpFactory);
            }

            Log.Information("Bootstrapper: Microsoft DI services for HttpClient registered (IServiceProvider and IHttpClientFactory)");

            return configuration;
        }

        private IConfiguration BuildConfiguration()
        {
            // Attempt to load .env from project root if present
            try
            {
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var assemblyParent = Directory.GetParent(assemblyDir);
                if (assemblyParent?.Parent?.FullName is string projectDir)
                {
                    var envPath = Path.Combine(projectDir, ".env");
                    if (File.Exists(envPath))
                    {
                        DotNetEnv.Env.Load(envPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Bootstrapper: failed to load .env (non-fatal)");
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            // Try to add user secrets if available (best-effort)
            try
            {
                builder.AddUserSecrets<Bootstrapper>(optional: true);
            }
            catch (Exception)
            {
                // swallow; user secrets not required for bootstrap
            }

            var configurationRoot = builder.Build();
            return configurationRoot;
        }

        // Note: Polly policies are centralized in Configuration/WpfHostingExtensions via a PolicyRegistry.
    }
}
