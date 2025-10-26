using System;
using DryIoc;

namespace WileyWidget.Services
{
    /// <summary>
    /// Wraps the DryIoc container in an <see cref="IServiceProvider"/> so components that expect
    /// a BCL service provider can resolve dependencies while still using Prism's DryIoc integration.
    /// Pattern based on Prism's container guidance.
    /// </summary>
    public sealed class DryIocServiceProviderAdapter : IServiceProvider
    {
        private readonly DryIoc.IResolverContext _resolver;

            public DryIocServiceProviderAdapter(DryIoc.IResolverContext resolver)
            {
                _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
                // Resolve with safe fallback returning null when not registered
                return _resolver.Resolve(serviceType, DryIoc.IfUnresolved.ReturnDefault);
            }
    }
}
