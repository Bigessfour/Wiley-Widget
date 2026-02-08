using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.Tests.Integration;

namespace WileyWidget.WinForms.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class HelpersIntegrationTests
{
    [StaFact]
    public void UIHelper_ShowMessageOnUI_DisplaysMessageBox()
    {
        // Arrange
        using var form = new Form();
        form.Show(); // Need to show form for handle creation
        var logger = Mock.Of<ILogger>();

        // Act & Assert
        FluentActions.Invoking(() =>
            UIHelper.ShowMessageOnUI(form, "Test message", "Test title"))
            .Should().NotThrow();
    }

    [StaFact]
    public void UIHelper_ShowErrorOnUI_DisplaysErrorMessage()
    {
        // Arrange
        using var form = new Form();
        form.Show();
        var logger = Mock.Of<ILogger>();

        // Act & Assert
        FluentActions.Invoking(() =>
            UIHelper.ShowErrorOnUI(form, "Test error", "Error title", logger))
            .Should().NotThrow();
    }

    [StaFact]
    public void UIThreadHelper_ExecuteOnUIThread_ExecutesActionOnUIThread()
    {
        // Arrange
        using var form = new Form();
        form.Show();
        var executed = false;

        // Act
        WileyWidget.WinForms.Utils.UIThreadHelper.ExecuteOnUIThread(form, () => executed = true);

        // Assert
        executed.Should().BeTrue();
    }

    [StaFact]
    public async Task UIThreadHelper_ExecuteOnUIThreadAsync_ExecutesAsyncAction()
    {
        // Arrange
        using var form = new Form();
        form.Show();
        var executed = false;

        // Act
        await WileyWidget.WinForms.Utils.UIThreadHelper.ExecuteOnUIThreadAsync(form, async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
    }

    [StaFact]
    public void UIThreadHelper_SafeInvoke_HandlesNullControl()
    {
        // Arrange
        Control? control = null;
        var executed = false;

        // Act
        control.SafeInvoke(() => executed = true);

        // Assert
        executed.Should().BeFalse(); // Should not execute due to null check
    }

    [StaFact]
    public void UIThreadHelper_SafeInvoke_ExecutesOnUIThread()
    {
        // Arrange
        using var form = new Form();
        form.Show();
        var executed = false;

        // Act
        form.SafeInvoke(() => executed = true);

        // Assert
        executed.Should().BeTrue();
    }
}
