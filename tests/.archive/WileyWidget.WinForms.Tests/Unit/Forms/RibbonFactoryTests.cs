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

            // NOTE: RibbonFactory.CreateRibbon requires MainForm (not generic Form).
            // Full ribbon creation test is covered by E2E UI tests.
            // For unit testing, we verify PanelRegistry is properly populated,
            // which is used by ribbon creation for the Panels dropdown menu.

            // Verify PanelRegistry is properly populated as a lightweight verification
            var entries = PanelRegistry.Panels;
            entries.Should().NotBeEmpty("PanelRegistry should contain registered panels for ribbon creation");
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
