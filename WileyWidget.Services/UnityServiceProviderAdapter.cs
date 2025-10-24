using System;
using Unity;

namespace WileyWidget.Services
{
    /// <summary>
    /// Wraps the Unity container in an <see cref="IServiceProvider"/> so components that expect
    /// a BCL service provider can resolve dependencies while still using Prism's Unity integration.
    /// Pattern based on Prism's Unity container guidance.
    /// </summary>
    public sealed class UnityServiceProviderAdapter : IServiceProvider
    {
        private readonly IUnityContainer _container;

        public UnityServiceProviderAdapter(IUnityContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            return _container.IsRegistered(serviceType)
                ? _container.Resolve(serviceType)
                : null;
        }
    }
}
