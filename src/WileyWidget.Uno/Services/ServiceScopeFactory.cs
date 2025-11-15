using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Services
{
    /// <summary>
    /// Factory for creating service scopes in Uno platform.
    /// </summary>
    public class ServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceScopeFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IServiceScope CreateScope()
        {
            return _serviceProvider.CreateScope();
        }
    }
}