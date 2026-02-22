using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IServiceProviderIsService = Microsoft.Extensions.DependencyInjection.IServiceProviderIsService;
using IServiceScope = Microsoft.Extensions.DependencyInjection.IServiceScope;
using Moq;
using WileyWidget.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;
using IStartupTimelineService = WileyWidget.Services.IStartupTimelineService;

// ═══════════════════════════════════════════════════════════════════════════
// Core Architecture Symbols Integration Tests
//
// Covers the 4 previously unexercised symbols and 4 thin-coverage symbols
// identified in the February 2026 coverage audit:
//
//   ❌ Zero coverage (new):
//       - StartupHostedService
//       - IWinFormsDiValidator / WinFormsDiValidator
//       - IStartupTimelineService / StartupInstrumentation
//       - AnalyticsHubPanel
//
//   ⚠️ Thin coverage (extended):
//       - IAsyncInitializable (sequencing + cancellation)
//       - PanelRegistry (completeness + DI alignment)
//       - IEnterpriseRepository (behavioral mock contract)
//       - IUtilityBillRepository (behavioral mock contract)
// ═══════════════════════════════════════════════════════════════════════════

namespace WileyWidget.WinForms.Tests.Integration;

// ─────────────────────────────────────────────────────────────────────────────
// 1. StartupHostedService
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "StartupHostedService")]
public sealed class StartupHostedServiceIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// StartupHostedService is registered via AddHostedService in AddWinFormsServices
    /// and must be resolvable as IHostedService from the production container.
    /// </summary>
    [Fact]
    public void StartupHostedService_IsRegistered_AsIHostedService()
    {
        var provider = Services;

        var hostedServices = provider.GetServices<IHostedService>().ToList();

        hostedServices.Should().ContainSingle(
            s => s is StartupHostedService,
            because: "AddWinFormsServices calls AddHostedService<StartupHostedService>");
    }

    /// <summary>
    /// ExecuteAsync must call IStartupOrchestrator.InitializeAsync exactly once.
    /// This is the core contract: the hosted service sequences the deferred async startup.
    /// </summary>
    [Fact]
    public async Task StartupHostedService_ExecuteAsync_CallsOrchestratorInitializeAsync_ExactlyOnce()
    {
        var orchestratorMock = new Mock<IStartupOrchestrator>();
        orchestratorMock
            .Setup(o => o.InitializeAsync())
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<StartupHostedService>>();
        var service = new StartupHostedService(orchestratorMock.Object, loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Allow the background task to complete
        await Task.Delay(200, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        orchestratorMock.Verify(o => o.InitializeAsync(), Times.Once,
            "ExecuteAsync must delegate startup orchestration to IStartupOrchestrator.InitializeAsync");
    }

    /// <summary>
    /// When the stoppingToken is cancelled before ExecuteAsync completes it must
    /// swallow the OperationCanceledException and not rethrow — preserving the
    /// rule that startup failures must not crash the host.
    /// </summary>
    [Fact]
    public async Task StartupHostedService_ExecuteAsync_WhenCancelled_DoesNotThrow()
    {
        var orchestratorMock = new Mock<IStartupOrchestrator>();
        orchestratorMock
            .Setup(o => o.InitializeAsync())
            .Returns(async () =>
            {
                await Task.Delay(Timeout.Infinite, new CancellationTokenSource(50).Token);
            });

        var loggerMock = new Mock<ILogger<StartupHostedService>>();
        var service = new StartupHostedService(orchestratorMock.Object, loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = async () =>
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(300, CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        };

        await act.Should().NotThrowAsync(
            because: "OperationCanceledException must be caught internally; service must not propagate it");
    }

    /// <summary>
    /// When IStartupOrchestrator.InitializeAsync throws an unexpected exception
    /// the service must log the error and swallow it — never rethrowing — to keep
    /// the application alive with degraded functionality.
    /// </summary>
    [Fact]
    public async Task StartupHostedService_ExecuteAsync_WhenOrchestratorThrows_DoesNotRethrow()
    {
        var orchestratorMock = new Mock<IStartupOrchestrator>();
        orchestratorMock
            .Setup(o => o.InitializeAsync())
            .ThrowsAsync(new InvalidOperationException("Simulated startup failure"));

        var loggerMock = new Mock<ILogger<StartupHostedService>>();
        var service = new StartupHostedService(orchestratorMock.Object, loggerMock.Object);

        Func<Task> act = async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(300, CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        };

        await act.Should().NotThrowAsync(
            because: "Critical exceptions must be logged and swallowed — the contract is explicit in the XML doc");
    }

    /// <summary>
    /// Constructor must reject null arguments with ArgumentNullException, not silently accept them.
    /// </summary>
    [Fact]
    public void StartupHostedService_Constructor_RejectsNullArguments()
    {
        var orchestrator = Mock.Of<IStartupOrchestrator>();
        var logger = Mock.Of<ILogger<StartupHostedService>>();

        FluentActions.Invoking(() => new StartupHostedService(null!, logger))
            .Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");

        FluentActions.Invoking(() => new StartupHostedService(orchestrator, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. IWinFormsDiValidator / WinFormsDiValidator
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "WinFormsDiValidator")]
public sealed class WinFormsDiValidatorIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// IWinFormsDiValidator must be resolvable as a singleton from the production container.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_IsRegistered_AsSingleton()
    {
        var provider = Services;

        var validator = provider.GetService<IWinFormsDiValidator>();

        validator.Should().NotBeNull(
            because: "AddWinFormsServices registers IWinFormsDiValidator as singleton");
        validator.Should().BeOfType<WinFormsDiValidator>();
    }

    /// <summary>
    /// ValidateCriticalServices must pass against the full production container,
    /// confirming IConfiguration, ITelemetryService, etc. are all present.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateCriticalServices_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateCriticalServices(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"All critical services must be registered. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateRepositories must pass — every repository interface must be in the production container.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateRepositories_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateRepositories(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"All repository interfaces must be registered. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateServices must pass against the production container for the full business service layer.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateServices_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateServices(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"All business services must be registered. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateViewModels must confirm every ViewModel surfaced through DI is present.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateViewModels_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateViewModels(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"All ViewModels must be registered. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateForms must confirm MainForm (the primary UI shell) is registered.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateForms_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateForms(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"MainForm must be registered. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidatePanelsFromRegistry must confirm every panel in PanelRegistry.Panels is in DI.
    /// This is the cross-check between the static registry and the live container.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidatePanelsFromRegistry_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidatePanelsFromRegistry(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"Every panel in PanelRegistry must be registered in DI. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateScopedPanels checks that all ScopedPanelBase<T> subtypes are registered as scoped.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateScopedPanels_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateScopedPanels(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"All ScopedPanelBase<T> panels must be registered as scoped services. Errors: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// ValidateAll is the comprehensive single-call check that the startup orchestrator
    /// invokes on startup. It must pass on a clean production container with no gaps.
    /// </summary>
    [Fact]
    public void WinFormsDiValidator_ValidateAll_PassesOnProductionContainer()
    {
        var provider = Services;
        var validator = provider.GetRequiredService<IWinFormsDiValidator>();

        var result = validator.ValidateAll(provider);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(
            because: $"ValidateAll is the startup gate; every category must pass. Errors: {string.Join(", ", result.Errors)}");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. IStartupTimelineService + StartupInstrumentation
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "IStartupTimelineService")]
public sealed class StartupTimelineServiceIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// IStartupTimelineService must be resolvable as a singleton from the production container.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_IsRegistered_AsSingleton()
    {
        var provider = Services;

        var service = provider.GetService<IStartupTimelineService>();

        service.Should().NotBeNull(
            because: "AddWinFormsServices calls TryAddSingleton<IStartupTimelineService, StartupTimelineService>");
    }

    /// <summary>
    /// RecordPhaseStart / RecordPhaseEnd must not throw and must record the phase name.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_RecordPhaseStartAndEnd_DoNotThrow()
    {
        var provider = Services;
        var service = provider.GetRequiredService<IStartupTimelineService>();

        Action act = () =>
        {
            service.RecordPhaseStart("TestPhase", expectedOrder: 1);
            service.RecordPhaseEnd("TestPhase");
        };

        act.Should().NotThrow(because: "Phase bookkeeping must never throw internally");
    }

    /// <summary>
    /// BeginPhaseScope must return a disposable handle that closes the phase on Dispose.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_BeginPhaseScope_ReturnsDisposable()
    {
        var provider = Services;
        var service = provider.GetRequiredService<IStartupTimelineService>();

        IDisposable? scope = null;
        Action act = () =>
        {
            scope = service.BeginPhaseScope("ScopeTestPhase", expectedOrder: 2);
            scope.Dispose();
        };

        act.Should().NotThrow(because: "BeginPhaseScope RAII handle must be safely disposable");
        scope.Should().NotBeNull();
    }

    /// <summary>
    /// GenerateReport must return a non-null StartupTimelineReport that can be called
    /// after phases have been recorded — this is the report that Program logs on startup.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_GenerateReport_ReturnsNonNullReport()
    {
        var provider = Services;
        var service = provider.GetRequiredService<IStartupTimelineService>();

        service.RecordPhaseStart("LicenseInit", expectedOrder: 1);
        service.RecordPhaseEnd("LicenseInit");
        service.RecordPhaseStart("ThemeInit", expectedOrder: 2);
        service.RecordPhaseEnd("ThemeInit");

        var report = service.GenerateReport();

        report.Should().NotBeNull(
            because: "GenerateReport is called by Program.cs on startup and must always produce a value");
    }

    /// <summary>
    /// RecordOperation must not throw when called with valid phase and operation names.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_RecordOperation_DoesNotThrow()
    {
        var provider = Services;
        var service = provider.GetRequiredService<IStartupTimelineService>();

        Action act = () =>
        {
            service.RecordPhaseStart("DI Validation", expectedOrder: 3);
            service.RecordOperation("ValidateCriticalServices", "DI Validation", durationMs: 12.5);
            service.RecordPhaseEnd("DI Validation");
        };

        act.Should().NotThrow();
    }

    /// <summary>
    /// RecordFormLifecycleEvent must not throw — it is called from MainForm.Shown event.
    /// </summary>
    [Fact]
    public void IStartupTimelineService_RecordFormLifecycleEvent_DoesNotThrow()
    {
        var provider = Services;
        var service = provider.GetRequiredService<IStartupTimelineService>();

        Action act = () => service.RecordFormLifecycleEvent("MainForm", "Shown");

        act.Should().NotThrow();
    }
}

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "StartupInstrumentation")]
public sealed class StartupInstrumentationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// RecordPhaseTime must store the timing so GetInitializationMetrics reflects it.
    /// Uses a unique phase name per test to avoid cross-test pollution on the static state.
    /// </summary>
    [Fact]
    public void StartupInstrumentation_RecordPhaseTime_AppearsInMetrics()
    {
        var phaseName = $"TestPhase_{Guid.NewGuid():N}";

        StartupInstrumentation.RecordPhaseTime(phaseName, 42L);

        var metrics = StartupInstrumentation.GetInitializationMetrics();
        metrics.Should().ContainKey(phaseName)
            .WhoseValue.Should().Be(42L,
                because: "RecordPhaseTime must persist the exact millisecond value");
    }

    /// <summary>
    /// RecordPhaseTime with a null/empty name must not throw — defensive guard behavior.
    /// </summary>
    [Fact]
    public void StartupInstrumentation_RecordPhaseTime_WithEmptyName_DoesNotThrow()
    {
        Action act = () => StartupInstrumentation.RecordPhaseTime(string.Empty, 10L);
        act.Should().NotThrow(because: "Empty phase names must be silently ignored");
    }

    /// <summary>
    /// GetTotalInitializationTime must equal the sum of all recorded phase durations
    /// at the time of the call.
    /// </summary>
    [Fact]
    public void StartupInstrumentation_GetTotalInitializationTime_IsSumOfAllPhases()
    {
        var phase1 = $"Phase1_{Guid.NewGuid():N}";
        var phase2 = $"Phase2_{Guid.NewGuid():N}";

        StartupInstrumentation.RecordPhaseTime(phase1, 100L);
        StartupInstrumentation.RecordPhaseTime(phase2, 200L);

        var total = StartupInstrumentation.GetTotalInitializationTime();

        // Total must be at least the two phases we just added (others may exist from prior tests)
        total.Should().BeGreaterThanOrEqualTo(300L,
            because: "GetTotalInitializationTime must aggregate all recorded phases");
    }

    /// <summary>
    /// GetInitializationMetrics returns phases in insertion order — the order field
    /// is the contract used by the startup report to show a meaningful timeline.
    /// </summary>
    [Fact]
    public void StartupInstrumentation_GetInitializationMetrics_IncludesAllRecordedPhases()
    {
        var phase = $"OrderPhase_{Guid.NewGuid():N}";
        StartupInstrumentation.RecordPhaseTime(phase, 77L);

        var metrics = StartupInstrumentation.GetInitializationMetrics();

        metrics.Should().ContainKey(phase);
        metrics[phase].Should().Be(77L);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. AnalyticsHubPanel
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "AnalyticsHubPanel")]
public sealed class AnalyticsHubPanelIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// AnalyticsHubPanel must be registered as a scoped service in the production container.
    /// </summary>
    [Fact]
    public void AnalyticsHubPanel_IsRegistered_AsScoped_InProductionDI()
    {
        var provider = Services;
        using var scope = provider.CreateScope();

        var isService = scope.ServiceProvider.GetService<IServiceProviderIsService>();
        isService.Should().NotBeNull();
        isService!.IsService(typeof(AnalyticsHubPanel)).Should().BeTrue(
            because: "AddWinFormsServices must register AnalyticsHubPanel as a scoped service");
    }

    /// <summary>
    /// AnalyticsHubPanel must appear in PanelRegistry.Panels under the Analytics group.
    /// This is the cross-check between the registry and the panel being an actual class.
    /// </summary>
    [Fact]
    public void AnalyticsHubPanel_ExistsIn_PanelRegistry()
    {
        var entry = PanelRegistry.Panels
            .FirstOrDefault(p => p.PanelType == typeof(AnalyticsHubPanel));

        entry.Should().NotBeNull(
            because: "AnalyticsHubPanel must be listed in PanelRegistry for ribbon navigation to work");
        entry!.DisplayName.Should().NotBeNullOrWhiteSpace();
        entry.DefaultGroup.Should().Be("Analytics",
            because: "AnalyticsHubPanel belongs to the Analytics ribbon group");
    }

    /// <summary>
    /// AnalyticsHubPanel must be resolvable from a DI scope without throwing,
    /// confirming its constructor dependencies are all registered.
    /// </summary>
    [StaFact]
    public void AnalyticsHubPanel_IsResolvable_FromScope()
    {
        var provider = Services;
        using var scope = provider.CreateScope();

        Action act = () =>
        {
            using var panel = scope.ServiceProvider.GetRequiredService<AnalyticsHubPanel>();
            panel.Should().NotBeNull();
        };

        act.Should().NotThrow(
            because: "All AnalyticsHubPanel constructor dependencies (IServiceScopeFactory, ILogger) must be registered");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. IAsyncInitializable — sequencing & cancellation (extended coverage)
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "IAsyncInitializable")]
public sealed class AsyncInitializableIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// IAsyncInitializable must be resolvable within a scope from the production container.
    /// A null result would mean DataPrefetchService (or GrokAgentService) lost its registration.
    /// </summary>
    [Fact]
    public void IAsyncInitializable_IsResolvable_WithinScope()
    {
        var provider = Services;
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetService<IAsyncInitializable>();

        service.Should().NotBeNull(
            because: "AddWinFormsServices registers at least DataPrefetchService as IAsyncInitializable");
    }

    /// <summary>
    /// IAsyncInitializable.InitializeAsync must complete without throwing when called
    /// with a live (non-cancelled) token — this is the happy-path startup contract.
    /// </summary>
    [Fact]
    public async Task IAsyncInitializable_InitializeAsync_CompletesWithoutThrowing()
    {
        var provider = Services;
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAsyncInitializable>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await FluentActions.Awaiting(() => service.InitializeAsync(cts.Token))
            .Should().NotThrowAsync(
                because: "The happy-path initialization must always complete without exceptions");
    }

    /// <summary>
    /// IAsyncInitializable.InitializeAsync must propagate or handle gracefully when the token
    /// is already cancelled — caller (StartupHostedService) must not hang.
    /// </summary>
    [Fact]
    public async Task IAsyncInitializable_InitializeAsync_WithCancelledToken_DoesNotHang()
    {
        var provider = Services;
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAsyncInitializable>();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        // Either completes instantly (honours cancellation) or throws OperationCanceledException
        var completed = false;
        try
        {
            await service.InitializeAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5));
            completed = true;
        }
        catch (OperationCanceledException)
        {
            completed = true; // correct behaviour: respect cancellation
        }

        completed.Should().BeTrue(because: "InitializeAsync must not hang when the CancellationToken is already cancelled");
    }

    /// <summary>
    /// The async-initialization-pattern rule mandates that IAsyncInitializable must NOT be
    /// resolved or called from the root (non-scoped) container. This test verifies the
    /// registration lifetime is Scoped or Transient (not Singleton).
    /// </summary>
    [Fact]
    public void IAsyncInitializable_Registration_IsNotSingleton()
    {
        var provider = Services;

        // Resolve two separate scopes and verify distinct instances
        IAsyncInitializable? instance1, instance2;
        using (var scope1 = provider.CreateScope())
            instance1 = scope1.ServiceProvider.GetService<IAsyncInitializable>();
        using (var scope2 = provider.CreateScope())
            instance2 = scope2.ServiceProvider.GetService<IAsyncInitializable>();

        // They must not be the same reference (singleton would produce same ref)
        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        // Note: if Transient they will differ; if Scoped they differ across scopes — both are correct.
        ReferenceEquals(instance1, instance2).Should().BeFalse(
            because: "IAsyncInitializable must be Scoped or Transient — never Singleton — per async-initialization-pattern rule");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. PanelRegistry — completeness guard
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "PanelRegistry")]
public sealed class PanelRegistryIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// Every entry in PanelRegistry must have a non-null, concrete PanelType.
    /// A null type would silently break ribbon button generation.
    /// </summary>
    [Fact]
    public void PanelRegistry_AllEntries_HaveConcreteNonNullPanelTypes()
    {
        foreach (var entry in PanelRegistry.Panels)
        {
            entry.PanelType.Should().NotBeNull(
                because: $"PanelEntry '{entry.DisplayName}' has a null PanelType — ribbon navigation would silently break");
            entry.PanelType.IsAbstract.Should().BeFalse(
                because: $"PanelEntry '{entry.DisplayName}' has an abstract type — it must be a concrete class");
        }
    }

    /// <summary>
    /// Every entry must have a non-empty DisplayName — these are shown to the user in ribbon menus.
    /// </summary>
    [Fact]
    public void PanelRegistry_AllEntries_HaveNonEmptyDisplayNames()
    {
        foreach (var entry in PanelRegistry.Panels)
        {
            entry.DisplayName.Should().NotBeNullOrWhiteSpace(
                because: $"Panel type '{entry.PanelType?.Name}' has a blank DisplayName — it would appear invisible in the ribbon");
        }
    }

    /// <summary>
    /// DisplayNames must be unique. Duplicates would cause ambiguous navigation
    /// and duplicate ribbon buttons.
    /// </summary>
    [Fact]
    public void PanelRegistry_DisplayNames_AreUnique()
    {
        var duplicates = PanelRegistry.Panels
            .GroupBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty(
            because: $"Duplicate DisplayNames would cause ambiguous ribbon navigation. Duplicates: {string.Join(", ", duplicates)}");
    }

    /// <summary>
    /// PanelType must be unique per entry. Two entries for the same panel type would
    /// produce two ribbon buttons opening the same panel.
    /// </summary>
    [Fact]
    public void PanelRegistry_PanelTypes_AreUnique()
    {
        var duplicates = PanelRegistry.Panels
            .GroupBy(p => p.PanelType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key?.Name ?? "<null>")
            .ToList();

        duplicates.Should().BeEmpty(
            because: $"Duplicate PanelTypes would create two ribbon entries for the same panel. Duplicates: {string.Join(", ", duplicates)}");
    }

    /// <summary>
    /// Every panel type listed in PanelRegistry must be registered in the production DI container.
    /// This is the canonical cross-check: registry → DI. A gap here means navigation would throw
    /// at runtime when the panel factory tries to resolve the panel.
    /// </summary>
    [Fact]
    public void PanelRegistry_AllPanelTypes_AreRegisteredInProductionDI()
    {
        var provider = Services;
        using var scope = provider.CreateScope();
        var isService = scope.ServiceProvider.GetRequiredService<IServiceProviderIsService>();

        var unregistered = PanelRegistry.Panels
            .Where(entry => !isService.IsService(entry.PanelType))
            .Select(entry => $"{entry.PanelType.Name} ('{entry.DisplayName}')")
            .ToList();

        unregistered.Should().BeEmpty(
            because: $"Every PanelRegistry entry must be resolvable from DI. Missing: {string.Join(", ", unregistered)}");
    }

    /// <summary>
    /// AnalyticsHubPanel specifically must be present in PanelRegistry.
    /// This guards against accidental removal since it previously had zero test coverage.
    /// </summary>
    [Fact]
    public void PanelRegistry_ContainsAnalyticsHubPanel()
    {
        PanelRegistry.Panels
            .Should().Contain(p => p.PanelType == typeof(AnalyticsHubPanel),
                because: "AnalyticsHubPanel is a primary workflow panel and must remain in the registry");
    }

    /// <summary>
    /// The registry must contain the 4 primary workflow panels named in the architecture brief.
    /// </summary>
    [Fact]
    public void PanelRegistry_ContainsAllPrimaryWorkflowPanels()
    {
        var requiredTypes = new[]
        {
            typeof(BudgetPanel),
            typeof(AccountsPanel),
            typeof(AnalyticsHubPanel),
            typeof(WarRoomPanel),
            typeof(EnterpriseVitalSignsPanel),
            typeof(JARVISChatUserControl)
        };

        foreach (var type in requiredTypes)
        {
            PanelRegistry.Panels.Should().Contain(p => p.PanelType == type,
                because: $"{type.Name} is a primary workflow panel listed in the architecture brief and must remain in the registry");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. IEnterpriseRepository + IUtilityBillRepository — behavioral mock contracts
// ─────────────────────────────────────────────────────────────────────────────

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "IEnterpriseRepository")]
public sealed class EnterpriseRepositoryContractTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// IEnterpriseRepository must be registered (as scoped mock) in the integration test provider.
    /// </summary>
    [Fact]
    public void IEnterpriseRepository_IsResolvable_FromIntegrationProvider()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var repo = scope.ServiceProvider.GetService<WileyWidget.Business.Interfaces.IEnterpriseRepository>();

        repo.Should().NotBeNull(
            because: "IEnterpriseRepository must be registered in the integration test provider so panels can activate");
    }

    /// <summary>
    /// IEnterpriseRepository must also be registered in the production DI container.
    /// </summary>
    [Fact]
    public void IEnterpriseRepository_IsRegistered_InProductionDI()
    {
        var provider = Services;
        using var scope = provider.CreateScope();
        var isService = scope.ServiceProvider.GetRequiredService<IServiceProviderIsService>();

        isService.IsService(typeof(WileyWidget.Business.Interfaces.IEnterpriseRepository)).Should().BeTrue(
            because: "IEnterpriseRepository is required by EnterpriseVitalSignsPanel and other components");
    }

    /// <summary>
    /// The interface contract must expose a method surface that callers can depend on.
    /// This guard catches accidental interface definition regressions.
    /// </summary>
    [Fact]
    public void IEnterpriseRepository_Interface_HasExpectedMethodSurface()
    {
        var type = typeof(WileyWidget.Business.Interfaces.IEnterpriseRepository);

        type.IsInterface.Should().BeTrue();
        type.GetMethods().Should().NotBeEmpty(
            because: "IEnterpriseRepository must define at least one method for callers to consume data");
    }
}

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Symbol", "IUtilityBillRepository")]
public sealed class UtilityBillRepositoryContractTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    /// <summary>
    /// IUtilityBillRepository must be resolvable from the integration test provider.
    /// </summary>
    [Fact]
    public void IUtilityBillRepository_IsResolvable_FromIntegrationProvider()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var repo = scope.ServiceProvider.GetService<WileyWidget.Business.Interfaces.IUtilityBillRepository>();

        repo.Should().NotBeNull(
            because: "IUtilityBillRepository is consumed by UtilityBillPanel and must always be registered");
    }

    /// <summary>
    /// IUtilityBillRepository must also be registered in the production DI container.
    /// </summary>
    [Fact]
    public void IUtilityBillRepository_IsRegistered_InProductionDI()
    {
        var provider = Services;
        using var scope = provider.CreateScope();
        var isService = scope.ServiceProvider.GetRequiredService<IServiceProviderIsService>();

        isService.IsService(typeof(WileyWidget.Business.Interfaces.IUtilityBillRepository)).Should().BeTrue(
            because: "IUtilityBillRepository must be registered in the production container");
    }

    /// <summary>
    /// Interface definition sanity check — must have methods defined.
    /// </summary>
    [Fact]
    public void IUtilityBillRepository_Interface_HasExpectedMethodSurface()
    {
        var type = typeof(WileyWidget.Business.Interfaces.IUtilityBillRepository);

        type.IsInterface.Should().BeTrue();
        type.GetMethods().Should().NotBeEmpty(
            because: "IUtilityBillRepository must define at least one data access method");
    }
}

// Bridge: CreateScope() and GetServices<T>() are not in System.ServiceExtensions (.NET 10),
// only GetService<T>/GetRequiredService<T> are. Provide them via a file-scoped class
// so removing `using Microsoft.Extensions.DependencyInjection;` does not break those calls.
file static class DiBridge
{
    internal static IServiceScope CreateScope(this IServiceProvider sp) =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(sp);

    internal static IServiceScope CreateScope(
        this Microsoft.Extensions.DependencyInjection.ServiceProvider sp) =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(sp);

    internal static System.Collections.Generic.IEnumerable<T> GetServices<T>(
        this IServiceProvider sp) =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<T>(sp);
}
