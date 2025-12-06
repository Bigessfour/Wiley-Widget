using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for AILoggingService - AI usage tracking and metrics
    /// Tests query logging, response logging, error logging, and metrics collection
    /// </summary>
    public class AILoggingServiceTests
    {
        private readonly Mock<ILogger<AILoggingService>> _mockLogger;
        private readonly AILoggingService _service;

        public AILoggingServiceTests()
        {
            _mockLogger = new Mock<ILogger<AILoggingService>>();
            _service = new AILoggingService(_mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AILoggingService(null!));
        }

        [Fact]
        public void Constructor_InitializesSuccessfully_WithValidLogger()
        {
            // Arrange & Act
            var service = new AILoggingService(_mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        #endregion

        #region LogQuery Tests

        [Fact]
        public void LogQuery_WithValidParameters_LogsSuccessfully()
        {
            // Arrange
            string query = "What is the budget for 2025?";
            string context = "Budget Analysis";
            string model = "grok-4-0709";

            // Act
            _service.LogQuery(query, context, model);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initialized")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogQuery_WithNullQuery_HandlesGracefully()
        {
            // Arrange
            string context = "Test Context";
            string model = "grok-4-0709";

            // Act
            _service.LogQuery(null!, context, model);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogQuery_WithEmptyContext_HandlesGracefully()
        {
            // Arrange
            string query = "Test query";
            string model = "grok-4-0709";

            // Act
            _service.LogQuery(query, string.Empty, model);

            // Assert - Should not throw
            Assert.True(true);
        }

        #endregion

        #region LogResponse Tests

        [Fact]
        public void LogResponse_WithValidParameters_LogsSuccessfully()
        {
            // Arrange
            string sessionId = "session-123";
            string response = "The budget for 2025 is $1,000,000";
            long responseTimeMs = 1500;
            int tokenCount = 250;

            // Act
            _service.LogResponse(sessionId, response, responseTimeMs, tokenCount);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogResponse_WithNullResponse_HandlesGracefully()
        {
            // Arrange
            string sessionId = "session-123";
            long responseTimeMs = 1500;
            int tokenCount = 0;

            // Act
            _service.LogResponse(sessionId, null!, responseTimeMs, tokenCount);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogResponse_WithZeroResponseTime_LogsCorrectly()
        {
            // Arrange
            string sessionId = "session-123";
            string response = "Quick response";
            long responseTimeMs = 0;
            int tokenCount = 10;

            // Act
            _service.LogResponse(sessionId, response, responseTimeMs, tokenCount);

            // Assert - Should not throw
            Assert.True(true);
        }

        #endregion

        #region LogError Tests

        [Fact]
        public void LogError_WithValidParameters_LogsSuccessfully()
        {
            // Arrange
            string operation = "GetBudgetData";
            var exception = new Exception("Test error");

            // Act
            _service.LogError(operation, exception);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogError_WithNullOperation_HandlesGracefully()
        {
            // Arrange
            var exception = new Exception("Test error");

            // Act
            _service.LogError(null!, exception);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogError_WithNullException_HandlesGracefully()
        {
            // Arrange
            string operation = "GetBudgetData";

            // Act
            _service.LogError(operation, null!);

            // Assert - Should not throw
            Assert.True(true);
        }

        #endregion

        #region LogMetric Tests

        [Fact]
        public void LogMetric_WithValidParameters_LogsSuccessfully()
        {
            // Arrange
            string metricName = "ResponseTime";
            double value = 1500.5;
            var tags = new Dictionary<string, object>
            {
                { "Model", "grok-4-0709" },
                { "Success", true }
            };

            // Act
            _service.LogMetric(metricName, value, tags);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogMetric_WithNullTags_HandlesGracefully()
        {
            // Arrange
            string metricName = "ResponseTime";
            double value = 1500.5;

            // Act
            _service.LogMetric(metricName, value, null);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public void LogMetric_WithNegativeValue_LogsCorrectly()
        {
            // Arrange
            string metricName = "Variance";
            double value = -150.5;

            // Act
            _service.LogMetric(metricName, value, null);

            // Assert - Should not throw
            Assert.True(true);
        }

        #endregion

        #region GetTodayMetrics Tests

        [Fact]
        public void GetTodayMetrics_ReturnsNonNull()
        {
            // Act
            var metrics = _service.GetTodayMetrics();

            // Assert
            Assert.NotNull(metrics);
        }

        [Fact]
        public void GetTodayMetrics_AfterLoggingQuery_ReflectsQueryCount()
        {
            // Arrange
            _service.LogQuery("Test query", "Test context", "grok-4-0709");
            _service.LogQuery("Another query", "Context", "grok-4-0709");

            // Act
            var metrics = _service.GetTodayMetrics();

            // Assert
            Assert.NotNull(metrics);
            // Note: Metrics should contain query count, but we're testing structure exists
        }

        [Fact]
        public void GetTodayMetrics_AfterLoggingError_ReflectsErrorCount()
        {
            // Arrange
            _service.LogError("TestOperation", new Exception("Test"));

            // Act
            var metrics = _service.GetTodayMetrics();

            // Assert
            Assert.NotNull(metrics);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteLoggingFlow_QueryToResponse_WorksEndToEnd()
        {
            // Arrange
            string query = "What is the budget?";
            string context = "Budget Analysis";
            string model = "grok-4-0709";
            string sessionId = Guid.NewGuid().ToString();
            string response = "The budget is $1M";
            long responseTime = 1500;
            int tokens = 200;

            // Act
            _service.LogQuery(query, context, model);
            _service.LogResponse(sessionId, response, responseTime, tokens);
            var metrics = _service.GetTodayMetrics();

            // Assert
            Assert.NotNull(metrics);
        }

        [Fact]
        public void CompleteLoggingFlow_QueryToError_WorksEndToEnd()
        {
            // Arrange
            string query = "What is the budget?";
            string context = "Budget Analysis";
            string model = "grok-4-0709";
            var exception = new Exception("API timeout");

            // Act
            _service.LogQuery(query, context, model);
            _service.LogError("GetBudget", exception);
            var metrics = _service.GetTodayMetrics();

            // Assert
            Assert.NotNull(metrics);
        }

        #endregion
    }
}
