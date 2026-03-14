using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Factories;

public sealed class SyncfusionControlFactoryTests
{
    [StaFact]
    public void CreateSfDataGrid_UsesLatestThemeServiceTheme_ForEachControl()
    {
        var themeService = new MutableThemeService("Office2019Colorful");
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance, themeService);

        var firstGrid = factory.CreateSfDataGrid();

        firstGrid.ThemeName.Should().Be("Office2019Colorful");

        themeService.CurrentTheme = "Office2016Black";

        var secondGrid = factory.CreateSfDataGrid();

        secondGrid.ThemeName.Should().Be("Office2016Black");
    }

    [StaFact]
    public void ApplyThemeToAllControls_ReplaysThemeToTextEntryAndRibbonControls()
    {
        using var host = new Panel();
        using var textBox = new TextBoxExt { ThemeName = "Office2016Black" };
        using var comboBox = new SfComboBox { ThemeName = "Office2016Black" };
        using var ribbon = new RibbonControlAdv { ThemeName = "Office2016Black" };

        host.Controls.Add(textBox);
        host.Controls.Add(comboBox);
        host.Controls.Add(ribbon);

        SyncfusionControlFactory.ApplyThemeToAllControls(host, "Office2019Colorful");

        textBox.ThemeName.Should().Be("Office2019Colorful");
        comboBox.ThemeName.Should().Be("Office2019Colorful");
        ribbon.ThemeName.Should().Be("Office2019Colorful");
    }

    private sealed class MutableThemeService : IThemeService
    {
        public MutableThemeService(string currentTheme)
        {
            CurrentTheme = currentTheme;
        }

        public event EventHandler<string>? ThemeChanged;

        public string CurrentTheme { get; set; }

        public bool IsDark => CurrentTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase) ||
                              CurrentTheme.Contains("Black", StringComparison.OrdinalIgnoreCase);

        public void ApplyTheme(string themeName)
        {
            CurrentTheme = themeName;
            ThemeChanged?.Invoke(this, themeName);
        }

        public void ReapplyCurrentTheme()
        {
            ThemeChanged?.Invoke(this, CurrentTheme);
        }
    }
}
