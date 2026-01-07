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

                // Act - inspect controls
                var split = chartPanel.Controls.OfType<SplitContainer>().FirstOrDefault();

                // Assert
                split.Should().NotBeNull("ChartPanel should use a SplitContainer to allow chart resizing");

                var chart = split!.Panel1.Controls.OfType<ChartControl>().FirstOrDefault();
                chart.Should().NotBeNull("Main chart control should be in SplitContainer.Panel1");
                chart!.Dock.Should().Be(DockStyle.Fill, "Main chart should fill its panel");

                var piePanel = split.Panel2.Controls.OfType<GradientPanelExt>().FirstOrDefault(p => p.Name == "Chart_Pie");
                piePanel.Should().NotBeNull("Pie panel should be in SplitContainer.Panel2");
                piePanel!.Dock.Should().Be(DockStyle.Fill, "Pie panel should fill its right-hand panel so the splitter controls sizing");

                split.Panel2MinSize.Should().BeGreaterOrEqualTo(ChartLayoutConstants.PiePanelMinWidth, "Pie panel min size should not be too small to render content");
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
