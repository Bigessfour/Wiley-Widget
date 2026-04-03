using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Diagnostics;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Controls;

public class ScopedPanelBaseTests
{
    private class DummyViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    private class TestPanel : ScopedPanelBase<DummyViewModel>
    {
        public TestPanel() : base(new DummyViewModel(), NullLogger.Instance)
        {
        }

        public void ExposedForceFullLayout() => ForceFullLayout();
    }

    [StaFact]
    public void ForceFullLayout_RespectsMinimumSizeForVisibleControl()
    {
        // Arrange
        using var host = new Form { Size = new Size(400, 300) };
        using var panel = new TestPanel { Dock = DockStyle.Fill };
        using var button = new Button
        {
            Name = "TestButton",
            Size = new Size(0, 0), // Zero size
            Visible = true,
            Dock = DockStyle.Top,
            Text = "Test",
            MinimumSize = new Size(30, 20)
        };

        panel.Controls.Add(button);
        host.Controls.Add(panel);
        host.Show();

        // Act
        panel.ExposedForceFullLayout();

        // Assert
        button.Width.Should().BeGreaterThanOrEqualTo(30);
        button.Height.Should().BeGreaterThanOrEqualTo(20);

        var diagnostics = PanelLayoutDiagnostics.Capture(panel);
        diagnostics.ZeroSizedVisibleControls.Should().NotContain(f => f.Identifier == "TestButton");
    }

    [StaFact]
    public void ForceFullLayout_RespectsMinimumSizeForNestedVisibleControl()
    {
        // Arrange
        using var host = new Form { Size = new Size(400, 300) };
        using var panel = new TestPanel { Dock = DockStyle.Fill };
        using var container = new Panel { Name = "Container", Dock = DockStyle.Fill, Visible = true };
        using var button = new Button
        {
            Name = "NestedButton",
            Size = new Size(0, 0), // Zero size
            Visible = true,
            Dock = DockStyle.Top,
            Text = "Nested",
            MinimumSize = new Size(30, 20)
        };

        container.Controls.Add(button);
        panel.Controls.Add(container);
        host.Controls.Add(panel);
        host.Show();

        // Act
        panel.ExposedForceFullLayout();

        // Assert
        button.Width.Should().BeGreaterThanOrEqualTo(30);
        button.Height.Should().BeGreaterThanOrEqualTo(20);
    }

    [StaFact]
    public void VisibleChanged_ExpandsZeroSizedVisibleControl()
    {
        // Arrange
        using var host = new Form { Size = new Size(400, 300) };
        using var panel = new TestPanel { Dock = DockStyle.Fill, Visible = false };
        using var button = new Button
        {
            Name = "VisibleChangedButton",
            Size = new Size(0, 0),
            Visible = true,
            Dock = DockStyle.Top,
            Text = "Visible",
            MinimumSize = Size.Empty
        };

        panel.Controls.Add(button);
        host.Controls.Add(panel);
        host.Show();

        // Act
        panel.Visible = true;
        Application.DoEvents();

        // Assert
        button.MinimumSize.Width.Should().BeGreaterThanOrEqualTo(30);
        button.MinimumSize.Height.Should().BeGreaterThanOrEqualTo(20);
        button.Width.Should().BeGreaterThanOrEqualTo(30);
        button.Height.Should().BeGreaterThanOrEqualTo(20);
    }
}
