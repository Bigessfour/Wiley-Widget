using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.WinForms.Forms;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Trait("Category", "Unit")]
public class UiModeValidationTests
{
    private static void RunInSta(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
        {
            throw captured;
        }
    }

    [Fact]
    public void MainForm_Construct_WithInvalidUIModeAndInconsistentFlags_DoesNotThrow()
    {
        RunInSta(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UIMode"] = "NotARealMode",
                    ["UI:UseDockingManager"] = "false",
                    ["UI:UseMdiMode"] = "false",
                    ["UI:UseTabbedMdi"] = "true"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var form = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance);

            Assert.False(form.IsMdiContainer);
        });
    }
}
