#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration.Mocks;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests.Integration;

// Why null for sealed tools:
//   GrokAgentService, RateScenarioTools, and BudgetForecastTools are all sealed and
//   cannot be subclassed for mocking. WarRoomViewModel degrades gracefully: RunScenarioAsync
//   uses a local regex parser when they are null; ExportForecastAsync sets "not available";
//   LoadAsync completes trivially. MockExcelExportService covers the export code path.

[Collection("IntegrationTests")]
public class WarRoomPanelIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _out;

    private const string ValidInput = "Raise water rates 12% and inflation is 4% for 5 years";

    public WarRoomPanelIntegrationTests(ITestOutputHelper output, IntegrationTestFixture fixture) : base(fixture) => _out = output;

    private static WarRoomViewModel CreateVm() =>
        new(grokService:         null,   // sealed — local regex parser fallback
            rateScenarioTools:   null,   // sealed — graceful degradation
            budgetForecastTools: null,   // sealed — "not available" status on export
            excelExportService:  new MockExcelExportService());

    // ─── Sanity ───────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Panel", "WarRoom")]
    public void Test_WarRoom_TestDiscovery() =>
        _out.WriteLine("🔍 WarRoomPanel integration test discovery OK");

    // ─── RunScenario: local fallback populates Projections + DepartmentImpacts ─
    // Benjamin: round-trip — valid input regex path runs end-to-end with all
    // upstream services null; local computation produces deterministic results.

    [WinFormsFact]
    [Trait("Category", "Integration")]
    [Trait("Panel", "WarRoom")]
    public async Task WarRoomViewModel_RunScenario_FallbackParser_PopulatesCollections()
    {
        var vm = CreateVm();
        vm.ScenarioInput = ValidInput;

        await vm.RunScenarioCommand.ExecuteAsync(null);

        // Harper: MVVM — state transitions match the fallback path in RunScenarioAsync.
        vm.HasResults.Should().BeTrue("fallback parser must set HasResults = true");
        vm.Projections.Should().NotBeEmpty("local parser must populate Projections");
        vm.DepartmentImpacts.Should().NotBeEmpty("local parser must populate DepartmentImpacts");
        vm.StatusMessage.Should().ContainEquivalentOf("complete", "status confirms analysis finished");
    }

    // ─── Export: null BudgetForecastTools → graceful status, no throw ─────────
    // Lucas: UI safety — missing BudgetForecastTools must never throw; the command
    // must surface a "not available" message and return cleanly.

    [WinFormsFact]
    [Trait("Category", "Integration")]
    [Trait("Panel", "WarRoom")]
    public async Task WarRoomViewModel_Export_NullBudgetTools_SetsGracefulStatus()
    {
        var vm = CreateVm();
        await vm.ExportForecastCommand.ExecuteAsync(null);

        vm.StatusMessage.Should()
            .ContainEquivalentOf("not available",
                "null BudgetForecastTools must produce a graceful degradation message, not a crash");
    }

    // ─── ScenarioInput property round-trips correctly ─────────────────────────
    // Harper: MVVM — two-way binding contract: setter/getter must preserve the value.

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Panel", "WarRoom")]
    public void WarRoomViewModel_ScenarioInput_RoundTrips()
    {
        var vm = CreateVm();
        vm.ScenarioInput = ValidInput;
        vm.ScenarioInput.Should().Be(ValidInput);
    }

    // ─── LazyLoad: panel activates without exception with null services ────────
    // Lucas: UI safety — WarRoomPanel.LoadAsync must complete cleanly even when all
    // optional upstream services (GrokAgentService, tools) are null.

    [StaFact]
    [Trait("Category", "Integration")]
    [Trait("Panel", "WarRoom")]
    public async Task WarRoomPanel_LoadAsync_CompletesWithoutException()
    {
        var services = new ServiceCollection();
        services.AddScoped<WarRoomViewModel>(_ => CreateVm());
        var provider = services.BuildServiceProvider();
        var scopeFactory = SPSE.GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.Logging.Abstractions
            .NullLogger<ScopedPanelBase<WarRoomViewModel>>.Instance;

        using var form = new Form();
        using var panel = new WarRoomPanel(scopeFactory, logger);
        form.Controls.Add(panel);
        form.CreateControl();
        form.Show();
        Application.DoEvents();

        var ex = await Record.ExceptionAsync(() => panel.LoadAsync(System.Threading.CancellationToken.None));
        IntegrationTestServices.TryCaptureScreenshot(form, "WarRoomPanel_LoadAsync");
        ex.Should().BeNull("LoadAsync must not throw when optional services are null");

        _out.WriteLine("🎉 WarRoomPanel full integration test complete (graceful degradation verified)");
    }
}
