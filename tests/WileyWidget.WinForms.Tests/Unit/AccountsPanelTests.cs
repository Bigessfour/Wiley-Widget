using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit;

[Collection("SyncfusionTheme")]
public class AccountsPanelTests
{
    [WinFormsFact]
    public void AccountsPanel_UsesStructuredHeaderLayout_AndReadableAccountNumberColumn()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.AccountsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new AccountsPanel(viewModel, factory)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var header = GetPrivateField<Control>(panel, "_header");
        var content = GetPrivateField<TableLayoutPanel>(panel, "_content");
        var layout = GetPrivateField<TableLayoutPanel>(panel, "_layout");
        var grid = GetPrivateField<SfDataGrid>(panel, "_accountsGrid");

        header.Should().NotBeNull();
        content.Should().NotBeNull();
        layout.Should().NotBeNull();
        grid.Should().NotBeNull();

        content.Controls.Contains(header!).Should().BeTrue("the panel header should participate in the root table layout instead of overlaying the body");
        content.GetRow(header!).Should().Be(0);
        content.Controls.Contains(layout!).Should().BeTrue("the body layout should occupy the content row below the header");
        content.GetRow(layout!).Should().Be(1);

        layout.Top.Should().BeGreaterThanOrEqualTo(header.Bottom,
            "the body should render below the header so the top of the panel is not clipped");

        var accountNumberColumn = grid!.Columns.FirstOrDefault(column => string.Equals(column.MappingName, "AccountNumber", StringComparison.Ordinal));
        accountNumberColumn.Should().NotBeNull();
        accountNumberColumn!.Width.Should().BeGreaterThanOrEqualTo(140,
            "account numbers should have enough width to display the full value without truncation");
    }

    [WinFormsFact]
    public void AccountEditPanel_NormalizesCanonicalMunicipalAccountNumber_WhenControlsSync()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.AccountsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);
        var imageService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<DpiAwareImageService>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new AccountEditPanel(viewModel, factory, imageService)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var accountNumberTextBox = FindControl<TextBoxExt>(panel, "txtAccountNumber");
        accountNumberTextBox.Text = "4051";

        var syncMethod = panel.GetType().GetMethod("SyncEditModelFromControls", BindingFlags.Instance | BindingFlags.NonPublic);
        syncMethod.Should().NotBeNull();
        syncMethod!.Invoke(panel, null);

        var editModel = GetPrivateField<MunicipalAccountEditModel>(panel, "_editModel");
        editModel.AccountNumber.Should().Be("405.10");
        accountNumberTextBox.Text.Should().Be("405.10");
    }

    [WinFormsFact]
    public void AccountEditPanel_FundSelectionBinding_UsesFundIdForEntityBackedFunds()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.AccountsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);
        var imageService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<DpiAwareImageService>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new AccountEditPanel(viewModel, factory, imageService)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var fundCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(panel, "cmbFund");
        var editModel = GetPrivateField<MunicipalAccountEditModel>(panel, "_editModel");
        var bindingSource = GetPrivateField<BindingSource>(panel, "_bindingSource");
        var bindMethod = panel.GetType().GetMethod("BindFundSelection", BindingFlags.Instance | BindingFlags.NonPublic);

        bindMethod.Should().NotBeNull();

        fundCombo.DisplayMember = "Name";
        fundCombo.ValueMember = "Id";
        fundCombo.DataSource = new[] { new Fund { Id = 7, Name = "General Fund" } };
        bindMethod!.Invoke(panel, new object[] { true });

        editModel.FundId = 7;
        bindingSource.ResetBindings(false);
        Application.DoEvents();

        fundCombo.SelectedValue.Should().Be(7,
            "entity-backed fund lists should track FundId so the displayed selection matches the persisted fund reference");
    }

    [WinFormsFact]
    public void AccountEditPanel_ComboThemeReapply_UsesActiveTheme()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.AccountsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);
        var imageService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<DpiAwareImageService>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new AccountEditPanel(viewModel, factory, imageService)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();

        var fundCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(panel, "cmbFund");
        var typeCombo = FindControl<Syncfusion.WinForms.ListView.SfComboBox>(panel, "cmbType");
        var reapplyMethod = panel.GetType().GetMethod("ReapplyComboTheme", BindingFlags.Instance | BindingFlags.NonPublic);

        reapplyMethod.Should().NotBeNull();

        fundCombo.DisplayMember = "Name";
        fundCombo.ValueMember = "Id";
        fundCombo.DataSource = new[] { new Fund { Id = 9, Name = "Water Fund" } };
        fundCombo.SelectedValue = 9;

        typeCombo.DataSource = Enum.GetValues(typeof(AccountType));
        typeCombo.SelectedItem = AccountType.Cash;

        reapplyMethod!.Invoke(panel, new object?[] { fundCombo });
        reapplyMethod.Invoke(panel, new object?[] { typeCombo });

        Application.DoEvents();
        Application.DoEvents();

        GetThemeName(fundCombo).Should().Be("Office2019Colorful");
        GetThemeName(typeCombo).Should().Be("Office2019Colorful");
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.Should().NotBeNull($"field {fieldName} should exist on {instance.GetType().Name}");
        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"field {fieldName} should be initialized");
        return value!;
    }

    private static TControl FindControl<TControl>(Control root, string name)
        where TControl : Control
    {
        if (root is TControl typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        foreach (Control child in root.Controls)
        {
            var result = TryFindControl<TControl>(child, name);
            if (result != null)
            {
                return result;
            }
        }

        throw new InvalidOperationException($"Control '{name}' of type {typeof(TControl).Name} was not found.");
    }

    private static TControl? TryFindControl<TControl>(Control root, string name)
        where TControl : Control
    {
        if (root is TControl typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        foreach (Control child in root.Controls)
        {
            var nested = TryFindControl<TControl>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
    private static string? GetThemeName(Control control)
    {
        return control.GetType()
            .GetProperty("ThemeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?.GetValue(control) as string;
    }
}
