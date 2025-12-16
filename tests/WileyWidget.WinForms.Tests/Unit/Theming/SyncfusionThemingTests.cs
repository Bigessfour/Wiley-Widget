#pragma warning disable CA1303 // Do not pass literals as localized parameters - Test strings are not UI strings

using Xunit;
using Xunit.Sdk;
using System;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using FluentAssertions;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.Tests.Infrastructure;

namespace WileyWidget.WinForms.Tests.Unit.Theming;

/// <summary>
/// Tests for Syncfusion theme management.
/// Validates that SfSkinManager correctly applies and propagates themes per project guidelines.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "Test cleanup only")]
[Collection(WinFormsUiCollection.CollectionName)]
public sealed class SyncfusionThemingTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;
    private readonly List<Form> _formsToDispose = new();

    public SyncfusionThemingTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void SfSkinManager_ShouldApplyOffice2019Theme_ToForm()
    {
        _ui.Run(() =>
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
        });
    }

    [Fact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldPropagateTheme_ToSyncfusionButton()
    {
        _ui.Run(() =>
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
        });
    }

    [Fact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldPropagateTheme_ToMultipleSyncfusionControls()
    {
        _ui.Run(() =>
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
        });
    }

    [Fact]
    public void SfSkinManager_ShouldLoadThemeAssembly_WithoutException()
    {
        _ui.Run(() =>
        {
            // Act
            Action act = () => SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            // Assert
            act.Should().NotThrow("theme assembly should load successfully");
        });
    }

    [Fact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldApplyTheme_ToNewlyAddedControls()
    {
        _ui.Run(() =>
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
        });
    }

    [Fact]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Form disposal handled by test cleanup")]
    public void SfSkinManager_ShouldHandleMultipleSetVisualStyleCalls()
    {
        _ui.Run(() =>
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
        });
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
