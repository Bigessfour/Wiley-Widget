using System;
using Microsoft.Extensions.DependencyInjection;
using DryIoc;

namespace WileyWidget.Services;

/// <summary>
/// DryIoc-based implementation of IServiceScopeFactory using scoped containers for scoped services.
/// This bridges Microsoft DI interfaces with DryIoc's scoping model.
/// </summary>
public class DryIocServiceScopeFactory : IServiceScopeFactory
{
    private readonly DryIoc.IContainer _container;

    public DryIocServiceScopeFactory(DryIoc.IContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public IServiceScope CreateScope()
    {
        // Create scoped resolver/context from DryIoc container
        var scope = _container.OpenScope(); // returns IResolverContext
        return new DryIocServiceScope(scope);
    }
}

/// <summary>
/// DryIoc-based implementation of IServiceScope that wraps a DryIoc scope.
/// Provides isolated service resolution for scoped operations (e.g., per-scenario DB contexts).
/// </summary>
public sealed class DryIocServiceScope : IServiceScope, IDisposable
{
    private readonly DryIoc.IResolverContext _resolverContext;
    private bool _disposed;

    public DryIocServiceScope(DryIoc.IResolverContext resolverContext)
    {
        _resolverContext = resolverContext ?? throw new ArgumentNullException(nameof(resolverContext));
    }

    public IServiceProvider ServiceProvider => new DryIocServiceProviderAdapter(_resolverContext);

    // Standard IDisposable pattern
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose resolver context if it implements IDisposable
            if (_resolverContext is IDisposable d)
            {
                d.Dispose();
            }
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
