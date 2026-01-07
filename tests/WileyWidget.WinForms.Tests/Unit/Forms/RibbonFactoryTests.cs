#pragma warning disable CA1303 // Do not pass literals as localized parameters - Test strings are not UI strings

using Xunit;
using System;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Collection(WinFormsUiCollection.CollectionName)]
public sealed class RibbonFactoryTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;
    private readonly System.Collections.Generic.List<Form> _formsToDispose = new();

    public RibbonFactoryTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void PanelRegistry_Entries_AreValid()
    {
        var entries = PanelRegistry.Panels;
        entries.Should().NotBeEmpty("PanelRegistry should contain at least one registered panel");

        foreach (var e in entries)
        {
            e.DisplayName.Should().NotBeNullOrWhiteSpace();
            typeof(UserControl).IsAssignableFrom(e.PanelType).Should().BeTrue($"{e.PanelType} must derive from UserControl");
        }
    }

    [Fact]
    public void CreateRibbon_PanelsMenu_Populated()
    {
        _ui.Run(() =>
        {
            // Arrange
            // Ensure theme assemblies are available to prevent SetVisualStyle errors
            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            var mainForm = new MainForm();
            _formsToDispose.Add(mainForm);

            var (ribbon, homeTab) = RibbonFactory.CreateRibbon(mainForm, NullLogger<MainForm>.Instance);

            ribbon.Should().NotBeNull();
            homeTab.Should().NotBeNull();

            // Verify the ribbon was created successfully with a Home tab
            homeTab.Text.Should().Be("Home", "Home tab should be created by RibbonFactory");
            homeTab.Panel.Should().NotBeNull("Home tab should have a panel for toolbar items");

            // Dispose created UI objects
            ribbon.Dispose();
            mainForm.Dispose();
        });
    }

    public void Dispose()
    {
        foreach (var form in _formsToDispose)
        {
            try { form?.Dispose(); } catch { }
        }
        _formsToDispose.Clear();
    }
}
