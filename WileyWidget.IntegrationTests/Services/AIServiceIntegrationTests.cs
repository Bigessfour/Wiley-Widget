using System.Threading.Tasks;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.IntegrationTests.TestDoubles;
using Xunit;

namespace WileyWidget.IntegrationTests.Services;

/// <summary>
/// Integration tests for AI services with dependency injection.
/// Demonstrates using IntegrationTestBase for service testing.
/// </summary>
public class AIServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void AIService_ShouldResolve_AsTestDouble()
    {
        // Arrange
        var aiService = GetService<IAIService>();

        // Assert - service should be registered
        Assert.NotNull(aiService);
        Assert.IsType<NullAIServiceDouble>(aiService);
    }

    [Fact]
    public void GrokSupercomputer_ShouldResolve_AsTestDouble()
    {
        // Arrange
        var grokService = GetService<IGrokSupercomputer>();

        // Assert - service should be registered
        Assert.NotNull(grokService);
        Assert.IsType<NullGrokSupercomputerDouble>(grokService);
    }

    [Fact]
    public async Task NullAIService_GetInsightsAsync_ShouldReturnTestStubMessage()
    {
        // Arrange
        var aiService = GetService<IAIService>();

        // Act
        var result = await aiService.GetInsightsAsync("test context", "test question");

        // Assert
        Assert.Contains("[Test Stub]", result);
        Assert.Contains("disabled in integration tests", result);
    }

    [Fact]
    public async Task NullGrokSupercomputer_QueryAsync_ShouldReturnTestStubMessage()
    {
        // Arrange
        var grokService = GetService<IGrokSupercomputer>();

        // Act
        var result = await grokService.QueryAsync("test prompt");

        // Assert
        Assert.Contains("[Test Stub]", result);
        Assert.Contains("disabled in integration tests", result);
    }

    [Fact]
    public void ServiceProvider_ShouldHaveDbContext()
    {
        // Arrange & Act
        var dbContext = GetDbContext();

        // Assert
        Assert.NotNull(dbContext);
        // Database should be created and ready to use
        Assert.True(dbContext.Database.CanConnect());
    }

    [Fact]
    public void TelemetryService_ShouldResolve_AsNullImplementation()
    {
        // Arrange
        var telemetryService = GetService<ITelemetryService>();

        // Assert
        Assert.NotNull(telemetryService);
        Assert.IsType<NullTelemetryService>(telemetryService);
    }
}
