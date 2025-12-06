using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using System.Text.Json;

namespace WileyWidget.Tests.Unit.Services
{
    /// <summary>
    /// Comprehensive unit tests for GrokSupercomputer class.
    /// Covers all public methods with happy paths, edge cases, error handling, and fallback scenarios.
    /// Uses Moq for dependency mocking and xUnit for testing framework.
    /// Assumptions based on truncated code: Methods like AnalyzeBudgetAsync fetch data, serialize, call AI, and handle fallbacks.
    /// Tests are async-aware and validate logging/metrics where applicable.
    /// </summary>
    public class GrokSupercomputerTests
    {
        private readonly Mock<ILogger<GrokSupercomputer>> _mockLogger;
        private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepo;
        private readonly Mock<IBudgetRepository> _mockBudgetRepo;
        private readonly Mock<IAuditRepository> _mockAuditRepo;
        private readonly Mock<IAILoggingService> _mockAiLoggingService;
        private readonly Mock<IAIService> _mockAiService;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IOptions<AppOptions>> _mockAppOptions;

        private readonly GrokSupercomputer _grokSupercomputer;

        public GrokSupercomputerTests()
        {
            _mockLogger = new Mock<ILogger<GrokSupercomputer>>();
            _mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            _mockBudgetRepo = new Mock<IBudgetRepository>();
            _mockAuditRepo = new Mock<IAuditRepository>();
            _mockAiLoggingService = new Mock<IAILoggingService>();
            _mockAiService = new Mock<IAIService>();
            _mockCache = new Mock<IMemoryCache>();

            // Setup default AppOptions
            var appOptions = new AppOptions
            {
                BudgetVarianceHighThresholdPercent = 10m,
                BudgetVarianceLowThresholdPercent = -10m,
                AIHighConfidence = 80,
                AILowConfidence = 40
            };
            _mockAppOptions = new Mock<IOptions<AppOptions>>();
            _mockAppOptions.Setup(o => o.Value).Returns(appOptions);

            _grokSupercomputer = new GrokSupercomputer(
                _mockLogger.Object,
                _mockEnterpriseRepo.Object,
                _mockBudgetRepo.Object,
                _mockAuditRepo.Object,
                _mockAiLoggingService.Object,
                _mockAiService.Object,
                _mockCache.Object,
                _mockAppOptions.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GrokSupercomputer(
                null!,
                _mockEnterpriseRepo.Object,
                _mockBudgetRepo.Object,
                _mockAuditRepo.Object,
                _mockAiLoggingService.Object,
                _mockAiService.Object,
                _mockCache.Object,
                _mockAppOptions.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenEnterpriseRepoIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GrokSupercomputer(
                _mockLogger.Object,
                null!,
                _mockBudgetRepo.Object,
                _mockAuditRepo.Object,
                _mockAiLoggingService.Object,
                _mockAiService.Object,
                _mockCache.Object,
                _mockAppOptions.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenBudgetRepoIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GrokSupercomputer(
                _mockLogger.Object,
                _mockEnterpriseRepo.Object,
                null!,
                _mockAuditRepo.Object,
                _mockAiLoggingService.Object,
                _mockAiService.Object,
                _mockCache.Object,
                _mockAppOptions.Object
            ));
        }

        #endregion

        #region AnalyzeBudgetAsync Tests

        [Fact]
        public async Task AnalyzeBudgetAsync_SuccessfulAIResponse_ReturnsInsights()
        {
            // Arrange
            int fiscalYear = 2025;
            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry { BudgetedAmount = 1000, ActualAmount = 900 }
            };
            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ReturnsAsync(budgetEntries);

            var aiInsights = "AI-generated budget insights";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(aiInsights);

            // Act
            var result = await _grokSupercomputer.AnalyzeBudgetAsync(fiscalYear);

            // Assert
            Assert.Equal(aiInsights, result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing budget")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
            _mockAiLoggingService.Verify(ls => ls.LogQuery(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockAiLoggingService.Verify(ls => ls.LogResponse(It.IsAny<string>(), aiInsights, It.IsAny<long>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeBudgetAsync_AIFailure_ReturnsFallbackAnalysis()
        {
            // Arrange
            int fiscalYear = 2025;
            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry { BudgetedAmount = 1000, ActualAmount = 900 }
            };
            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ReturnsAsync(budgetEntries);

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeBudgetAsync(fiscalYear);

            // Assert
            Assert.Contains("Basic budget analysis", result);
            Assert.Contains($"fiscal year {fiscalYear}", result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
            _mockAiLoggingService.Verify(ls => ls.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeBudgetAsync_NoBudgetData_ReturnsEmptyFallback()
        {
            // Arrange
            int fiscalYear = 2025;
            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ReturnsAsync((IEnumerable<BudgetEntry>)null!);

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeBudgetAsync(fiscalYear);

            // Assert
            Assert.Contains("Basic budget analysis", result);
        }

        [Fact]
        public async Task AnalyzeBudgetAsync_InvalidFiscalYear_ThrowsException()
        {
            // Arrange
            int invalidFiscalYear = -1;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _grokSupercomputer.AnalyzeBudgetAsync(invalidFiscalYear));
        }

        #endregion

        #region AnalyzeEnterpriseAsync Tests

        [Fact]
        public async Task AnalyzeEnterpriseAsync_SuccessfulAIResponse_ReturnsInsights()
        {
            // Arrange
            int enterpriseId = 1;
            var enterprise = new Enterprise
            {
                Id = enterpriseId,
                Name = "Test Enterprise",
                Description = "Test Description",
                CurrentRate = 50m,
                MonthlyExpenses = 10000m,
                Type = "Sewer"
            };
            _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId))
                .ReturnsAsync(enterprise);

            var aiInsights = "AI-generated enterprise insights";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(aiInsights);

            // Act
            var result = await _grokSupercomputer.AnalyzeEnterpriseAsync(enterpriseId);

            // Assert
            Assert.Equal(aiInsights, result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task AnalyzeEnterpriseAsync_AIFailure_ReturnsFallback()
        {
            // Arrange
            int enterpriseId = 1;
            var enterprise = new Enterprise
            {
                Id = enterpriseId,
                Name = "Test Enterprise",
                Type = "Sewer"
            };
            _mockEnterpriseRepo.Setup(r => r.GetByIdAsync(enterpriseId))
                .ReturnsAsync(enterprise);

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeEnterpriseAsync(enterpriseId);

            // Assert
            Assert.Contains("Basic enterprise analysis", result);
            Assert.Contains($"enterprise ID {enterpriseId}", result);
        }

        [Fact]
        public async Task AnalyzeEnterpriseAsync_InvalidId_ThrowsException()
        {
            // Arrange
            int invalidId = -1;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _grokSupercomputer.AnalyzeEnterpriseAsync(invalidId));
        }

        #endregion

        #region AnalyzeAuditAsync Tests

        [Fact]
        public async Task AnalyzeAuditAsync_SuccessfulAIResponse_ReturnsInsights()
        {
            // Arrange
            var auditEntries = new List<AuditEntry>
            {
                new AuditEntry { User = "admin", Action = "Update", EntityType = "Budget" }
            };
            _mockAuditRepo.Setup(r => r.GetAuditTrailAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(auditEntries);

            var aiInsights = "AI-generated audit insights";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(aiInsights);

            // Act
            var result = await _grokSupercomputer.AnalyzeAuditAsync();

            // Assert
            Assert.Equal(aiInsights, result);
        }

        [Fact]
        public async Task AnalyzeAuditAsync_AIFailure_ReturnsFallback()
        {
            // Arrange
            var auditEntries = new List<AuditEntry>
            {
                new AuditEntry { User = "admin", Action = "Update" }
            };
            _mockAuditRepo.Setup(r => r.GetAuditTrailAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(auditEntries);

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeAuditAsync();

            // Assert
            Assert.Contains("Basic audit analysis", result);
        }

        #endregion

        #region AnalyzeMunicipalAccountsAsync Tests

        [Fact]
        public async Task AnalyzeMunicipalAccountsAsync_SuccessfulAIResponse_ReturnsInsights()
        {
            // Arrange
            var enterprises = new List<Enterprise>
            {
                new Enterprise { Id = 1, Name = "Account 1", Type = "Sewer" }
            };
            _mockEnterpriseRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(enterprises);

            var aiInsights = "AI-generated accounts insights";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(aiInsights);

            // Act
            var result = await _grokSupercomputer.AnalyzeMunicipalAccountsAsync();

            // Assert
            Assert.Equal(aiInsights, result);
        }

        [Fact]
        public async Task AnalyzeMunicipalAccountsAsync_AIFailure_ReturnsFallback()
        {
            // Arrange
            var enterprises = new List<Enterprise>
            {
                new Enterprise { Id = 1, Name = "Account 1" }
            };
            _mockEnterpriseRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(enterprises);

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeMunicipalAccountsAsync();

            // Assert
            Assert.Contains("Basic municipal accounts analysis", result);
        }

        [Fact]
        public async Task AnalyzeMunicipalAccountsAsync_EmptyAccounts_ReturnsZeroCountFallback()
        {
            // Arrange
            _mockEnterpriseRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Enterprise>());

            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.AnalyzeMunicipalAccountsAsync();

            // Assert
            Assert.Contains("Basic municipal accounts analysis", result);
        }

        #endregion

        #region GenerateRecommendationsAsync Tests

        [Fact]
        public async Task GenerateRecommendationsAsync_SuccessfulAIResponse_ReturnsRecommendations()
        {
            // Arrange
            var data = new { Key = "Value" };
            var aiRecommendations = "AI-generated recommendations";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(aiRecommendations);

            // Act
            var result = await _grokSupercomputer.GenerateRecommendationsAsync(data);

            // Assert
            Assert.Equal(aiRecommendations, result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generating AI-powered recommendations")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateRecommendationsAsync_AIFailure_ReturnsFallbackRecommendations()
        {
            // Arrange
            var data = new { Key = "Value" };
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI error"));

            // Act
            var result = await _grokSupercomputer.GenerateRecommendationsAsync(data);

            // Assert
            Assert.Contains("Recommended actions:", result);
            Assert.Contains("Implement data-driven decision making", result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateRecommendationsAsync_NullData_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _grokSupercomputer.GenerateRecommendationsAsync(null!));
        }

        [Fact]
        public async Task GenerateRecommendationsAsync_EmptyData_SerializesCorrectly()
        {
            // Arrange
            var emptyData = new object();
            var aiRecommendations = "Recommendations for empty data";
            _mockAiService.Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.Is<string>(q => q.Contains("{}"))))
                .ReturnsAsync(aiRecommendations);

            // Act
            var result = await _grokSupercomputer.GenerateRecommendationsAsync(emptyData);

            // Assert
            Assert.Equal(aiRecommendations, result);
        }

        #endregion

        #region QueryAsync Tests

        [Fact]
        public async Task QueryAsync_ValidPrompt_ReturnsAIResponse()
        {
            // Arrange
            string prompt = "Test prompt";
            var aiResponse = new AIResponseResult("AI response content");
            _mockAiService.Setup(s => s.SendPromptAsync(prompt, default))
                .ReturnsAsync(aiResponse);

            // Act
            var result = await _grokSupercomputer.QueryAsync(prompt);

            // Assert
            Assert.Equal(aiResponse.Content, result);
            _mockLogger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Executing AI query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockAiLoggingService.Verify(ls => ls.LogMetric("GrokSupercomputer.QueryAsync.ResponseTime", It.IsAny<double>(), It.IsAny<Dictionary<string, object>?>()), Times.Once);
        }

        [Fact]
        public async Task QueryAsync_EmptyPrompt_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _grokSupercomputer.QueryAsync(string.Empty));
        }

        [Fact]
        public async Task QueryAsync_AIFailure_ThrowsException()
        {
            // Arrange
            string prompt = "Test prompt";
            _mockAiService.Setup(s => s.SendPromptAsync(prompt, default))
                .ThrowsAsync(new Exception("AI error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _grokSupercomputer.QueryAsync(prompt));
            _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockAiLoggingService.Verify(ls => ls.LogError("QueryAsync", It.IsAny<Exception>()), Times.Once);
        }

        #endregion
    }
}
