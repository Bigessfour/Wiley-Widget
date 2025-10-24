using System;
using Microsoft.Extensions.DependencyInjection;
using Unity;
using Unity.Lifetime;

namespace WileyWidget.Services;

/// <summary>
/// Unity-based implementation of IServiceScopeFactory using child containers for scoped services.
/// This bridges Microsoft DI interfaces with Unity's hierarchical container model.
/// </summary>
public class UnityServiceScopeFactory : IServiceScopeFactory
{
    private readonly IUnityContainer _container;

    public UnityServiceScopeFactory(IUnityContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public IServiceScope CreateScope()
    {
        // Create child container with hierarchical lifetime for scoped services
        var childContainer = _container.CreateChildContainer();
        return new UnityServiceScope(childContainer);
    }
}

/// <summary>
/// Unity-based implementation of IServiceScope that wraps a Unity child container.
/// Provides isolated service resolution for scoped operations (e.g., per-scenario DB contexts).
/// </summary>
public sealed class UnityServiceScope : IServiceScope, IDisposable
{
    private readonly IUnityContainer _childContainer;
    private bool _disposed;

    public UnityServiceScope(IUnityContainer childContainer)
    {
        _childContainer = childContainer ?? throw new ArgumentNullException(nameof(childContainer));
    }

    public IServiceProvider ServiceProvider => (IServiceProvider)_childContainer;

    // Standard IDisposable pattern
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed state (managed objects)
            _childContainer?.Dispose();
        }

        // Free unmanaged resources (none) and set large fields to null (none)
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
