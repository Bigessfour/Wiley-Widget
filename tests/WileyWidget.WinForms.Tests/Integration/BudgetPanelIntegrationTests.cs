using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class BudgetPanelIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [StaFact]
    public void BudgetPanel_TopContent_OrdersSummaryAboveFilters_AndReservesHeight()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider(new Dictionary<string, string?>
        {
            ["UI:IsUiTestHarness"] = "true"
        });
        using var scope = provider.CreateScope();
        using var form = new Form
        {
            Width = 1600,
            Height = 1000,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual
        };

        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scope.ServiceProvider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WileyWidget.WinForms.Controls.Base.ScopedPanelBase<BudgetViewModel>>>(scope.ServiceProvider);

        using var panel = new BudgetPanel(scopeFactory, logger)
        {
            Dock = DockStyle.Fill
        };

        form.Controls.Add(panel);
        form.Show();
        Application.DoEvents();
        panel.Show();
        Application.DoEvents();

        var summaryPanel = FindControl<Panel>(panel, "BudgetSummaryPanel");
        var filterPanel = FindControl<Panel>(panel, "BudgetFilterPanel");
        var splitContainer = FindControl<SplitContainerAdv>(panel, "BudgetMainSplitContainer");

        summaryPanel.Should().NotBeNull();
        filterPanel.Should().NotBeNull();
        splitContainer.Should().NotBeNull();

        summaryPanel!.Top.Should().BeLessThan(filterPanel!.Top);
        splitContainer!.Panel1MinSize.Should().BeGreaterThanOrEqualTo(summaryPanel.Height + filterPanel.Height);
    }

    private static TControl? FindControl<TControl>(Control root, string name)
        where TControl : Control
    {
        if (root is TControl match && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<TControl>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
