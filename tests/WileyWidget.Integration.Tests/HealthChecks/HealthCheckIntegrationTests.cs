using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using Xunit;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;

namespace WileyWidget.Integration.Tests.HealthChecks
{
    public class HealthCheckIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task HealthCheckService_CheckHealthAsync_ReturnsHealthyReport()
        {
            // Arrange
            var healthCheckService = GetRequiredService<HealthCheckService>();

            // Act
            var report = await healthCheckService.CheckHealthAsync();

            // Assert
            report.Should().NotBeNull();
            report.OverallStatus.Should().Be(WileyWidget.Models.HealthStatus.Healthy);
            report.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            report.Results.Should().NotBeEmpty();
        }

        [Fact]
        public async Task HealthCheckService_CheckHealthAsync_IncludesDatabaseCheck()
        {
            // Arrange
            var healthCheckService = GetRequiredService<HealthCheckService>();

            // Act
            var report = await healthCheckService.CheckHealthAsync();

            // Assert
            report.Should().NotBeNull();
            // Should include database connectivity check
            report.Results.Should().Contain(r => r.ServiceName.Contains("Database") || r.ServiceName.Contains("Db"));
        }

        [Fact]
        public async Task HealthCheckService_CheckHealthAsync_HandlesCancellation()
        {
            // Arrange
            var healthCheckService = GetRequiredService<HealthCheckService>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                healthCheckService.CheckHealthAsync(cts.Token));
        }
    }
}
