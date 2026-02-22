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
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class StartupOrchestratorIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
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

    [StaFact(Skip = "Runs Application.Run() in-process — background threads (GrokAgentService) outlive the test and crash the host. Run standalone.")]
    public async Task StartupOrchestrator_RunApplicationAsync_CreatesMainForm()
    {
        // Arrange
        using var provider = IntegrationTestServices.BuildProvider();
        var orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(provider);

        // Act & Assert
        await FluentActions.Awaiting(() => orchestrator.RunApplicationAsync(provider))
            .Should().NotThrowAsync();
    }

    [StaFact(Skip = "Runs Application.Run() in-process via full sequence — background threads (GrokAgentService) outlive the test and crash the host. Run standalone.")]
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
