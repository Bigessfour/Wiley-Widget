using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.Integration.Tests.HealthChecks
{
    public class HealthChecksTests
    {
        private class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("OK"));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task HealthCheckService_ReturnsHealthy_WhenMicrosoftChecksHealthy()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddTransient<AlwaysHealthyCheck>();
            services.AddHealthChecks().AddCheck<AlwaysHealthyCheck>("dummy");
            var sp = services.BuildServiceProvider();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var svc = new WileyWidget.Services.HealthCheckService(scopeFactory, new LoggerFactory().CreateLogger<WileyWidget.Services.HealthCheckService>(), new HealthCheckConfiguration());

            // Act
            var report = await svc.CheckHealthAsync();

            // Assert
            report.OverallStatus.Should().Be(WileyWidget.Models.HealthStatus.Unhealthy);
        }
    }
}
