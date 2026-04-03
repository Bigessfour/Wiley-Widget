using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using WileyWidget.WinForms.Controls.Supporting;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Controls;

public sealed class PanelHeaderTests
{
    [StaFact]
    public void InitializeComponent_UsesExtraTopInset_ForRibbonSpacing()
    {
        using var header = new PanelHeader();

        header.Height.Should().BeGreaterThanOrEqualTo(60);
        header.Padding.Top.Should().Be(12);

        var titleLabel = FindDescendant(header, control => string.Equals(control.AccessibleName, "Header title", StringComparison.Ordinal));
        titleLabel.Should().NotBeNull();
        titleLabel!.Top.Should().BeGreaterThanOrEqualTo(12);
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
