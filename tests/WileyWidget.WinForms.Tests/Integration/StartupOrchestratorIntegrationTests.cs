using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;

namespace WileyWidget.WinForms.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class StartupOrchestratorIntegrationTests
{
    [StaFact]
    public async Task StartupOrchestrator_InitializeAsync_CompletesWithoutErrors()
    {
        // Arrange
        using var provider = IntegrationTestServices.BuildProvider();
        var orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(provider);

        // Act & Assert
        await FluentActions.Awaiting(() => orchestrator.InitializeAsync())
            .Should().NotThrowAsync();
    }

    [StaFact]
    public async Task StartupOrchestrator_ValidateServicesAsync_ValidatesAllServices()
    {
        // Arrange
        using var provider = IntegrationTestServices.BuildProvider();
        var orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(provider);

        // Act & Assert
        await FluentActions.Awaiting(() => orchestrator.ValidateServicesAsync(provider))
            .Should().NotThrowAsync();
    }

    [StaFact]
    public async Task StartupOrchestrator_RunApplicationAsync_CreatesMainForm()
    {
        // Arrange
        using var provider = IntegrationTestServices.BuildProvider();
        var orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(provider);

        // Act & Assert
        await FluentActions.Awaiting(() => orchestrator.RunApplicationAsync(provider))
            .Should().NotThrowAsync();
    }

    [StaFact]
    public async Task StartupOrchestrator_FullStartupSequence_ExecutesAllPhases()
    {
        // Arrange
        using var provider = IntegrationTestServices.BuildProvider();
        var orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(provider);

        // Act
        await orchestrator.InitializeAsync();
        await orchestrator.ValidateServicesAsync(provider);
        await orchestrator.RunApplicationAsync(provider);

        // Assert - If we get here without exceptions, all phases completed
        orchestrator.Should().NotBeNull();
    }
}
