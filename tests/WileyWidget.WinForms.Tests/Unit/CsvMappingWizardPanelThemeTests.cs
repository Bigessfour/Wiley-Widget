using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Syncfusion.WinForms.ListView;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit;

[Collection("SyncfusionTheme")]
public class CsvMappingWizardPanelThemeTests
{
    [WinFormsFact]
    public void CsvMappingWizardPanel_UsesThemedSelectors_AndBindsReadableDefaults()
    {
        var themeService = new Mock<IThemeService>();
        themeService.SetupGet(service => service.CurrentTheme).Returns("Office2019Colorful");

        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance, themeService.Object);

        var panel = new CsvMappingWizardPanel(NullLogger.Instance, factory)
        {
            Dock = DockStyle.Fill,
        };
        using var hostForm = new Form
        {
            Width = 1200,
            Height = 800,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };
        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();

        var csvPath = Path.Combine(Path.GetTempPath(), $"csv-mapping-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "Account,Description,Budgeted,Actual\r\n100-10,Main street,1000,900");

        try
        {
            panel.Initialize(csvPath, new[] { "Utility Fund" }, defaultFiscalYear: 2026);
            Application.DoEvents();

            var accountCombo = GetPrivateField<SfComboBox>(panel, "_cbAccount");
            var entityCombo = GetPrivateField<SfComboBox>(panel, "_cbEntity");
            var fiscalYearCombo = GetPrivateField<SfComboBox>(panel, "_cbFiscalYear");

            accountCombo.ThemeName.Should().Be("Office2019Colorful",
                "mapping selectors should follow the active Syncfusion theme rather than rendering as un-themed native combo boxes");
            entityCombo.Text.Should().Be("Utility Fund",
                "entity binding should show the user-facing entity name instead of a stale or blank selector value");
            fiscalYearCombo.SelectedItem.Should().Be(2026,
                "the fiscal year selector should bind to the requested default year after initialization");
        }
        finally
        {
            try
            {
                File.Delete(csvPath);
            }
            catch
            {
            }
        }
    }

    private static TControl GetPrivateField<TControl>(object instance, string fieldName)
        where TControl : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"field {fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as TControl;
        value.Should().NotBeNull($"field {fieldName} should be initialized");
        return value!;
    }
}