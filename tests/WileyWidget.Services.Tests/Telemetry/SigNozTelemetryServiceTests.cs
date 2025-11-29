using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Services.Tests.Telemetry
{
    public class SigNozTelemetryServiceTests
    {
        [Fact]
        public void Initialize_And_RecordException_DoNotThrow()
        {
            // Arrange
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var logger = NullLogger<SigNozTelemetryService>.Instance;
            using var svc = new SigNozTelemetryService(logger, config);

            // Act & Assert: should not throw
            svc.Initialize();
            var ex = new InvalidOperationException("telemetry-test");
            svc.RecordException(ex, ("test", "value"));
        }
    }
}
