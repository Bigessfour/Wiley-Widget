using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Trait("Category", "Unit")]
[Collection(WinFormsUiCollection.CollectionName)]
public class UiModeValidationTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public UiModeValidationTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void MainForm_Construct_WithInvalidUIModeAndInconsistentFlags_DoesNotThrow()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UIMode"] = "NotARealMode",
                    ["UI:UseDockingManager"] = "false",
                    ["UI:UseMdiMode"] = "false",
                    ["UI:UseTabbedMdi"] = "false",
                    ["UI:IsUiTestHarness"] = "true"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var form = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            Assert.False(form.IsMdiContainer);
        });
    }
}
