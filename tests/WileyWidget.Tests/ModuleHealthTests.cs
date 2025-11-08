using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests;

public class ModuleHealthTests
{
    [Fact]
    public void RegisterAndMarkInitialized_UpdatesStatus()
    {
        var mockLogger = new Mock<ILogger<ModuleHealthService>>();
        var svc = new ModuleHealthService(mockLogger.Object);

        svc.RegisterModule("TestModule");
        svc.MarkModuleInitialized("TestModule", true);

        svc.GetModuleStatus("TestModule").Should().Be(ModuleHealthStatus.Healthy);
        svc.AreAllModulesHealthy().Should().BeTrue();
        svc.GetAllModuleStatuses().First().ModuleName.Should().Be("TestModule");
    }

    [Fact]
    public void UnknownModule_GetStatus_ReturnsNotFound()
    {
        var mockLogger = new Mock<ILogger<ModuleHealthService>>();
        var svc = new ModuleHealthService(mockLogger.Object);

        svc.GetModuleStatus("NoSuch").Should().Be(ModuleHealthStatus.NotFound);
    }
}
