using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Imports plugin types into a <see cref="Kernel"/> by scanning assemblies
    /// for types that have methods annotated with <see cref="KernelFunctionAttribute"/>.
    /// </summary>
    public static class KernelPluginRegistrar
    {
        public static void ImportPluginsFromTypes(Kernel kernel, IEnumerable<Type> pluginTypes, ILogger? logger = null, IServiceProvider? serviceProvider = null)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (pluginTypes == null) throw new ArgumentNullException(nameof(pluginTypes));

            foreach (var pluginType in pluginTypes.Where(IsKernelPluginType).Distinct())
            {
                try
                {
                    ImportPluginFromType(kernel, pluginType, logger, serviceProvider);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to register kernel plugin from type {Type}", pluginType.FullName);
                }
            }
        }

        public static void ImportPluginsFromAssemblies(Kernel kernel, IEnumerable<Assembly> assemblies, ILogger? logger = null, IServiceProvider? serviceProvider = null)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

            var distinctAssemblies = assemblies
                .Where(a => a != null && !a.IsDynamic)
                .Distinct()
                .ToList();

            foreach (var assembly in distinctAssemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                    logger?.LogDebug(ex, "ReflectionTypeLoadException while getting types from assembly {Assembly}", assembly.FullName);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to enumerate types from assembly {Assembly}", assembly.FullName);
                    continue;
                }

                var pluginTypes = types
                    .Where(IsKernelPluginType)
                    .ToList();

                if (pluginTypes.Count == 0)
                {
                    continue;
                }

                logger?.LogDebug("Found {Count} plugin type(s) in assembly {Assembly}", pluginTypes.Count, assembly.FullName);

                ImportPluginsFromTypes(kernel, pluginTypes, logger, serviceProvider);
            }
        }

        private static void ImportPluginFromType(Kernel kernel, Type pluginType, ILogger? logger, IServiceProvider? serviceProvider)
        {
            string pluginName = pluginType.Name;

            if (kernel.Plugins.Any(p => string.Equals(p.Name, pluginName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Per Microsoft docs (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities):
            // "ActivatorUtilities.CreateInstance creates an instance using DI to resolve constructor dependencies"
            // This is the ONLY correct approach for plugins with DI-injected constructors
            object? pluginInstance = null;

            if (serviceProvider != null)
            {
                // Use a temporary scope to resolve scoped dependencies safely.
                // This allows plugins with scoped dependencies (e.g., IEnterpriseRepository) to be instantiated
                // without lifetime violations. The scope disposes immediately after creation (cleaning transients),
                // but the plugin instance captures the resolved services and holds them long-term.
                // Safe because repositories use factories and don't hold open DbContexts.
                using var scope = serviceProvider.CreateScope();
                pluginInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, pluginType);
                logger?.LogDebug("Created plugin instance using scoped ActivatorUtilities (DI-aware): {PluginName}", pluginName);
            }
            else
            {
                // Fallback for no DI (rare) - requires parameterless constructor
                pluginInstance = Activator.CreateInstance(pluginType);
                if (pluginInstance != null)
                {
                    logger?.LogDebug("Created plugin instance using Activator.CreateInstance (parameterless ctor): {PluginName}", pluginName);
                }
            }

            if (pluginInstance == null)
            {
                logger?.LogWarning("Could not create instance of plugin type {Type}", pluginType.FullName);
                return;
            }

            kernel.ImportPluginFromObject(pluginInstance, pluginName);
            logger?.LogInformation("Registered kernel plugin {PluginName} from type {Type}", pluginName, pluginType.FullName);
        }

        private static bool IsKernelPluginType(Type? type)
        {
            if (type == null) return false;
            if (!type.IsClass || type.IsAbstract) return false;

            return type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.GetCustomAttributes(inherit: true).Any(a => a is KernelFunctionAttribute));
        }
    }
}
