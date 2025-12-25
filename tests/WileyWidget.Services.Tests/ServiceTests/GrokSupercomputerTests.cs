using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using Xunit;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class GrokSupercomputerTests
    {
        [Fact]
        public async Task FetchEnterpriseDataAsync_CacheHit_ReturnsCachedReportAndSkipsRepositories()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new AppOptions { EnableDataCaching = true, EnterpriseDataCacheSeconds = 60 });

            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            var cacheKey = $"Grok.FetchEnterpriseData:all:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:";

            var cachedReport = new ReportData { Title = "Cached Report", GeneratedAt = DateTime.UtcNow };
            cache.Set(cacheKey, cachedReport);

            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>(MockBehavior.Strict);
            var mockBudgetRepo = new Mock<IBudgetRepository>(MockBehavior.Strict);
            var mockAuditRepo = new Mock<IAuditRepository>(MockBehavior.Strict);
            var mockAiLogging = new Mock<IAILoggingService>();
            var mockAiService = new Mock<IAIService>();
            var logger = new Mock<ILogger<GrokSupercomputer>>();

            var svc = new GrokSupercomputer(logger.Object, mockEnterpriseRepo.Object, mockBudgetRepo.Object, mockAuditRepo.Object, mockAiLogging.Object, mockAiService.Object, cache, options);

            // Act
            var result = await svc.FetchEnterpriseDataAsync(null, startDate, endDate, string.Empty);

            // Assert
            result.Should().BeSameAs(cachedReport);
        }

        [Fact]
        public async Task FetchEnterpriseDataAsync_SetsCache_OnSuccess()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new AppOptions { EnableDataCaching = true, EnterpriseDataCacheSeconds = 10 });

            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            var cacheKey = $"Grok.FetchEnterpriseData:all:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:";

            var mockBudgetRepo = new Mock<IBudgetRepository>();
            mockBudgetRepo.Setup(m => m.GetBudgetSummaryAsync(startDate, endDate, default)).ReturnsAsync(new BudgetVarianceAnalysis { TotalBudgeted = 100m });
            mockBudgetRepo.Setup(m => m.GetVarianceAnalysisAsync(startDate, endDate, default)).ReturnsAsync(new BudgetVarianceAnalysis());
            mockBudgetRepo.Setup(m => m.GetDepartmentBreakdownAsync(startDate, endDate, default)).ReturnsAsync(new List<DepartmentSummary> { new DepartmentSummary { DepartmentName = "D1", TotalBudgeted = 100m, TotalActual = 90m } });
            mockBudgetRepo.Setup(m => m.GetFundAllocationsAsync(startDate, endDate, default)).ReturnsAsync(new List<FundSummary>());
            mockBudgetRepo.Setup(m => m.GetYearEndSummaryAsync(endDate.Year, default)).ReturnsAsync(new BudgetVarianceAnalysis());

            var mockAuditRepo = new Mock<IAuditRepository>();
            mockAuditRepo.Setup(m => m.GetAuditTrailAsync(startDate, endDate)).ReturnsAsync(new List<AuditEntry>());

            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            mockEnterpriseRepo.Setup(m => m.GetAllAsync()).ReturnsAsync(new List<Enterprise> { new Enterprise { Id = 1, Name = "Test" } });

            var mockAiLogging = new Mock<IAILoggingService>();
            var mockAiService = new Mock<IAIService>();
            var logger = new Mock<ILogger<GrokSupercomputer>>();

            var svc = new GrokSupercomputer(logger.Object, mockEnterpriseRepo.Object, mockBudgetRepo.Object, mockAuditRepo.Object, mockAiLogging.Object, mockAiService.Object, cache, options);

            // Act
            var result = await svc.FetchEnterpriseDataAsync(null, startDate, endDate, string.Empty);

            // Assert cached
            cache.TryGetValue(cacheKey, out var cachedObj).Should().BeTrue();
            cachedObj.Should().BeSameAs(result);
            result.BudgetSummary.TotalBudgeted.Should().Be(100m);
        }

        [Fact]
        public async Task FetchEnterpriseDataAsync_RepositoryThrows_UsesFallbackAndLogsError()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new AppOptions { EnableDataCaching = false, EnterpriseDataCacheSeconds = 10 });

            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);

            var mockBudgetRepo = new Mock<IBudgetRepository>();
            mockBudgetRepo.Setup(m => m.GetBudgetSummaryAsync(startDate, endDate, default)).ThrowsAsync(new InvalidOperationException("fail"));
            mockBudgetRepo.Setup(m => m.GetVarianceAnalysisAsync(startDate, endDate, default)).ReturnsAsync(new BudgetVarianceAnalysis());
            mockBudgetRepo.Setup(m => m.GetDepartmentBreakdownAsync(startDate, endDate, default)).ReturnsAsync(new List<DepartmentSummary>());
            mockBudgetRepo.Setup(m => m.GetFundAllocationsAsync(startDate, endDate, default)).ReturnsAsync(new List<FundSummary>());
            mockBudgetRepo.Setup(m => m.GetYearEndSummaryAsync(endDate.Year, default)).ReturnsAsync(new BudgetVarianceAnalysis());

            var mockAuditRepo = new Mock<IAuditRepository>();
            mockAuditRepo.Setup(m => m.GetAuditTrailAsync(startDate, endDate)).ReturnsAsync(new List<AuditEntry>());

            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            mockEnterpriseRepo.Setup(m => m.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

            var mockAiLogging = new Mock<IAILoggingService>();
            var mockAiService = new Mock<IAIService>();
            var logger = new Mock<ILogger<GrokSupercomputer>>();

            var svc = new GrokSupercomputer(logger.Object, mockEnterpriseRepo.Object, mockBudgetRepo.Object, mockAuditRepo.Object, mockAiLogging.Object, mockAiService.Object, cache, options);

            // Act
            var result = await svc.FetchEnterpriseDataAsync(null, startDate, endDate, string.Empty);

            // Assert
            result.BudgetSummary.Should().NotBeNull();
            result.BudgetSummary.Should().BeOfType<BudgetVarianceAnalysis>();
            mockAiLogging.Verify(m => m.LogError("GetBudgetSummaryAsync", It.IsAny<Exception>()), Times.Once);
        }
    }
}
