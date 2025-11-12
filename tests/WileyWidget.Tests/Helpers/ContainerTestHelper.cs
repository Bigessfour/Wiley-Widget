// ContainerTestHelper.cs - Test helper for building and validating DI container in tests
//
// Provides xUnit and .csx scripts with a reusable way to:
// - Build a full DI container with all production registrations
// - Validate service registrations
// - Assert specific dependencies are registered
// - Test service resolution without full app startup
//
// Usage in xUnit:
//   var container = ContainerTestHelper.BuildTestContainer();
//   var service = container.Resolve<IMyService>();
//   Assert.NotNull(service);
//
// Usage in .csx:
//   #r "path/to/WileyWidget.Tests.dll"
//   var helper = new ContainerTestHelper();
//   var container = helper.BuildTestContainer();

using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Prism.Ioc;
using Prism.Container.DryIoc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Xunit;

namespace WileyWidget.Tests.Helpers
{
    /// <summary>
    /// Helper class for building and testing DI containers in unit tests.
    /// Replicates App.xaml.cs registration logic without full WPF startup.
    /// </summary>
    public class ContainerTestHelper
    {
        /// <summary>
        /// Builds a test container with all production service registrations.
        /// Excludes WPF-specific registrations (Shell, Views) for headless testing.
        /// </summary>
        public static IContainerProvider BuildTestContainer(Action<IContainerRegistry>? additionalRegistrations = null)
        {
            // Initialize Serilog for test logging
            if (Log.Logger == null || Log.Logger.GetType().Name == "SilentLogger")
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .CreateLogger();
            }

            Log.Information("[TEST] Building test DI container...");

            // Create DryIoc container with production rules
            var rules = DryIoc.Rules.Default
                .WithMicrosoftDependencyInjectionRules()
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithDefaultReuse(Reuse.Singleton)
                .WithAutoConcreteTypeResolution()
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace)
                .WithFactorySelector(Rules.SelectLastRegisteredFactory())
                .WithoutThrowOnRegisteringDisposableTransient()
                .WithTrackingDisposableTransients()
                .WithFuncAndLazyWithoutRegistration();

            var dryIocContainer = new Container(rules);
            var containerExtension = new DryIocContainerExtension(dryIocContainer);

            // Register core infrastructure (mirrors App.DependencyInjection.cs)
            RegisterCoreInfrastructure(containerExtension);

            // Register repositories (scoped)
            RegisterRepositories(containerExtension);

            // Register business services (singleton)
            RegisterBusinessServices(containerExtension);

            // Register ViewModels (transient) - exclude for headless tests
            // RegisterViewModels(containerExtension);

            // Allow caller to add additional test-specific registrations
            additionalRegistrations?.Invoke(containerExtension);

            Log.Information("[TEST] Test container built successfully");

            return containerExtension;
        }

        /// <summary>
        /// Registers core infrastructure services (configuration, logging, caching, HTTP clients).
        /// </summary>
        private static void RegisterCoreInfrastructure(IContainerRegistry registry)
        {
            Log.Debug("[TEST] Registering core infrastructure...");

            // Configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            registry.RegisterInstance<IConfiguration>(configuration);

            // Memory cache
            var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 * 1024 * 1024 });
            registry.RegisterInstance<IMemoryCache>(memoryCache);

            // Logging (Serilog bridge)
            var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
            registry.RegisterInstance<ILoggerFactory>(loggerFactory);
            registry.Register(typeof(ILogger<>), typeof(Logger<>));

            Log.Debug("[TEST] Core infrastructure registered");
        }

        /// <summary>
        /// Registers repository interfaces with concrete implementations (scoped lifetime).
        /// </summary>
        private static void RegisterRepositories(IContainerRegistry registry)
        {
            Log.Debug("[TEST] Registering repositories...");

            // Note: Actual repository registration would require DbContext setup
            // For unit tests, consider registering mock repositories instead
            Log.Debug("[TEST] Repository registration skipped for unit tests (use mocks)");
        }

        /// <summary>
        /// Registers business service interfaces with concrete implementations (singleton lifetime).
        /// </summary>
        private static void RegisterBusinessServices(IContainerRegistry registry)
        {
            Log.Debug("[TEST] Registering business services...");

            // Register key services that tests commonly need
            try
            {
                registry.RegisterSingleton<WileyWidget.Services.ISettingsService, WileyWidget.Services.SettingsService>();
                registry.RegisterSingleton<WileyWidget.Services.ISecretVaultService, WileyWidget.Services.LocalSecretVaultService>();
                registry.RegisterSingleton<WileyWidget.Services.IAuditService, WileyWidget.Services.AuditService>();
                Log.Debug("[TEST] Key business services registered");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TEST] Failed to register some business services - tests may need to mock");
            }
        }

        /// <summary>
        /// Validates that a specific service is registered and resolvable.
        /// </summary>
        public static void AssertServiceRegistered<T>(IContainerProvider container) where T : class
        {
            var service = container.Resolve<T>();
            Assert.NotNull(service);
            Log.Debug("[TEST] ✓ {Service} is registered and resolvable", typeof(T).Name);
        }

        /// <summary>
        /// Validates that multiple services are registered and resolvable.
        /// </summary>
        public static void AssertServicesRegistered(IContainerProvider container, params Type[] serviceTypes)
        {
            foreach (var serviceType in serviceTypes)
            {
                var service = container.Resolve(serviceType);
                Assert.NotNull(service);
                Log.Debug("[TEST] ✓ {Service} is registered and resolvable", serviceType.Name);
            }
        }

        /// <summary>
        /// Gets all service registrations from the container for inspection.
        /// </summary>
        public static IEnumerable<ServiceRegistrationInfo> GetAllRegistrations(IContainerProvider container)
        {
            var dryIoc = (container as IContainerExtension<DryIoc.IContainer>)?.Instance;
            if (dryIoc == null)
                return Enumerable.Empty<ServiceRegistrationInfo>();

            return dryIoc.GetServiceRegistrations() ?? Enumerable.Empty<ServiceRegistrationInfo>();
        }

        /// <summary>
        /// Validates container health similar to production validation.
        /// Returns success rate percentage.
        /// </summary>
        public static double ValidateContainerHealth(IContainerProvider container, out List<string> failures)
        {
            failures = new List<string>();
            var dryIoc = (container as IContainerExtension<DryIoc.IContainer>)?.Instance;

            if (dryIoc == null)
            {
                failures.Add("DryIoc container not accessible");
                return 0.0;
            }

            var registrations = dryIoc.GetServiceRegistrations()?.ToList() ?? new List<ServiceRegistrationInfo>();
            if (registrations.Count == 0)
            {
                failures.Add("No registrations found");
                return 0.0;
            }

            int validated = 0;
            foreach (var reg in registrations.Where(r => r.ServiceType != null))
            {
                try
                {
                    var resolved = dryIoc.TryResolve(reg.ServiceType!, IfUnresolved.ReturnDefault);
                    if (resolved != null)
                        validated++;
                    else
                        failures.Add($"{reg.ServiceType!.Name}: resolved to null");
                }
                catch (Exception ex)
                {
                    failures.Add($"{reg.ServiceType!.Name}: {ex.Message}");
                }
            }

            return (double)validated / registrations.Count * 100.0;
        }
    }
}
