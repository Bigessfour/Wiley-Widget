using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Utilities;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Architecture", "Phase1PanelSkeleton")]
public sealed class ScopedPanelLayoutIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string ProfessionalContentHostName = "ScopedPanelContentHost";

    [StaFact]
    public void ScopedPanelBase_ProfessionalLayout_IsIdempotent_AndMigratesLegacyControls()
    {
        using var hostForm = new Form
        {
            Width = 1200,
            Height = 800,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
        };

        using var panel = new TestScopedPanel();
        panel.Text = "Test Panel";

        var legacyControl = new Label
        {
            Name = "LegacyControl",
            AutoSize = true,
            Text = "Legacy",
        };

        panel.Controls.Add(legacyControl);
        hostForm.Controls.Add(panel);

        panel.ReapplyProfessionalLayout();
        hostForm.CreateControl();
        panel.CreateControl();
        Application.DoEvents();

        var firstHost = panel.ExposedContentHost;
        firstHost.Should().NotBeNull();
        firstHost!.Name.Should().Be(ProfessionalContentHostName);

        panel.Controls.OfType<PanelHeader>().Should().ContainSingle();
        panel.Controls.OfType<Panel>().Count(p => p.Name == ProfessionalContentHostName).Should().Be(1);
        panel.Padding.Should().Be(new Padding(LayoutTokens.PanelPadding));
        panel.Margin.Should().Be(Padding.Empty);
        panel.Dock.Should().Be(DockStyle.Fill);
        panel.AutoScroll.Should().BeTrue();

        legacyControl.Parent.Should().Be(firstHost, "legacy controls must be migrated into the content host");

        panel.ReapplyProfessionalLayout();
        Application.DoEvents();

        panel.ExposedContentHost.Should().BeSameAs(firstHost, "layout reapply must be idempotent and not wrap hosts repeatedly");
        panel.Controls.OfType<PanelHeader>().Should().ContainSingle();
        panel.Controls.OfType<Panel>().Count(p => p.Name == ProfessionalContentHostName).Should().Be(1);
    }

    [StaFact]
    public void PanelRegistry_ScopedPanels_UseProfessionalSkeletonContract()
    {
        var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        try
        {
            var panelEntries = PanelRegistry.Panels
                .Where(entry => typeof(ScopedPanelBase).IsAssignableFrom(entry.PanelType))
                .OrderBy(entry => entry.PanelType.Name)
                .ToList();

            var failures = new List<string>();

            foreach (var entry in panelEntries)
            {
                using var scope = CreateScope();

                ScopedPanelBase? panel;
                try
                {
                    panel = scope.ServiceProvider.GetService(entry.PanelType) as ScopedPanelBase
                        ?? ActivatorUtilities.CreateInstance(scope.ServiceProvider, entry.PanelType) as ScopedPanelBase;
                }
                catch (Exception ex)
                {
                    failures.Add($"{entry.PanelType.Name}: DI resolve failed ({ex.GetType().Name}: {ex.Message})");
                    continue;
                }

                if (panel == null)
                {
                    failures.Add($"{entry.PanelType.Name}: DI resolve returned null or non-ScopedPanelBase");
                    continue;
                }

                var hostForm = new Form
                {
                    Width = 1600,
                    Height = 900,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-2000, -2000),
                };

                try
                {
                    hostForm.Controls.Add(panel);
                    hostForm.CreateControl();
                    panel.CreateControl();
                    Application.DoEvents();

                    var headerCount = panel.Controls.OfType<PanelHeader>().Count();
                    if (headerCount != 1)
                    {
                        failures.Add($"{entry.PanelType.Name}: expected exactly 1 PanelHeader, found {headerCount}");
                    }

                    var contentHost = panel.Controls.OfType<Panel>()
                        .FirstOrDefault(control => string.Equals(control.Name, ProfessionalContentHostName, StringComparison.Ordinal));

                    if (contentHost == null)
                    {
                        failures.Add($"{entry.PanelType.Name}: missing {ProfessionalContentHostName}");
                    }
                    else
                    {
                        if (contentHost.Dock != DockStyle.Fill)
                        {
                            failures.Add($"{entry.PanelType.Name}: content host Dock expected Fill, found {contentHost.Dock}");
                        }

                        if (!ReferenceEquals(contentHost.Parent, panel))
                        {
                            failures.Add($"{entry.PanelType.Name}: content host parent is not panel root");
                        }
                    }

                    if (panel.Dock != DockStyle.Fill)
                    {
                        failures.Add($"{entry.PanelType.Name}: panel Dock expected Fill, found {panel.Dock}");
                    }

                    if (panel.Padding != new Padding(LayoutTokens.PanelPadding))
                    {
                        failures.Add($"{entry.PanelType.Name}: panel Padding expected {LayoutTokens.PanelPadding}, found {panel.Padding}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{entry.PanelType.Name}: initialization failed ({ex.GetType().Name}: {ex.Message})");
                }
                finally
                {
                    try
                    {
                        hostForm.Controls.Remove(panel);
                        hostForm.Dispose();
                    }
                    catch
                    {
                        // Best-effort cleanup only. This contract test focuses on layout conformance.
                    }
                }
            }

            failures.Should().BeEmpty(
                "Phase 1 requires every ScopedPanelBase panel in PanelRegistry to conform to header/padding/content-host skeleton. Failures: {0}",
                string.Join(" | ", failures));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
        }
    }

    private sealed class TestScopedPanel : ScopedPanelBase
    {
        public Panel? ExposedContentHost => ContentHost;

        public void ReapplyProfessionalLayout() => ApplyProfessionalPanelLayout();
    }
}
