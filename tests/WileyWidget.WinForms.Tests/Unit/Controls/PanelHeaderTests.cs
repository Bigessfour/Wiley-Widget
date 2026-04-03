using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Utilities;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Controls;

public sealed class PanelHeaderTests
{
    [StaFact]
    public void InitializeComponent_UsesExtraTopInset_ForRibbonSpacing()
    {
        using var header = new PanelHeader();

        header.Height.Should().Be(PanelHeader.DefaultHeight);
        header.Height.Should().Be(LayoutTokens.HeaderHeight);
        header.Padding.Top.Should().Be(12);

        var titleLabel = FindDescendant(header, control => string.Equals(control.AccessibleName, "Header title", StringComparison.Ordinal));
        titleLabel.Should().NotBeNull();
        titleLabel!.Dock.Should().Be(DockStyle.Fill);
        titleLabel!.AccessibleName.Should().Be("Header title");
    }

    [StaFact]
    public void HeaderHeightToken_LeavesEnoughSpace_ForSharedPanelHeaderContract()
    {
        using var header = new PanelHeader();

        LayoutTokens.HeaderHeight.Should().BeGreaterThanOrEqualTo(header.Height);
        header.MinimumSize.Height.Should().Be(PanelHeader.DefaultHeight);
        (header.Height - header.Padding.Vertical).Should().BeGreaterThan(0);
    }

    private static Control? FindDescendant(Control root, Func<Control, bool> predicate)
    {
        foreach (Control child in root.Controls)
        {
            if (predicate(child))
            {
                return child;
            }

            var match = FindDescendant(child, predicate);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
