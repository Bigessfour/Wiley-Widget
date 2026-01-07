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
            // Ensure theme assemblies are available to prevent SetVisualStyle errors
            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            var mainForm = new MainForm();
            _formsToDispose.Add(mainForm);

var (ribbon, homeTab) = RibbonFactory.CreateRibbon(mainForm, NullLogger<MainForm>.Instance);

            ribbon.Should().NotBeNull();
            homeTab.Should().NotBeNull();

            // ToolStripEx may be nested; search all ToolStrip controls on the main form for the drop-down button
            IEnumerable<ToolStrip> FindAllToolStrips(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    if (c is ToolStrip ts) yield return ts;
                    foreach (var child in FindAllToolStrips(c)) yield return child;
                }
            }

            var toolStrips = FindAllToolStrips(mainForm).ToList();
            toolStrips.Should().NotBeEmpty("Expected to find at least one ToolStrip on the main form");

            var panelsDropDown = toolStrips.SelectMany(ts => ts.Items.OfType<ToolStripDropDownButton>())
                                           .FirstOrDefault(b => b.Name == "Nav_Panels");
            panelsDropDown.Should().NotBeNull("Panels drop-down should exist in a ToolStrip on the main form");

            var expected = PanelRegistry.Panels.Where(e => e.ShowInRibbonPanelsMenu).Select(e => e.DisplayName).ToList();
            var actual = panelsDropDown!.DropDownItems.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();

            foreach (var text in expected)
            {
                actual.Should().Contain(text, $"Panels menu should include '{text}'");
            }

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
