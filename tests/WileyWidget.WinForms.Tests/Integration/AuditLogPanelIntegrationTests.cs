using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class AuditLogPanelIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static AuditLogPanel CreatePanel(IServiceProvider services)
    {
        var viewModel = SPSE.GetRequiredService<AuditLogViewModel>(services);
        var factory = SPSE.GetRequiredService<SyncfusionControlFactory>(services);
        return new AuditLogPanel(viewModel, factory);
    }

    private static T? FindDescendantByAccessibleName<T>(Control root, string accessibleName) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T typed && string.Equals(typed.AccessibleName, accessibleName, StringComparison.Ordinal))
            {
                return typed;
            }

            var nested = FindDescendantByAccessibleName<T>(child, accessibleName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    [StaFact]
    public void Panel_ConstructsWithoutException_WithValidDependencies()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var act = () =>
        {
            using var panel = CreatePanel(scope.ServiceProvider);
        };

        act.Should().NotThrow();
    }

    [StaFact]
    public void Panel_ContainsCoreAuditControls_WhenConstructed()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);

        var grid = FindDescendantByAccessibleName<Control>(panel, "Audit log entries grid");
        var filters = FindDescendantByAccessibleName<Control>(panel, "Audit log filters");
        var chart = FindDescendantByAccessibleName<Control>(panel, "Audit events chart");

        grid.Should().NotBeNull("audit grid must be present for entry browsing");
        filters.Should().NotBeNull("filter area must be present for narrowing entries");
        chart.Should().NotBeNull("chart must be present for audit trend visualization");
    }

    [StaFact]
    public async Task ValidateAsync_ReturnsWarning_WhenNoEntriesAvailable()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);

        await panel.LoadAsync(CancellationToken.None);
        var result = await panel.ValidateAsync(CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Any(error => error.FieldName == "Data" && error.Message.Contains("No audit entries", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [StaFact]
    public void FocusFirstError_DoesNotThrow()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);

        var act = () => panel.FocusFirstError();

        act.Should().NotThrow();
    }
}
