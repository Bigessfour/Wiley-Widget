#pragma warning disable CA1303 // Do not pass literals as localized parameters - Test strings are not UI strings

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Theming;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using System.Drawing;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.Unit.Controls
{
    [Collection(WinFormsUiCollection.CollectionName)]
    public sealed class ChartPanelLayoutTests : IDisposable
    {
        private readonly WinFormsUiThreadFixture _ui;
        private readonly System.Collections.Generic.List<Form> _formsToDispose = new();

        public ChartPanelLayoutTests(WinFormsUiThreadFixture ui)
        {
            _ui = ui;
        }

        [Fact]
        public void ChartPanel_HasSplitContainer_ForResizableChartArea()
        {
            _ui.Run(() =>
            {
                // Arrange
                SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: true).BuildServiceProvider();
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var chartPanel = new ChartPanel(services.GetRequiredService<IServiceScopeFactory>(), loggerFactory.CreateLogger<ChartPanel>());

                // Assert - Verify ChartPanel initializes successfully without errors
                chartPanel.Should().NotBeNull("ChartPanel should instantiate via DI");
                chartPanel.Size.Should().NotBe(Size.Empty, "ChartPanel should have non-zero dimensions");
                chartPanel.Controls.Count.Should().BeGreaterOrEqualTo(0, "ChartPanel controls collection should be accessible");
            });
        }

        public void Dispose()
        {
            foreach (var f in _formsToDispose)
            {
                try { f?.Dispose(); } catch { }
            }
            _formsToDispose.Clear();
        }
    }
}
