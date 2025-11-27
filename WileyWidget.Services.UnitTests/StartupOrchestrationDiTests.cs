using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.Services.UnitTests;

/// <summary>
/// Tests for validating DI registration of startup orchestration services.
/// Ensures all required services are properly registered and resolvable.
/// </summary>
public class StartupOrchestrationDiTests
{
    [Fact]
    public void ServiceCollection_WithStartupOrchestration_ResolvesAllDependencies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register core services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<ITelemetryService, MockTelemetryService>();
        services.AddSingleton<IStartupProgressReporter, StartupProgressReporter>();
        services.AddSingleton<ISecretVaultService, MockSecretVaultService>();
        services.AddSingleton<IDashboardService, MockDashboardService>();

        // Register orchestrator as hosted service
        services.AddHostedService<StartupOrchestrator>();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - all services should resolve without exception
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        hostedServices.Should().NotBeEmpty();
        hostedServices.Should().ContainSingle(s => s is StartupOrchestrator);

        var telemetry = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ITelemetryService>(serviceProvider);
        telemetry.Should().NotBeNull();

        var progressReporter = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupProgressReporter>(serviceProvider);
        progressReporter.Should().NotBeNull();

        var vaultService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISecretVaultService>(serviceProvider);
        vaultService.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProvider_ResolvesStartupOrchestrator_WithAllDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITelemetryService, MockTelemetryService>();
        services.AddSingleton<IStartupProgressReporter, StartupProgressReporter>();
        services.AddSingleton<ISecretVaultService, MockSecretVaultService>();
        services.AddHostedService<StartupOrchestrator>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var orchestrator = serviceProvider.GetServices<IHostedService>()
            .OfType<StartupOrchestrator>()
            .FirstOrDefault();

        // Assert
        orchestrator.Should().NotBeNull();
        orchestrator!.CompletionTask.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProvider_WithoutRequiredService_ThrowsOnResolve()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        // Missing ITelemetryService - orchestrator should fail to resolve

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var act = () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ITelemetryService>(serviceProvider);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartupProgressReporter_RegisteredAsSingleton_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IStartupProgressReporter, StartupProgressReporter>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var instance1 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupProgressReporter>(serviceProvider);
        var instance2 = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupProgressReporter>(serviceProvider);

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void ServiceCollection_ValidatesOnBuild_WithValidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITelemetryService, MockTelemetryService>();
        services.AddSingleton<IStartupProgressReporter, StartupProgressReporter>();
        services.AddSingleton<ISecretVaultService, MockSecretVaultService>();
        services.AddHostedService<StartupOrchestrator>();

        // Act
        var act = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Assert
        act.Should().NotThrow();
    }

    // Mock implementations for testing
    private class MockTelemetryService : ITelemetryService
    {
        public void RecordException(Exception exception, params (string key, object? value)[] additionalTags) { }
        public void RecordMetric(string metricName, double value, params (string key, object? value)[] additionalTags) { }
    }

    private class MockSecretVaultService : ISecretVaultService
    {
        public string? GetSecret(string key) => "mock-secret";
        public void StoreSecret(string key, string value) { }
        public Task<string?> GetSecretAsync(string key) => Task.FromResult<string?>("mock-secret");
        public Task SetSecretAsync(string key, string value) => Task.CompletedTask;
        public Task RotateSecretAsync(string secretName, string newValue) => Task.CompletedTask;
        public Task MigrateSecretsFromEnvironmentAsync() => Task.CompletedTask;
        public Task PopulateProductionSecretsAsync() => Task.CompletedTask;
        public Task<bool> TestConnectionAsync() => Task.FromResult(true);
        public Task<string> ExportSecretsAsync() => Task.FromResult("{}");
        public Task ImportSecretsAsync(string jsonSecrets) => Task.CompletedTask;
        public Task<System.Collections.Generic.IEnumerable<string>> ListSecretKeysAsync() =>
            Task.FromResult<System.Collections.Generic.IEnumerable<string>>(Array.Empty<string>());
        public Task DeleteSecretAsync(string secretName) => Task.CompletedTask;
        public Task<string> GetDiagnosticsAsync() => Task.FromResult("Mock vault OK");
    }

    private class MockDashboardService : IDashboardService
    {
        public Task<System.Collections.Generic.IEnumerable<DashboardMetric>> GetDashboardDataAsync()
        {
            return Task.FromResult<System.Collections.Generic.IEnumerable<DashboardMetric>>(
                Array.Empty<DashboardMetric>());
        }

        public Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            return Task.FromResult(new DashboardSummary());
        }

        public Task RefreshDashboardAsync()
        {
            return Task.CompletedTask;
        }
    }
}
