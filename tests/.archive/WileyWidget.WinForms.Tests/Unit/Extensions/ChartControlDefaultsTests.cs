using System.Drawing.Drawing2D;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Extensions;

[Collection(WinFormsUiCollection.CollectionName)]
public sealed class ChartControlDefaultsTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public ChartControlDefaultsTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [StaFact]
    [Trait("Category", "Chart")]
    public void Apply_SetsBaselineProperties_AndPrintDocumentIsAccessible()
    {
        _ui.Run(() =>
        {
            using var chart = new ChartControl();
            chart.CreateControl();

            ChartControlDefaults.Apply(chart);

            Assert.Equal(SmoothingMode.AntiAlias, chart.SmoothingMode);
            Assert.Equal(5, chart.ElementsSpacing);
            Assert.Equal(ChartBorderSkinStyle.None, chart.BorderAppearance.SkinStyle);
            Assert.True(chart.ShowToolTips);

            var doc = ChartControlPrinting.TryGetPrintDocument(chart);
            Assert.NotNull(doc);
        });
    }

    [StaFact]
    [Trait("Category", "Chart")]
    public void Apply_AllowsSparklineProfile()
    {
        _ui.Run(() =>
        {
            using var chart = new ChartControl();
            chart.CreateControl();

            ChartControlDefaults.Apply(chart, new ChartControlDefaults.Options
            {
                SmoothingMode = SmoothingMode.HighQuality,
                ElementsSpacing = 2,
                ShowToolTips = false,
                EnableZooming = false,
                EnableAxisScrollBar = false,
            });

            Assert.Equal(SmoothingMode.HighQuality, chart.SmoothingMode);
            Assert.Equal(2, chart.ElementsSpacing);
            Assert.False(chart.ShowToolTips);
        });
    }
}
