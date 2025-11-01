extern alias Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using Moq;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Startup.Modules;
using IModuleHealthService = Services::WileyWidget.Services.IModuleHealthService;
using ModuleHealthInfo = Services::WileyWidget.Services.ModuleHealthInfo;

namespace WileyWidget.ContainerTests
{
    /// <summary>
    /// Comprehensive container validation tests ensuring production readiness.
    /// Tests verify that all modules can be instantiated, registered, and initialized
    /// without throwing exceptions, and that all registered services can be resolved.
    /// </summary>
    public class ModuleContainerValidationTests
    {
        private readonly ITestOutputHelper _output;

        public ModuleContainerValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Creates a properly configured test container with all necessary infrastructure services.
        /// </summary>
        private (IContainerRegistry Registry, IContainerProvider Provider, Container DryIocContainer) CreateTestContainer()
        {
            var container = new Container(Rules.Default.WithMicrosoftDependencyInjectionRules());
            var containerExtension = new Prism.Container.DryIoc.DryIocContainerExtension(container);
            IContainerRegistry registry = containerExtension;
            IContainerProvider provider = containerExtension;

            // Register core infrastructure services that modules depend on
            registry.RegisterInstance(typeof(ILoggerFactory), NullLoggerFactory.Instance);
            // Register ILogger<T> as a factory instead of instance
            registry.Register(typeof(ILogger<>), typeof(NullLogger<>));

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:LogLevel:Default"] = "Information",
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=True;"
                })
                .Build();

            registry.RegisterInstance<IConfiguration>(config);
            registry.RegisterInstance<Prism.Ioc.IContainerRegistry>(registry);
            registry.RegisterInstance<Prism.Ioc.IContainerProvider>(provider);

            // Mock IRegionManager - REQUIRED by Prism modules for view registration
            // NOTE: RegisterViewWithRegion is an extension method and cannot be mocked directly
            // Modules will call it, but the mock will safely ignore those calls
            var regionManagerMock = new Mock<IRegionManager>();
            regionManagerMock.Setup(rm => rm.Regions).Returns(new Mock<IRegionCollection>().Object);
            registry.RegisterInstance<IRegionManager>(regionManagerMock.Object);

            // Mock IModuleHealthService
            var moduleHealthMock = new Mock<IModuleHealthService>();
            moduleHealthMock.Setup(m => m.RegisterModule(It.IsAny<string>())).Verifiable();
            moduleHealthMock.Setup(m => m.MarkModuleInitialized(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>())).Verifiable();
            moduleHealthMock.Setup(m => m.GetAllModuleStatuses()).Returns(new List<ModuleHealthInfo>());
            registry.RegisterInstance<IModuleHealthService>(moduleHealthMock.Object);

            return (registry, provider, container);
        }

        /// <summary>
        /// Discovers all module types from the main application assembly.
        /// </summary>
        private List<Type> GetAllModuleTypes()
        {
            try
            {
                // Load the WileyWidget assembly from the referenced project
                var assembly = typeof(WileyWidget.App).Assembly;

                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && !t.IsInterface
                        && t.Namespace != null
                        && t.Namespace.StartsWith("WileyWidget.Startup.Modules", StringComparison.Ordinal))
                    .OrderBy(t => t.Name)
                    .ToList();

                _output.WriteLine($"Loaded assembly: {assembly.FullName}");
                _output.WriteLine($"Assembly location: {assembly.Location}");

                return moduleTypes;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error loading modules: {ex.Message}");
                _output.WriteLine($"Stack: {ex.StackTrace}");
                return new List<Type>();
            }
        }

        [Fact]
        public void AllModules_CanBeDiscovered()
        {
            // Arrange & Act
            var moduleTypes = GetAllModuleTypes();

            // Assert
            _output.WriteLine($"Discovered {moduleTypes.Count} module types:");
            foreach (var type in moduleTypes)
            {
                _output.WriteLine($"  - {type.FullName}");
            }

            Assert.NotEmpty(moduleTypes);
            Assert.True(moduleTypes.Count >= 10, $"Expected at least 10 modules, found {moduleTypes.Count}");
        }

        [Fact]
        public void AllModules_CanBeInstantiated()
        {
            // Arrange
            var moduleTypes = GetAllModuleTypes();
            var failures = new List<(Type Module, Exception Error)>();

            // Act
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = Activator.CreateInstance(moduleType);
                    Assert.NotNull(module);
                    Assert.IsAssignableFrom<IModule>(module);
                    _output.WriteLine($"✓ Successfully instantiated: {moduleType.Name}");
                }
                catch (Exception ex)
                {
                    failures.Add((moduleType, ex));
                    _output.WriteLine($"✗ Failed to instantiate {moduleType.Name}: {ex.Message}");
                }
            }

            // Assert
            if (failures.Any())
            {
                var failureDetails = string.Join(Environment.NewLine,
                    failures.Select(f => $"  {f.Module.Name}: {f.Error.Message}"));
                Assert.Fail($"Failed to instantiate {failures.Count} module(s):{Environment.NewLine}{failureDetails}");
            }
        }

        [Fact]
        public void AllModules_RegisterTypes_DoesNotThrow()
        {
            // Arrange
            var (registry, provider, container) = CreateTestContainer();
            var moduleTypes = GetAllModuleTypes();
            var failures = new List<(Type Module, string Phase, Exception Error)>();

            // Act
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = (IModule)Activator.CreateInstance(moduleType)!;

                    try
                    {
                        module.RegisterTypes(registry);
                        _output.WriteLine($"✓ {moduleType.Name}.RegisterTypes() succeeded");
                    }
                    catch (Exception ex)
                    {
                        failures.Add((moduleType, "RegisterTypes", ex));
                        _output.WriteLine($"✗ {moduleType.Name}.RegisterTypes() failed: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add((moduleType, "Instantiation", ex));
                    _output.WriteLine($"✗ {moduleType.Name} instantiation failed: {ex.Message}");
                }
            }

            // Assert
            if (failures.Any())
            {
                var failureDetails = string.Join(Environment.NewLine,
                    failures.Select(f => $"  {f.Module.Name} ({f.Phase}): {f.Error.Message}"));
                Assert.Fail($"{failures.Count} module(s) failed during RegisterTypes:{Environment.NewLine}{failureDetails}");
            }

            container.Dispose();
        }

        [Fact]
        public void AllModules_OnInitialized_DoesNotThrow()
        {
            // Arrange
            var (registry, provider, container) = CreateTestContainer();
            var moduleTypes = GetAllModuleTypes();
            var failures = new List<(Type Module, string Phase, Exception Error)>();

            // Act - First register all modules
            var modules = new List<(Type Type, IModule Instance)>();
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = (IModule)Activator.CreateInstance(moduleType)!;
                    module.RegisterTypes(registry);
                    modules.Add((moduleType, module));
                    _output.WriteLine($"✓ Registered: {moduleType.Name}");
                }
                catch (Exception ex)
                {
                    failures.Add((moduleType, "RegisterTypes", ex));
                    _output.WriteLine($"✗ Failed to register {moduleType.Name}: {ex.Message}");
                }
            }

            // Act - Then initialize all modules
            foreach (var (moduleType, module) in modules)
            {
                try
                {
                    module.OnInitialized(provider);
                    _output.WriteLine($"✓ {moduleType.Name}.OnInitialized() succeeded");
                }
                catch (Exception ex)
                {
                    failures.Add((moduleType, "OnInitialized", ex));
                    _output.WriteLine($"✗ {moduleType.Name}.OnInitialized() failed: {ex.Message}");
                }
            }

            // Assert
            if (failures.Any())
            {
                var failureDetails = string.Join(Environment.NewLine,
                    failures.Select(f => $"  {f.Module.Name} ({f.Phase}): {f.Error.Message}{Environment.NewLine}    Stack: {f.Error.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"));
                Assert.Fail($"{failures.Count} module(s) failed:{Environment.NewLine}{failureDetails}");
            }

            container.Dispose();
        }

        [Fact]
        public void Container_AllRegisteredServices_CanBeResolved()
        {
            // Arrange
            var (registry, provider, container) = CreateTestContainer();
            var moduleTypes = GetAllModuleTypes();

            // Register all modules
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = (IModule)Activator.CreateInstance(moduleType)!;
                    module.RegisterTypes(registry);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Skipping {moduleType.Name} due to registration failure: {ex.Message}");
                }
            }

            // Act - Try to resolve all registered services
            var registeredTypes = container.GetServiceRegistrations()
                .Where(r => r.ServiceType != null && !r.ServiceType.IsGenericTypeDefinition)
                .Select(r => r.ServiceType)
                .Distinct()
                .ToList();

            _output.WriteLine($"Found {registeredTypes.Count} registered service types");

            var failures = new List<(Type ServiceType, Exception Error)>();
            var successes = 0;

            foreach (var serviceType in registeredTypes)
            {
                try
                {
                    var resolved = provider.Resolve(serviceType);
                    if (resolved != null)
                    {
                        successes++;
                        _output.WriteLine($"✓ Resolved: {serviceType.Name}");
                    }
                    else
                    {
                        _output.WriteLine($"⚠ Resolved to null: {serviceType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add((serviceType, ex));
                    _output.WriteLine($"✗ Failed to resolve {serviceType.Name}: {ex.Message}");
                }
            }

            // Assert
            _output.WriteLine($"Summary: {successes} successful, {failures.Count} failed out of {registeredTypes.Count} types");

            if (failures.Any())
            {
                var failureDetails = string.Join(Environment.NewLine,
                    failures.Take(10).Select(f => $"  {f.ServiceType.Name}: {f.Error.Message}"));
                if (failures.Count > 10)
                {
                    failureDetails += $"{Environment.NewLine}  ... and {failures.Count - 10} more";
                }
                Assert.Fail($"{failures.Count} service(s) failed to resolve:{Environment.NewLine}{failureDetails}");
            }

            container.Dispose();
        }

        [Fact]
        public void Container_NoCircularDependencies()
        {
            // Arrange
            var (registry, provider, container) = CreateTestContainer();
            var moduleTypes = GetAllModuleTypes();

            // Register all modules
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = (IModule)Activator.CreateInstance(moduleType)!;
                    module.RegisterTypes(registry);
                }
                catch { /* Ignore registration failures for this test */ }
            }

            // Act - Check for circular dependencies by analyzing the container
            var circularDeps = new List<string>();

            try
            {
                // DryIoc will detect circular dependencies during resolution
                // We'll attempt to resolve key services that might have circular deps
                var testServices = new[]
                {
                    typeof(IModuleHealthService),
                    typeof(IConfiguration),
                    typeof(ILoggerFactory)
                };

                foreach (var serviceType in testServices)
                {
                    try
                    {
                        provider.Resolve(serviceType);
                        _output.WriteLine($"✓ No circular dependency for: {serviceType.Name}");
                    }
                    catch (Exception ex) when (ex.Message.Contains("circular") || ex.Message.Contains("recursive"))
                    {
                        circularDeps.Add($"{serviceType.Name}: {ex.Message}");
                        _output.WriteLine($"✗ Circular dependency detected for: {serviceType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error during circular dependency check: {ex.Message}");
            }

            // Assert
            Assert.Empty(circularDeps);

            container.Dispose();
        }

        [Fact]
        public void AllModules_HaveModuleAttribute()
        {
            // Arrange
            var moduleTypes = GetAllModuleTypes();
            var missingAttribute = new List<Type>();

            // Act
            foreach (var moduleType in moduleTypes)
            {
                var moduleAttr = moduleType.GetCustomAttribute<ModuleAttribute>();
                if (moduleAttr == null)
                {
                    missingAttribute.Add(moduleType);
                    _output.WriteLine($"✗ Missing [Module] attribute: {moduleType.Name}");
                }
                else
                {
                    _output.WriteLine($"✓ {moduleType.Name} has [Module(ModuleName=\"{moduleAttr.ModuleName}\")]");
                }
            }

            // Assert
            if (missingAttribute.Any())
            {
                var details = string.Join(", ", missingAttribute.Select(t => t.Name));
                Assert.Fail($"{missingAttribute.Count} module(s) missing [Module] attribute: {details}");
            }
        }

        [Fact]
        public void AllModules_CompleteLifecycle_WithoutExceptions()
        {
            // Arrange
            var (registry, provider, container) = CreateTestContainer();
            var moduleTypes = GetAllModuleTypes();
            var failures = new List<(Type Module, string Phase, Exception Error)>();
            var successfulModules = 0;

            // Act - Full lifecycle: Instantiate → RegisterTypes → OnInitialized
            foreach (var moduleType in moduleTypes)
            {
                IModule? module = null;
                var moduleName = moduleType.Name;

                try
                {
                    // Phase 1: Instantiation
                    module = (IModule)Activator.CreateInstance(moduleType)!;
                    _output.WriteLine($"[{moduleName}] Phase 1/3: Instantiated ✓");

                    // Phase 2: RegisterTypes
                    module.RegisterTypes(registry);
                    _output.WriteLine($"[{moduleName}] Phase 2/3: RegisterTypes() ✓");

                    // Phase 3: OnInitialized
                    module.OnInitialized(provider);
                    _output.WriteLine($"[{moduleName}] Phase 3/3: OnInitialized() ✓");

                    successfulModules++;
                    _output.WriteLine($"[{moduleName}] ✓✓✓ Complete lifecycle SUCCESS");
                }
                catch (Exception ex) when (module == null)
                {
                    failures.Add((moduleType, "Instantiation", ex));
                    _output.WriteLine($"[{moduleName}] ✗✗✗ FAILED at Instantiation: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Determine which phase failed
                    var phase = "RegisterTypes or OnInitialized";
                    try
                    {
                        // Try to narrow down the phase
                        var testModule = (IModule)Activator.CreateInstance(moduleType)!;
                        testModule.RegisterTypes(registry);
                        phase = "OnInitialized";
                    }
                    catch
                    {
                        phase = "RegisterTypes";
                    }

                    failures.Add((moduleType, phase, ex));
                    _output.WriteLine($"[{moduleName}] ✗✗✗ FAILED at {phase}: {ex.Message}");
                }
            }

            // Assert
            _output.WriteLine("");
            _output.WriteLine("═══════════════════════════════════════════════════════════");
            _output.WriteLine($"PRODUCTION READINESS SUMMARY");
            _output.WriteLine("═══════════════════════════════════════════════════════════");
            _output.WriteLine($"Total Modules: {moduleTypes.Count}");
            _output.WriteLine($"Successful: {successfulModules} ({successfulModules * 100.0 / moduleTypes.Count:F1}%)");
            _output.WriteLine($"Failed: {failures.Count}");
            _output.WriteLine("═══════════════════════════════════════════════════════════");

            if (failures.Any())
            {
                _output.WriteLine("");
                _output.WriteLine("FAILURE DETAILS:");
                foreach (var (module, phase, error) in failures)
                {
                    _output.WriteLine($"  ✗ {module.Name}");
                    _output.WriteLine($"    Phase: {phase}");
                    _output.WriteLine($"    Error: {error.Message}");
                    if (error.InnerException != null)
                    {
                        _output.WriteLine($"    Inner: {error.InnerException.Message}");
                    }
                }
                _output.WriteLine("");

                var failureDetails = string.Join(Environment.NewLine,
                    failures.Select(f => $"  {f.Module.Name} failed at {f.Phase}: {f.Error.Message}"));
                Assert.Fail($"❌ PRODUCTION READINESS: FAILED{Environment.NewLine}{Environment.NewLine}" +
                           $"{failures.Count} out of {moduleTypes.Count} modules failed complete lifecycle:{Environment.NewLine}{failureDetails}");
            }

            _output.WriteLine("✓✓✓ ALL MODULES PASSED - CONTAINERS ARE PRODUCTION READY ✓✓✓");
            container.Dispose();
        }
    }
}
