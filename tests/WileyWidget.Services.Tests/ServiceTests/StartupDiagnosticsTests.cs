using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class StartupDiagnosticsTests
    {
        [Fact]
        public void ErrorReportingService_CanBeConstructedAndReportsEvent()
        {
            // Arrange
            var logger = NullLogger<ErrorReportingService>.Instance;

            // Act - construct and wire event
            var service = new ErrorReportingService(logger);

            var wasRaised = false;
            service.ErrorReported += (ex, ctx) => wasRaised = true;

            service.ReportError(new InvalidOperationException("test-init"), "unit-test", showToUser: false);

            // Assert
            Assert.True(wasRaised, "ErrorReported event should have been raised when reporting an error");
        }

        [Fact]
        public void ErrorReportingService_ReportError_RaisesEvent()
        {
            var logger = NullLogger<ErrorReportingService>.Instance;
            var service = new ErrorReportingService(logger);

            var called = false;
            service.ErrorReported += (ex, ctx) => { called = true; };

            service.ReportError(new InvalidOperationException("unit-test"), "unit-test", showToUser: false);

            Assert.True(called, "ErrorReported event should be raised when reporting an error");
        }
    }
}
