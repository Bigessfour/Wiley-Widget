using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit;

[Collection("SyncfusionTheme")]
public class PaymentEditPanelLayoutTests
{
    [WinFormsFact]
    public void PaymentEditPanel_CompactHostedDialog_UsesSharedCompactSizing()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        var panel = ActivatorUtilities.CreateInstance<PaymentEditPanel>(provider);
        using var hostForm = new Form
        {
            Text = "Edit Payment",
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        PaymentEditPanel.ConfigureHostedDialog(hostForm);
        panel.Dock = DockStyle.Fill;
        hostForm.Controls.Add(panel);

        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var scrollHost = FindDescendants<Panel>(panel).SingleOrDefault(control => control.AutoScroll);
        var descriptionBox = GetPrivateField<TextBoxExt>(panel, "_txtDescription");
        var firstSectionHeader = FindDescendants<Label>(panel).SingleOrDefault(label => label.Text == "Check Information");

        scrollHost.Should().NotBeNull("the payment editor should render inside a single scrollable content host");
        firstSectionHeader.Should().NotBeNull("the compact payment editor should render the first section heading");
        hostForm.Size.Should().Be(LayoutTokens.GetScaled(new Size(800, 660)));
        hostForm.MinimumSize.Should().Be(LayoutTokens.GetScaled(new Size(720, 600)));
        descriptionBox.Height.Should().BeLessThan(LayoutTokens.GetScaled(88), "the multiline editor should be smaller than the previous oversized body field");
        firstSectionHeader!.Top.Should().BeLessThan(LayoutTokens.GetScaled(96), "the compact payment editor should not leave an oversized blank gap before the first section");
    }

    [WinFormsFact]
    public async System.Threading.Tasks.Task PaymentEditPanel_RebindsExistingSelections_UsingStableValueMembers()
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        paymentRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());

        var accountRepository = new Mock<IMunicipalAccountRepository>();
        accountRepository
            .Setup(repository => repository.GetBudgetAccountsAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MunicipalAccount { Id = 7, Name = "Operating" }
            }.AsEnumerable());

        var vendorRepository = new Mock<IVendorRepository>();
        vendorRepository
            .Setup(repository => repository.GetActiveAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Vendor>
            {
                new() { Id = 8, Name = "Active Vendor", IsActive = true }
            });

        var themeService = new Mock<IThemeService>();
        themeService.SetupGet(service => service.CurrentTheme).Returns("Office2019Colorful");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IThemeService>(themeService.Object);
        services.AddScoped<IPaymentRepository>(_ => paymentRepository.Object);
        services.AddScoped<IMunicipalAccountRepository>(_ => accountRepository.Object);
        services.AddScoped<IVendorRepository>(_ => vendorRepository.Object);
        services.AddScoped<SyncfusionControlFactory>();
        services.AddScoped<PaymentsViewModel>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
        var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase<PaymentsViewModel>>>(provider);

        var panel = new PaymentEditPanel(scopeFactory, logger);
        using var hostForm = new Form
        {
            Text = "Edit Payment",
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        panel.SetExistingPayment(new Payment
        {
            CheckNumber = "1001",
            PaymentDate = new System.DateTime(2026, 3, 10),
            Payee = "Legacy Vendor",
            VendorId = 999,
            MunicipalAccountId = 42,
            Amount = 12.34m,
            Description = "Hydrant repair",
            Status = "Pending"
        });

        panel.Dock = DockStyle.Fill;
        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();

        await panel.LoadDataAsync();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var payeeCombo = GetPrivateField<SfComboBox>(panel, "_cmbPayee");
        var accountCombo = GetPrivateField<SfComboBox>(panel, "_cmbAccount");

        payeeCombo.ValueMember.Should().Be("VendorId",
            "the payee selector should rebind against the vendor identity instead of an object reference");
        payeeCombo.Text.Should().Contain("Legacy Vendor",
            "editing an older payment should preserve the payee display even when the vendor is no longer active");

        accountCombo.ValueMember.Should().Be("AccountId",
            "the account selector should rebind against the account identity instead of an object reference");
        accountCombo.Text.Should().Contain("Historical Account #42",
            "editing an older payment should preserve the historical account display when the account is no longer in the active list");
    }

    private static IEnumerable<TControl> FindDescendants<TControl>(Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindDescendants<TControl>(child))
            {
                yield return descendant;
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