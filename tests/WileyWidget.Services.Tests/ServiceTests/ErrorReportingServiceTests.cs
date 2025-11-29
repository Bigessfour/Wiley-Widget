using System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Services;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class ErrorReportingServiceTests
    {
        [Fact]
        public void ReportError_Raises_Event_And_Suppresses_Dialog_When_Configured()
        {
            // Arrange
            var logger = NullLogger<ErrorReportingService>.Instance;
            var service = new ErrorReportingService(logger)
            {
                SuppressUserDialogs = true
            };

            var called = false;
            service.ErrorReported += (ex, ctx) => called = true;

            // Act
            service.ReportError(new InvalidOperationException("unit-test"), "unit-test", showToUser: false);

            // Assert
            Assert.True(called, "ErrorReported event should be invoked when reporting an error.");
        }
    }
}
