using System;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Services;

/// <summary>
/// DryIoc-based implementation of IServiceScope that wraps a DryIoc scope.
/// Provides isolated service resolution for scoped operations (e.g., per-scenario DB contexts).
/// </summary>
public sealed class DryIocServiceScopeImpl : IServiceScope, IDisposable
{
    private readonly DryIoc.IResolverContext _resolverContext;
    private bool _disposed;

    public DryIocServiceScopeImpl(DryIoc.IResolverContext resolverContext)
    {
        _resolverContext = resolverContext ?? throw new ArgumentNullException(nameof(resolverContext));
    }

    public IServiceProvider ServiceProvider => new DryIocServiceProviderAdapter((object)_resolverContext);

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
