using System;
using Xunit;
using WileyWidget.Services;
using Microsoft.Extensions.Logging.Abstractions;

public class ModuleHealthServiceTests
{
    [Fact]
    public void RegisterAndMarkModule_Healthy_StatusReflects()
    {
        var logger = NullLogger<ModuleHealthService>.Instance;
        var svc = new ModuleHealthService(logger);
        svc.RegisterModule("TestModule");
        Assert.Equal(ModuleHealthStatus.Registered, svc.GetModuleStatus("TestModule"));

        svc.MarkModuleInitialized("TestModule", true);
        Assert.Equal(ModuleHealthStatus.Healthy, svc.GetModuleStatus("TestModule"));
        Assert.True(svc.AreAllModulesHealthy());
    }

    [Fact]
    public void MarkUnknownModule_NoThrows_StatusNotFound()
    {
        var logger = NullLogger<ModuleHealthService>.Instance;
        var svc = new ModuleHealthService(logger);
        svc.MarkModuleInitialized("Unknown", false, "err");
        Assert.Equal(ModuleHealthStatus.NotFound, svc.GetModuleStatus("Unknown"));
    }
}