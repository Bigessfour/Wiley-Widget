using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.WinForms.Services;

namespace WileyWidget.Services.UnitTests
{
    public class LicenseHelperTests
    {
        [Fact]
        public void GetSyncfusionLicenseKey_FromConfiguration_ReturnsKey()
        {
            var inMemory = new Dictionary<string, string?>
            {
                { "Syncfusion:LicenseKey", "config-key-123" }
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

            var key = LicenseHelper.GetSyncfusionLicenseKey(config);

            Assert.Equal("config-key-123", key);
        }

        [Fact]
        public void GetSyncfusionLicenseKey_FromEnvironment_ReturnsEnvKey()
        {
            var keyName = "SYNCFUSION_LICENSE_KEY";
            var expected = "env-key-456";
            Environment.SetEnvironmentVariable(keyName, expected);

            var config = new ConfigurationBuilder().Build();
            var key = LicenseHelper.GetSyncfusionLicenseKey(config);

            Assert.Equal(expected, key);

            // cleanup
            Environment.SetEnvironmentVariable(keyName, null);
        }

        [Fact]
        public void TryRegisterSyncfusionLicense_NoKey_ReturnsFalseAndLogsWarning()
        {
            var config = new ConfigurationBuilder().Build();
            var mockLogger = new Mock<ILogger>();

            var result = LicenseHelper.TryRegisterSyncfusionLicense(config, mockLogger.Object);

            Assert.False(result);
            mockLogger.Verify(l => l.Log(
                It.Is<LogLevel>(ll => ll == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}
