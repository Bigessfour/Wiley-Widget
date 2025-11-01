using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace WileyWidget.ContainerTests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Module container validation runner starting...");

            try
            {
                var container = new DryIoc.Container(Rules.Default.WithMicrosoftDependencyInjectionRules());
                var containerExtension = new Prism.Container.DryIoc.DryIocContainerExtension(container);
                IContainerRegistry registry = containerExtension;
                IContainerProvider provider = containerExtension;

                // Minimal registrations
                registry.RegisterInstance(typeof(ILoggerFactory), NullLoggerFactory.Instance);
                registry.RegisterInstance(typeof(IConfiguration), new ConfigurationBuilder().AddInMemoryCollection().Build());
                registry.RegisterInstance(typeof(Prism.Ioc.IContainerRegistry), registry);
                registry.RegisterInstance(typeof(Prism.Ioc.IContainerProvider), provider);

                var mhMock = new Mock<WileyWidget.Services.IModuleHealthService>();
                registry.RegisterInstance<WileyWidget.Services.IModuleHealthService>(mhMock.Object);

                var regionManagerMock = new Mock<Prism.Regions.IRegionManager>();
                registry.RegisterInstance<Prism.Regions.IRegionManager>(regionManagerMock.Object);

                var moduleTypes = Assembly.Load("WileyWidget").GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t) && t.Namespace != null && t.Namespace.StartsWith("WileyWidget.Startup.Modules"))
                    .ToList();

                var failures = new List<(Type Module, Exception Error)>();

                foreach (var mt in moduleTypes)
                {
                    Console.WriteLine($"Checking module: {mt.FullName}");
                    try
                    {
                        var module = (IModule)Activator.CreateInstance(mt)!;
                        try
                        {
                            module.RegisterTypes(registry);
                        }
                        catch (Exception ex)
                        {
                            failures.Add((mt, new Exception($"RegisterTypes failed: {ex.Message}", ex)));
                            continue;
                        }

                        try
                        {
                            module.OnInitialized(provider);
                        }
                        catch (Exception ex)
                        {
                            failures.Add((mt, new Exception($"OnInitialized failed: {ex.Message}", ex)));
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add((mt, new Exception($"Instantiation failed: {ex.Message}", ex)));
                    }
                }

                if (failures.Count == 0)
                {
                    Console.WriteLine("All modules registered and initialized successfully.");
                    return 0;
                }

                Console.WriteLine("Module failures detected:");
                foreach (var f in failures)
                {
                    Console.WriteLine($"- Module: {f.Module.FullName}");
                    Console.WriteLine(f.Error);
                }

                return 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Runner failed: " + ex);
                return 3;
            }
        }
    }
}
