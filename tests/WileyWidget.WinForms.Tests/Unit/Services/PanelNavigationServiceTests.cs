using System;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class PanelNavigationServiceTests
{
    [StaFact]
    public void CreateMdiChild_DisablesChildCaptionButtons_ForTabbedMdiHosts()
    {
        using var owner = new Form { IsMdiContainer = true };
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new PanelNavigationService(owner, provider, NullLogger<PanelNavigationService>.Instance);

        var createMdiChild = typeof(PanelNavigationService).GetMethod(
            "CreateMDIChild",
            BindingFlags.Instance | BindingFlags.NonPublic);

        createMdiChild.Should().NotBeNull();

        using var host = (Form)createMdiChild!.Invoke(service, new object?[] { "Payments", new UserControl() })!;

        host.MdiParent.Should().Be(owner);
        host.FormBorderStyle.Should().Be(FormBorderStyle.None);
        host.ShowInTaskbar.Should().BeFalse();
        host.ShowIcon.Should().BeFalse();
        host.ControlBox.Should().BeFalse();
        host.MinimizeBox.Should().BeFalse();
        host.MaximizeBox.Should().BeFalse();
    }

    [StaFact]
    public void ApplyMinimumTopInset_ReservesEnoughTopPadding_ForRibbonHosts()
    {
        var panel = new Panel
        {
            Padding = Padding.Empty
        };

        var applyMinimumTopInset = typeof(PanelNavigationService).GetMethod(
            "ApplyMinimumTopInset",
            BindingFlags.Static | BindingFlags.NonPublic);

        applyMinimumTopInset.Should().NotBeNull();

        applyMinimumTopInset!.Invoke(null, new object[] { panel });

        panel.Padding.Top.Should().BeGreaterThanOrEqualTo(20);
        panel.Padding.Left.Should().Be(0);
        panel.Padding.Right.Should().Be(0);
        panel.Padding.Bottom.Should().Be(0);
    }
}
