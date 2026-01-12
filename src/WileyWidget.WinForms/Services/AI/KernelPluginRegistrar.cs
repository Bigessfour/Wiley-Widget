using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private static readonly Lazy<MethodInfo?> ImportPluginFromObjectMethod = new(ResolveImportPluginFromObjectMethod);

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

                foreach (var pluginType in pluginTypes)
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

            var importMethodDefinition = ImportPluginFromObjectMethod.Value;
            if (importMethodDefinition == null)
            {
                logger?.LogWarning("Could not locate Semantic Kernel ImportPluginFromObject API; plugin {PluginName} will not be registered", pluginName);
                return;
            }

            var importMethod = importMethodDefinition;
            if (importMethod.IsGenericMethodDefinition)
            {
                // Common shape in some SK versions: ImportPluginFromObject<T>(this Kernel kernel, T plugin, ...)
                importMethod = importMethod.MakeGenericMethod(pluginType);
            }

            var parameters = importMethod.GetParameters();

            // Expected extension shape: ImportPluginFromObject(this Kernel kernel, object plugin, string? pluginName = null)
            // Some SK versions omit pluginName; call the best matching overload we found.
            object? result;
            if (parameters.Length >= 3 && parameters[2].ParameterType == typeof(string))
            {
                result = importMethod.Invoke(null, new object?[] { kernel, pluginInstance, pluginName });
            }
            else
            {
                result = importMethod.Invoke(null, new object?[] { kernel, pluginInstance });
            }

            _ = result;
            logger?.LogInformation("Registered kernel plugin {PluginName} from type {Type}", pluginName, pluginType.FullName);
        }

        private static MethodInfo? ResolveImportPluginFromObjectMethod()
        {
            try
            {
                // In SK 1.16, many public extension APIs live in Microsoft.SemanticKernel.Core / Abstractions,
                // while Microsoft.SemanticKernel.dll may just be a facade assembly.
                // Scan only loaded Semantic Kernel assemblies (not the full AppDomain).
                var skAssemblies = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(a => a != null && !a.IsDynamic)
                    .Where(a => (a.GetName().Name ?? string.Empty).StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal))
                    .Distinct()
                    .ToList();

                // Prefer a non-generic overload with (Kernel, object, string?) if present.
                MethodInfo? bestWithoutName = null;

                foreach (var assembly in skAssemblies)
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray()!;
                    }

                    foreach (var type in types)
                    {
                        if (type == null) continue;
                        if (!type.IsSealed || !type.IsAbstract) continue;

                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (!string.Equals(method.Name, "ImportPluginFromObject", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (!method.IsDefined(typeof(ExtensionAttribute), inherit: false))
                            {
                                continue;
                            }

                            var parameters = method.GetParameters();
                            if (parameters.Length < 2)
                            {
                                continue;
                            }

                            if (parameters[0].ParameterType != typeof(Kernel))
                            {
                                continue;
                            }

                            // The plugin parameter may be `object` or a generic parameter (ImportPluginFromObject<T>).
                            if (parameters[1].ParameterType != typeof(object) && !parameters[1].ParameterType.IsGenericParameter)
                            {
                                continue;
                            }

                            if (parameters.Length >= 3 && parameters[2].ParameterType == typeof(string))
                            {
                                return method;
                            }

                            bestWithoutName ??= method;
                        }
                    }
                }

                return bestWithoutName;
            }
            catch
            {
                return null;
            }
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
