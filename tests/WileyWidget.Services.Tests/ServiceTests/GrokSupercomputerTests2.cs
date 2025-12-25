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
    public class GrokSupercomputerTests2
    {
        [Fact]
        public async Task CacheHit_ReturnsCachedData()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = Options.Create(new AppOptions { EnableDataCaching = true, EnterpriseDataCacheSeconds = 60 });

            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            var cacheKey = $"Grok.FetchEnterpriseData:all:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:";

            var cachedReport = new ReportData { Title = "Cached" };
            cache.Set(cacheKey, cachedReport);

            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>(MockBehavior.Strict);
            var mockBudgetRepo = new Mock<IBudgetRepository>(MockBehavior.Strict);
            var mockAuditRepo = new Mock<IAuditRepository>(MockBehavior.Strict);
            var mockAiLogging = new Mock<IAILoggingService>();
            var mockAiService = new Mock<IAIService>();
            var logger = new Mock<ILogger<GrokSupercomputer>>();

            var svc = new GrokSupercomputer(logger.Object, mockEnterpriseRepo.Object, mockBudgetRepo.Object, mockAuditRepo.Object, mockAiLogging.Object, mockAiService.Object, cache, options);

            var result = await svc.FetchEnterpriseDataAsync(null, startDate, endDate, string.Empty);

            result.Should().BeSameAs(cachedReport);
        }
    }
}
