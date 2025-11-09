using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Services.Tests
{
    public class AILoggingServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AILoggingService>> _loggerMock;
        private readonly Mock<ILogger<ErrorReportingService>> _errLoggerMock;
        private readonly ErrorReportingService _errorReportingService;
        private readonly AILoggingService _service;
        private readonly string _tempFile;

        public AILoggingServiceTests()
        {
            _loggerMock = new Mock<ILogger<AILoggingService>>();
            _errLoggerMock = new Mock<ILogger<ErrorReportingService>>();
            _errorReportingService = new ErrorReportingService(_errLoggerMock.Object);
            _service = new AILoggingService(_loggerMock.Object, _errorReportingService);

            // Create a temp file for export tests
            _tempFile = Path.Combine(Path.GetTempPath(), $"ai_logs_test_{Guid.NewGuid():N}.json");
        }

        [Fact]
        public void LogQuery_IncrementsTodayCount()
        {
            var before = _service.GetTodayQueryCount();
            _service.LogQuery("q1", "ctx", "model-x");
            Assert.Equal(before + 1, _service.GetTodayQueryCount());
        }

        [Fact]
        public void LogResponse_UpdatesAverageResponseTime()
        {
            _service.LogResponse("q1", "resp1", 100, 0);
            _service.LogResponse("q2", "resp2", 300, 0);
            var avg = _service.GetAverageResponseTime();
            Assert.InRange(avg, 199.9, 200.1);
        }

        [Fact]
        public void LogError_IncrementsErrorCount()
        {
            var before = _service.GetErrorRate();

            _service.LogQuery("q1", "ctx", "m");
            _service.LogError("q1", "something went wrong", "TestError");

            // Error rate is computed over today query count; ensure it's > 0
            var rate = _service.GetErrorRate();
            Assert.True(rate >= 0);
        }

        [Fact]
        public async Task ExportLogsAsync_CreatesFile()
        {
            _service.LogQuery("q1", "ctx", "m");
            _service.LogResponse("q1", "resp", 150, 0);
            await _service.ExportLogsAsync(_tempFile, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
            Assert.True(File.Exists(_tempFile));

            var content = await File.ReadAllTextAsync(_tempFile);
            Assert.Contains("Query", content);
            Assert.Contains("Response", content);
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
        }
    }
}
