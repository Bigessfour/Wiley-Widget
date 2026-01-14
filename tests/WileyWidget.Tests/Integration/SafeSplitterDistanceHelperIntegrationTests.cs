using System.Windows.Forms;
using FluentAssertions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.Utils;
using Xunit;

namespace WileyWidget.Tests.Integration;

/// <summary>
/// Integration test for SafeSplitterDistanceHelper, converted from .csx script.
/// Validates safe splitter distance handling at narrow widths without exceptions.
/// </summary>
public sealed class SafeSplitterDistanceHelperIntegrationTests : IDisposable
{
    private SfForm? _testForm;

    [WpfFact]
    public void SafeSplitterDistanceHelper_HandlesNarrowWidthsSafely()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";

        _testForm = new SfForm
        {
            Text = "Safe Splitter Regression",
            Width = 900,
            Height = 600
        };
        // Theme set globally in test setup - no per-control SetVisualStyle needed

        var split = new SplitContainer
        {
            Orientation = Orientation.Vertical,
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 50,  // Start with smaller min sizes
            Panel2MinSize = 50
        };

        SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(split, 400);
        SafeSplitterDistanceHelper.SetupProportionalResizing(split, 0.5);

        _testForm.Controls.Add(split);
        _testForm.Show();
        Application.DoEvents();

        // Act & Assert: Initial apply should succeed when width is sufficient
        var initialApplied = SafeSplitterDistanceHelper.TrySetSplitterDistance(split, 400);
        initialApplied.Should().BeTrue("Initial splitter distance should apply when space is available");

        // Resize form to below minimum width
        var requiredWidth = split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth;
        _testForm.Width = requiredWidth - 20; // force below combined min sizes
        _testForm.PerformLayout();
        Application.DoEvents();

        // Assert: When width is too small, helper should decline without throwing
        var declined = SafeSplitterDistanceHelper.TrySetSplitterDistance(split, 400);
        declined.Should().BeFalse("TrySetSplitterDistance should return false when space is insufficient");

        // Restore width and ensure helper can reapply safely
        _testForm.Width = requiredWidth + 60;
        _testForm.PerformLayout();
        Application.DoEvents();

        var reapplied = SafeSplitterDistanceHelper.TrySetSplitterDistance(split, 420);
        reapplied.Should().BeTrue("Splitter distance should reapply after width is restored");

        var maxAllowed = _testForm.ClientSize.Width - split.Panel2MinSize - split.SplitterWidth;
        split.SplitterDistance.Should().BeGreaterOrEqualTo(split.Panel1MinSize, "SplitterDistance respects Panel1MinSize");
        split.SplitterDistance.Should().BeLessOrEqualTo(maxAllowed, "SplitterDistance respects maximum bound");
    }

    public void Dispose()
    {
        _testForm?.Dispose();
    }
}
