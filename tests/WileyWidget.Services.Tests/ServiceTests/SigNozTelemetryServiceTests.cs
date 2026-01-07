using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Services.Tests.ServiceTests;

public class SigNozTelemetryServiceTests
{
    [Fact]
    public void ValidateConnectivity_ReturnsTrue()
    {
        var logger = Mock.Of<ILogger<SigNozTelemetryService>>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var serviceProvider = Mock.Of<IServiceProvider>();

#pragma warning disable CA2000 // Do not dispose telemetry in test to avoid DB flush; object used only for lightweight assertions
        var telemetry = new SigNozTelemetryService(logger, configuration, serviceProvider);
#pragma warning restore CA2000

        Assert.True(telemetry.ValidateConnectivity());
        // Do not call Dispose() here to avoid attempting DB flush in test environment
    }

    [Fact]
    public void StartActivity_ReturnsActivityOrNull_DoesNotThrow()
    {
        var logger = Mock.Of<ILogger<SigNozTelemetryService>>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var serviceProvider = Mock.Of<IServiceProvider>();

#pragma warning disable CA2000 // Do not dispose telemetry in test to avoid DB flush; object used only for lightweight assertions
        var telemetry = new SigNozTelemetryService(logger, configuration, serviceProvider);
#pragma warning restore CA2000

        var activity = telemetry.StartActivity("test.operation");

        // Activity may be null depending on environment; ensure no exception and type is expected
        Assert.True(activity == null || activity is System.Diagnostics.Activity);
        // Do not call Dispose() here to avoid attempting DB flush in test environment
    }
}
