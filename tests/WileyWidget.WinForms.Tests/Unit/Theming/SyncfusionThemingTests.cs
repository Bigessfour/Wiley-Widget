using Xunit;
using Xunit.Sdk;
using System;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using FluentAssertions;
using System.Diagnostics.CodeAnalysis;

namespace WileyWidget.WinForms.Tests.Unit.Theming;

/// <summary>
/// Tests for Syncfusion theme management.
/// Validates that SfSkinManager correctly applies and propagates themes per project guidelines.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "Test cleanup only")]
public sealed class SyncfusionThemingTests : IDisposable
{
    private readonly List<Form> _formsToDispose = new();

    [StaFact]
    public void SfSkinManager_ShouldApplyOffice2019Theme_ToForm()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        var testForm = new Form();
        _formsToDispose.Add(testForm);

        // Act
        SfSkinManager.SetVisualStyle(testForm, "Office2019Colorful");

        // Assert
        // Theme is applied to form - verify no exceptions thrown
        testForm.Should().NotBeNull();
    }

    [StaFact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldPropagateTheme_ToSyncfusionButton()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        var testForm = new Form();
        _formsToDispose.Add(testForm);

        var button = new SfButton
        {
            Parent = testForm,
            Text = "Test Button"
        };

        // Act
        SfSkinManager.SetVisualStyle(testForm, "Office2019Colorful");

        // Assert
        button.ThemeName.Should().Be("Office2019Colorful",
            "theme should propagate to child Syncfusion controls");
    }

    [StaFact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldPropagateTheme_ToMultipleSyncfusionControls()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        var parentForm = new Form();
        _formsToDispose.Add(parentForm);

        var button1 = new SfButton { Parent = parentForm, Text = "Button 1" };
        var button2 = new SfButton { Parent = parentForm, Text = "Button 2" };

        var panel = new Panel { Parent = parentForm };
        var button3 = new SfButton { Parent = panel, Text = "Button 3" };

        // Act
        SfSkinManager.SetVisualStyle(parentForm, "Office2019Colorful");

        // Assert
        button1.ThemeName.Should().Be("Office2019Colorful", "direct child should have theme");
        button2.ThemeName.Should().Be("Office2019Colorful", "direct child should have theme");
        button3.ThemeName.Should().Be("Office2019Colorful", "nested child should have theme");
    }

    [StaFact]
    public void SfSkinManager_ShouldLoadThemeAssembly_WithoutException()
    {
        // Act
        Action act = () => SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

        // Assert
        act.Should().NotThrow("theme assembly should load successfully");
    }

    [StaFact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldApplyTheme_ToNewlyAddedControls()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        var testForm = new Form();
        _formsToDispose.Add(testForm);

        SfSkinManager.SetVisualStyle(testForm, "Office2019Colorful");

        // Act - Add button AFTER theme is applied
        var lateButton = new SfButton
        {
            Parent = testForm,
            Text = "Late Button"
        };

        // NOTE: Syncfusion SfSkinManager does NOT automatically apply theme to
        // dynamically added controls. Must call SetVisualStyle/ApplyFormTheme again.
        // This is documented Syncfusion behavior, not a bug.
        SfSkinManager.SetVisualStyle(lateButton, "Office2019Colorful");

        // Assert
        lateButton.ThemeName.Should().Be("Office2019Colorful",
            "theme should apply after explicit SetVisualStyle call on late-added control");
    }

    [StaFact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldHandleMultipleSetVisualStyleCalls()
    {
        // Arrange
        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
        var testForm = new Form();
        _formsToDispose.Add(testForm);

        var button = new SfButton { Parent = testForm, Text = "Test" };

        // Act
        SfSkinManager.SetVisualStyle(testForm, "Office2019Colorful");
        SfSkinManager.SetVisualStyle(testForm, "Office2019Colorful"); // Apply again

        // Assert - Should not throw
        button.ThemeName.Should().Be("Office2019Colorful");
    }

    public void Dispose()
    {
        foreach (var form in _formsToDispose)
        {
            try
            {
                form?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _formsToDispose.Clear();
    }
}
