using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using DryIoc;
using Prism.Ioc;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// DryIoc-based implementation of IServiceScopeFactory using scoped containers for scoped services.
/// This bridges Microsoft DI interfaces with DryIoc's scoping model.
/// </summary>
public class DryIocServiceScopeFactory : IServiceScopeFactory
{
    private readonly DryIoc.IContainer _container;

    public DryIocServiceScopeFactory(IContainerProvider containerProvider)
    {
        if (containerProvider == null) throw new ArgumentNullException(nameof(containerProvider));

        // Try to obtain the underlying DryIoc container from the Prism container provider using reflection.
        // This keeps the DI registration at Prism/IContainerRegistry level while still supporting
        // services that need DryIoc scoping behavior.
        _container = TryGetDryIocContainer(containerProvider) ?? throw new InvalidOperationException("DryIoc container could not be obtained from the Prism container provider.");
    }

    public IServiceScope CreateScope()
    {
        // Create scoped resolver/context from DryIoc container
        var scope = _container.OpenScope(); // returns IResolverContext
        return new DryIocServiceScopeImpl(scope);
    }

    private static DryIoc.IContainer? TryGetDryIocContainer(IContainerProvider provider)
    {
        try
        {
            var provType = provider.GetType();

            // 1) Try public 'Container' property (common on ContainerExtension types)
            var prop = provType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(provider);
                if (val is DryIoc.IContainer di) return di;
                // Sometimes the property returns a container wrapper - try to inspect its properties too
                if (val != null)
                {
                    var innerProp = val.GetType().GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
                    if (innerProp != null)
                    {
                        var innerVal = innerProp.GetValue(val);
                        if (innerVal is DryIoc.IContainer di2) return di2;
                    }
                }
            }

            // 2) Try a parameterless method named 'GetContainer' (some adapters expose this)
            var getContainerMethod = provType.GetMethod("GetContainer", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, Type.EmptyTypes, null);
            if (getContainerMethod != null)
            {
                var result = getContainerMethod.Invoke(provider, null);
                if (result is DryIoc.IContainer di3) return di3;
            }

            // 3) Try private/internal fields commonly used to hold the underlying container (e.g., '_container' or 'container')
            var field = provType.GetField("_container", BindingFlags.NonPublic | BindingFlags.Instance) ?? provType.GetField("container", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var fval = field.GetValue(provider);
                if (fval is DryIoc.IContainer di4) return di4;
            }

            // 4) As a last resort, scan all public properties/fields for an instance of DryIoc.IContainer
            foreach (var p in provType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var v = p.GetValue(provider);
                    if (v is DryIoc.IContainer di5) return di5;
                }
                catch { /* ignore property getter exceptions */ }
            }

            foreach (var f in provType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    var v = f.GetValue(provider);
                    if (v is DryIoc.IContainer di6) return di6;
                }
                catch { }
            }
        }
        catch
        {
            // swallow - we will surface a clearer error at ctor time
        }

        return null;
    }
}
// Removed the DryIocServiceScopeImpl class definition
