using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

[Trait("Category", "ServiceProof")]
public sealed class AdaptiveTimeoutServiceProofTests
{
    [Fact]
    public void GetRecommendedTimeoutSeconds_UsesBaseTimeout_UntilEnoughSamplesExist()
    {
        var service = new AdaptiveTimeoutService(
            NullLogger<AdaptiveTimeoutService>.Instance,
            maxSamples: 20,
            baseTimeoutSeconds: 15d,
            multiplier: 1.5d);

        foreach (var latency in new[] { 1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d })
        {
            service.RecordLatency(latency);
        }

        service.GetRecommendedTimeoutSeconds().Should().Be(15d);

        var stats = service.GetStatistics();
        stats.SampleCount.Should().Be(9);
        stats.AverageLatencySeconds.Should().BeApproximately(5d, 0.001d);
    }

    [Fact]
    public void GetRecommendedTimeoutSeconds_AppliesPercentileBounds_AndResetClearsSamples()
    {
        var service = new AdaptiveTimeoutService(
            NullLogger<AdaptiveTimeoutService>.Instance,
            maxSamples: 10,
            baseTimeoutSeconds: 15d,
            multiplier: 1.5d);

        foreach (var latency in new[] { -5d, 1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d, 100d })
        {
            service.RecordLatency(latency);
        }

        var stats = service.GetStatistics();

        stats.SampleCount.Should().Be(10, "negative latencies should be ignored and the queue should be capped");
        stats.P95LatencySeconds.Should().Be(100d);
        stats.RecommendedTimeoutSeconds.Should().Be(60d, "timeouts should be clamped to the maximum bound");

        service.Reset();

        service.GetStatistics().SampleCount.Should().Be(0);
        service.GetRecommendedTimeoutSeconds().Should().Be(15d);
    }

    [Fact]
    public void GetRecommendedTimeoutSeconds_ClampsToMinimumBound_ForVeryFastLatencies()
    {
        var service = new AdaptiveTimeoutService(
            NullLogger<AdaptiveTimeoutService>.Instance,
            maxSamples: 20,
            baseTimeoutSeconds: 15d,
            multiplier: 1.5d);

        foreach (var latency in Enumerable.Repeat(0.1d, 10))
        {
            service.RecordLatency(latency);
        }

        service.GetRecommendedTimeoutSeconds().Should().Be(5d);
    }
}

[Trait("Category", "ServiceProof")]
public sealed class CorrelationIdServiceProofTests
{
    [Fact]
    public void SetCorrelationId_UpdatesCurrentContext_AndActivityTag()
    {
        var service = new CorrelationIdService(NullLogger<CorrelationIdService>.Instance);
        using var activity = new Activity("service-proof").Start();

        service.SetCorrelationId("proof-correlation-id");

        service.CurrentCorrelationId.Should().Be("proof-correlation-id");
        activity.GetTagItem(CorrelationIdService.CorrelationIdTagName).Should().Be("proof-correlation-id");

        service.ClearCorrelationId();
        service.CurrentCorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteInContextAsync_FlowsCorrelationAcrossAwaits_AndClearsAfterSuccess()
    {
        var service = new CorrelationIdService(NullLogger<CorrelationIdService>.Instance);
        string? observedBeforeAwait = null;
        string? observedAfterAwait = null;

        await service.ExecuteInContextAsync(async () =>
        {
            observedBeforeAwait = service.CurrentCorrelationId;
            await Task.Yield();
            observedAfterAwait = service.CurrentCorrelationId;
        }, "ctx-123");

        observedBeforeAwait.Should().Be("ctx-123");
        observedAfterAwait.Should().Be("ctx-123");
        service.CurrentCorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteInContextAsync_ClearsCorrelationId_AfterException()
    {
        var service = new CorrelationIdService(NullLogger<CorrelationIdService>.Instance);

        var act = () => service.ExecuteInContextAsync(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }, "ctx-error");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        service.CurrentCorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task CorrelatedAIServiceWrapper_ExecutesAIRequest_InsideCorrelationContext()
    {
        var correlationService = new CorrelationIdService(NullLogger<CorrelationIdService>.Instance);
        var aiService = new Mock<IAIService>(MockBehavior.Strict);

        aiService
            .Setup(service => service.GetInsightsAsync("context", "question", default))
            .Returns(() =>
            {
                correlationService.CurrentCorrelationId.Should().Be("wrapper-correlation");
                return Task.FromResult("insight");
            });

        var wrapper = new CorrelatedAIServiceWrapper(
            aiService.Object,
            correlationService,
            NullLogger<CorrelatedAIServiceWrapper>.Instance);

        var result = await wrapper.GetInsightsWithCorrelationAsync("context", "question", "wrapper-correlation");

        result.Should().Be("insight");
        correlationService.CurrentCorrelationId.Should().BeNull();
        aiService.VerifyAll();
    }
}

[Trait("Category", "ServiceProof")]
public sealed class DataAnonymizerServiceProofTests
{
    [Fact]
    public void AnonymizeEnterprise_MasksSensitiveFields_WhilePreservingOperationalData_AndCacheConsistency()
    {
        var service = new DataAnonymizerService(NullLogger<DataAnonymizerService>.Instance);
        var enterprise = new Enterprise
        {
            Id = 7,
            Name = "Wiley Water Utility",
            Type = "Water",
            Status = EnterpriseStatus.Active,
            Description = "Contact jane@example.com or 555-123-4567. SSN 123-45-6789. Account 123456789.",
            CurrentRate = 12.5m,
            CitizenCount = 400,
            MonthlyExpenses = 1500m,
            TotalBudget = 100000m,
            BudgetAmount = 8000m,
            CreatedDate = new DateTime(2026, 1, 1),
            ModifiedDate = new DateTime(2026, 2, 1),
        };

        var anonymizedFirst = service.AnonymizeEnterprise(enterprise);
        var anonymizedSecond = service.AnonymizeEnterprise(enterprise);

        anonymizedFirst.Id.Should().Be(7);
        anonymizedFirst.Name.Should().StartWith("ANON_Enterprise_");
        anonymizedFirst.Name.Should().Be(anonymizedSecond.Name, "the anonymization cache should keep names stable across calls");
        anonymizedFirst.Type.Should().Be("Water");
        anonymizedFirst.Status.Should().Be(EnterpriseStatus.Active);
        anonymizedFirst.CurrentRate.Should().Be(12.5m);
        anonymizedFirst.CitizenCount.Should().Be(400);
        anonymizedFirst.MonthlyExpenses.Should().Be(1500m);
        anonymizedFirst.TotalBudget.Should().Be(100000m);
        anonymizedFirst.BudgetAmount.Should().Be(8000m);
        anonymizedFirst.Description.Should().Contain("[EMAIL_REDACTED]");
        anonymizedFirst.Description.Should().Contain("[PHONE_REDACTED]");
        anonymizedFirst.Description.Should().Contain("[SSN_REDACTED]");
        anonymizedFirst.Description.Should().Contain("[ACCOUNT_REDACTED]");

        service.GetCacheStatistics()["TotalEntries"].Should().Be(1);
    }

    [Fact]
    public void CollectionMethods_HandleNullInputs_And_ClearCacheResetsStatistics()
    {
        var service = new DataAnonymizerService(NullLogger<DataAnonymizerService>.Instance);

        service.AnonymizeEnterprises(null!).Should().BeEmpty();
        service.AnonymizeBudgetDataCollection(null!).Should().BeEmpty();
        service.AnonymizeEnterprise(null!).Should().BeNull();
        service.AnonymizeBudgetData(null!).Should().BeNull();

        var budgets = service.AnonymizeBudgetDataCollection(new[]
        {
            new BudgetData
            {
                EnterpriseId = 9,
                FiscalYear = 2026,
                TotalBudget = 2000m,
                TotalExpenditures = 1500m,
                RemainingBudget = 500m,
            }
        }).ToList();

        budgets.Should().ContainSingle();
        budgets[0].EnterpriseId.Should().Be(9);
        budgets[0].RemainingBudget.Should().Be(500m);

        service.Anonymize("Sensitive Name").Should().StartWith("ANON_Text_");
        service.GetCacheStatistics()["TotalEntries"].Should().BeGreaterThan(0);

        service.ClearCache();

        service.GetCacheStatistics()["TotalEntries"].Should().Be(0);
    }
}
