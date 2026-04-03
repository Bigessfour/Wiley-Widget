using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WileyWidget.Business.Configuration;
using WileyWidget.Business.Interfaces;
using WileyWidget.Business.Services;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Business;

[Trait("Category", "Business")]
[Trait("Category", "LayerProof")]
public sealed class BusinessContractAndServiceProofTests
{
    [Fact]
    public void RecommendationResult_Preserves_Metadata_And_AdjustmentFactors()
    {
        var result = new RecommendationResult(
            AdjustmentFactors: new Dictionary<string, decimal> { ["Water"] = 1.15m },
            Explanation: "Rates cover cost recovery.",
            FromGrokApi: false,
            ApiModelUsed: "rule-based",
            Warnings: new[] { "fallback" });

        result.AdjustmentFactors["Water"].Should().Be(1.15m);
        result.Explanation.Should().Be("Rates cover cost recovery.");
        result.FromGrokApi.Should().BeFalse();
        result.ApiModelUsed.Should().Be("rule-based");
        result.Warnings.Should().ContainSingle().Which.Should().Be("fallback");
    }

    [Fact]
    public void GrokRecommendationOptions_Defaults_To_TwoHours()
    {
        var options = new GrokRecommendationOptions();

        options.CacheDuration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void AccountTypeValidator_Enforces_AccountNumber_And_FundClass_Rules()
    {
        var validator = new AccountTypeValidator();

        validator.ValidateAccountTypeForNumber(AccountType.Cash, "101.100").Should().BeTrue();
        validator.ValidateAccountTypeForNumber(AccountType.Taxes, "101.100").Should().BeFalse();
        validator.ValidateAccountTypeForFund(AccountType.Taxes, FundClass.Governmental).Should().BeTrue();
        validator.ValidateAccountTypeForFund(AccountType.Inventory, FundClass.Governmental).Should().BeFalse();

        validator.GetValidAccountTypesForNumber("510.100").Should().Contain(AccountType.Services);
        validator.GetValidAccountTypesForFund(FundClass.Proprietary).Should().Contain(AccountType.Sales);
    }

    [Fact]
    public void AccountTypeValidator_Flags_Invalid_MunicipalAccounts()
    {
        var validator = new AccountTypeValidator();
        var invalidAccount = new MunicipalAccount
        {
            Id = 41,
            Name = "Invalid Tax Asset",
            AccountNumber = new AccountNumber("101.100"),
            Type = AccountType.Taxes,
            FundType = MunicipalFundType.General,
        };

        var singleErrors = validator.ValidateAccount(invalidAccount);
        var compliance = validator.ValidateAccountTypeCompliance(new[] { invalidAccount });

        singleErrors.Should().NotBeEmpty();
        compliance.IsValid.Should().BeFalse();
        compliance.Errors.Should().Contain(error => error.Contains("not valid for account number", StringComparison.Ordinal));
    }

    [Fact]
    public void BusinessInterfaces_Expose_Expected_Methods()
    {
        typeof(IDepartmentExpenseService).GetMethod(nameof(IDepartmentExpenseService.GetDepartmentExpensesAsync)).Should().NotBeNull();
        typeof(IDepartmentExpenseService).GetMethod(nameof(IDepartmentExpenseService.GetAllDepartmentExpensesAsync)).Should().NotBeNull();
        typeof(IDepartmentExpenseService).GetMethod(nameof(IDepartmentExpenseService.GetRollingAverageExpensesAsync)).Should().NotBeNull();

        typeof(IGrokRecommendationService).GetMethod(nameof(IGrokRecommendationService.GetRecommendedAdjustmentFactorsAsync)).Should().NotBeNull();
        typeof(IGrokRecommendationService).GetMethod(nameof(IGrokRecommendationService.GetRecommendationExplanationAsync)).Should().NotBeNull();
        typeof(IGrokRecommendationService).GetMethod(nameof(IGrokRecommendationService.ClearCache)).Should().NotBeNull();

        typeof(IQuickBooksBudgetSyncService).GetMethod(nameof(IQuickBooksBudgetSyncService.SyncFiscalYearActualsAsync)).Should().NotBeNull();
    }

    [Fact]
    public void AuditService_Emits_Audit_And_Financial_Log_Events()
    {
        var sink = new CollectingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var service = new AuditService();

            service.LogAudit("copilot", "LOGIN", "User", "Successful");
            service.LogFinancialOperation("copilot", "POST", 125.50m, "410.1");

            sink.Events.Should().HaveCount(2);
            sink.Events[0].Properties["User"].ToString().Should().Contain("copilot");
            sink.Events[0].Properties["Action"].ToString().Should().Contain("LOGIN");
            sink.Events[1].Properties["Entity"].ToString().Should().Contain("Financial");
            sink.Events[1].Properties["Details"].ToString().Should().Contain("125.50").And.Contain("410.1");
        }
        finally
        {
            Log.Logger = previousLogger;
            Log.CloseAndFlush();
        }
    }

    [Fact]
    public async Task DepartmentExpenseService_Uses_Sample_Fallback_When_QuickBooks_Disabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "false",
            })
            .Build();

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            Mock.Of<IQuickBooksService>());

        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 4, 1);

        var result = await service.GetDepartmentExpensesAsync("Water", startDate, endDate);

        result.Should().Be(135000m);
    }

    [Fact]
    public async Task DepartmentExpenseService_Uses_QuickBooks_Data_When_Enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "true",
            })
            .Build();

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync(
                "Water",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExpenseLine(100m),
                new ExpenseLine(25.5m),
            });

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            quickBooks.Object);

        var result = await service.GetDepartmentExpensesAsync("water", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().Be(125.5m);
        quickBooks.VerifyAll();
    }

    [Fact]
    public async Task DepartmentExpenseService_Falls_Back_When_QuickBooks_Requires_Reauthorization()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "true",
            })
            .Build();

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync(
                "Water",
                new DateTime(2026, 1, 1),
                new DateTime(2026, 3, 2),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("QuickBooks token is invalid or expired"));

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            quickBooks.Object);

        var result = await service.GetDepartmentExpensesAsync("water", new DateTime(2026, 1, 1), new DateTime(2026, 3, 2));

        result.Should().Be(90000m);
        quickBooks.VerifyAll();
    }

    [Fact]
    public async Task DepartmentExpenseService_Propagates_Cancellation_From_QuickBooks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "true",
            })
            .Build();

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync(
                "Water",
                new DateTime(2026, 5, 1),
                new DateTime(2026, 5, 31),
                cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            quickBooks.Object);

        var act = () => service.GetDepartmentExpensesAsync(
            "Water",
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 31),
            cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        quickBooks.VerifyAll();
    }

    [Fact]
    public async Task DepartmentExpenseService_Returns_All_Known_Department_Expenses()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "true",
            })
            .Build();

        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Water"] = 100m,
            ["Sewer"] = 200m,
            ["Trash"] = 300m,
            ["Apartments"] = 400m,
            ["Electric"] = 500m,
            ["Gas"] = 600m,
        };

        var quickBooks = new Mock<IQuickBooksService>();
        quickBooks
            .Setup(service => service.QueryExpensesByDepartmentAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string departmentName, DateTime _, DateTime _, CancellationToken _) =>
                new[] { new ExpenseLine(totals[departmentName]) });

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            quickBooks.Object);

        var result = await service.GetAllDepartmentExpensesAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        result.Should().HaveCount(6);
        result["Electric"].Should().Be(500m);
        result["Trash"].Should().Be(300m);
    }

    [Fact]
    public async Task DepartmentExpenseService_Returns_Rolling_Average_From_Fallback_Data()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QuickBooks:Enabled"] = "false",
            })
            .Build();

        var service = new DepartmentExpenseService(
            NullLogger<DepartmentExpenseService>.Instance,
            configuration,
            Mock.Of<IQuickBooksService>());

        var result = await service.GetRollingAverageExpensesAsync("Sewer");

        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-12);
        var expected = 68000m * (decimal)Math.Max(1, (endDate - startDate).Days / 30.0) / 12m;

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GrokRecommendationService_Uses_RuleBased_Fallback_When_Api_Disabled()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateGrokService(cache, enabled: false);
        var expenses = new Dictionary<string, decimal>
        {
            ["Water"] = 100m,
            ["Trash"] = 100m,
        };

        var result = await service.GetRecommendedAdjustmentFactorsAsync(expenses, 10m);

        result.FromGrokApi.Should().BeFalse();
        result.ApiModelUsed.Should().Be("rule-based");
        result.AdjustmentFactors["Water"].Should().Be(1.10m);
        result.AdjustmentFactors["Trash"].Should().Be(1.05m);
        result.Explanation.Should().Contain("10%");
    }

    [Fact]
    public async Task GrokRecommendationService_Provides_Explanation_And_Clears_Cache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateGrokService(cache, enabled: false);
        var expenses = new Dictionary<string, decimal>
        {
            ["Water"] = 100m,
            ["Sewer"] = 200m,
        };

        var recommendations = await service.GetRecommendedAdjustmentFactorsAsync(expenses, 12m);
        var explanation = await service.GetRecommendationExplanationAsync(expenses, 12m);

        recommendations.AdjustmentFactors.Should().ContainKey("Water");
        explanation.Should().Contain("12%").And.Contain("monthly expenses totaling");

        var recommendationKey = InvokePrivate<string>(service, "GenerateCacheKey", expenses, 12m);
        cache.TryGetValue(recommendationKey, out _).Should().BeTrue();
        cache.TryGetValue($"rec_expl_{recommendationKey}", out _).Should().BeTrue();

        service.ClearCache();

        cache.TryGetValue(recommendationKey, out _).Should().BeFalse();
        cache.TryGetValue($"rec_expl_{recommendationKey}", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GrokRecommendationService_HealthCheck_Is_Healthy_When_Api_Disabled()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateGrokService(cache, enabled: false);

        var result = await service.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        result.Description.Should().Contain("rule-based recommendations");
    }

    [Fact]
    public async Task GrokRecommendationService_HealthCheck_Normalizes_Responses_Endpoint()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var service = CreateGrokService(
            cache,
            enabled: true,
            httpClientFactory: new StubHttpClientFactory(httpClient),
            endpoint: "https://api.x.ai/v1/responses");

        var result = await service.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        handler.RequestUris.Should().ContainSingle().Which.Should().Be(new Uri("https://api.x.ai/v1/chat/completions"));
        handler.AuthorizationHeaders.Should().ContainSingle().Which.Should().Be("Bearer fake-key");
    }

    [Fact]
    public async Task GrokRecommendationService_HealthCheck_Returns_Unhealthy_When_Api_Returns_Error()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var service = CreateGrokService(
            cache,
            enabled: true,
            httpClientFactory: new StubHttpClientFactory(httpClient));

        var result = await service.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
        result.Description.Should().Contain("InternalServerError");
    }

    [Fact]
    public void GrokRecommendationService_GenerateStructuredRecommendation_Rounds_And_Trims()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateGrokService(cache, enabled: false);

        var result = service.GenerateStructuredRecommendation(
            new Dictionary<string, decimal> { ["Water"] = 1.123456m },
            "  Explanation with whitespace.  ");

        result.Factors["Water"].Should().Be(1.1235m);
        result.Explanation.Should().Be("Explanation with whitespace.");
    }

    [Fact]
    public async Task GrokRecommendationService_Rejects_Invalid_Inputs()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateGrokService(cache, enabled: false);

        var act = async () => await service.GetRecommendedAdjustmentFactorsAsync(
            new Dictionary<string, decimal> { ["Unknown"] = 100m },
            10m);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static GrokRecommendationService CreateGrokService(
        MemoryCache cache,
        bool enabled,
        IHttpClientFactory? httpClientFactory = null,
        string endpoint = "https://api.x.ai/v1")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = enabled ? "true" : "false",
                ["XAI:Model"] = "grok-4.1",
                ["XAI:Endpoint"] = endpoint,
            })
            .Build();

        var apiKeyProvider = new Mock<IGrokApiKeyProvider>();
        apiKeyProvider.SetupGet(provider => provider.ApiKey).Returns(enabled ? "fake-key" : null);
        apiKeyProvider.SetupGet(provider => provider.IsValidated).Returns(false);
        apiKeyProvider.Setup(provider => provider.GetConfigurationSource()).Returns("test");

        return new GrokRecommendationService(
            apiKeyProvider.Object,
            NullLogger<GrokRecommendationService>.Instance,
            configuration,
            httpClientFactory ?? Mock.Of<IHttpClientFactory>(),
            cache,
            Options.Create(new GrokRecommendationOptions()));
    }

    private static T InvokePrivate<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (T)method!.Invoke(target, args)!;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            return _httpClient;
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<Uri> RequestUris { get; } = new();

        public List<string?> AuthorizationHeaders { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(_responseFactory(request));
        }
    }
}
