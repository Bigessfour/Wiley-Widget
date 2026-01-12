#pragma warning disable CA1303 // Do not pass literals as localized parameters - Test strings are not UI strings

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Xunit.Sdk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Configuration;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

[Collection(WinFormsUiCollection.CollectionName)]
public sealed class DockingLayoutManagerTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;
    private readonly List<Form> _formsToDispose = new();

    public DockingLayoutManagerTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void SaveLayout_WritesBinaryFile_WhenPanelsAreDocked()
    {
        _ui.Run(() =>
        {
            // Arrange
            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: true).BuildServiceProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var dlmLogger = loggerFactory.CreateLogger<DockingLayoutManager>();
            var dlm = new DockingLayoutManager(services, panelNavigator: null, dlmLogger);

            var mainForm = new MainForm();
            _formsToDispose.Add(mainForm);

            // Create docking host (this docks left/right panels and adds content)
            var (dockingManager, leftPanel, rightPanel, _, _) = DockingHostFactory.CreateDockingHost(mainForm, services, panelNavigator: null, logger: NullLogger<MainForm>.Instance);

            // Initialize manager and attach event handlers
            dlm.InitializeDockingManager(dockingManager);
            dlm.AttachTo(dockingManager);

            // Act - write to a temp layout base path
            var layoutBase = Path.Combine(Path.GetTempPath(), "wiley_dock_" + Guid.NewGuid().ToString("N"));
            try
            {
                dlm.SaveLayout(dockingManager, layoutBase);

                var binaryPath = Path.ChangeExtension(layoutBase, ".bin");
                var panelsJson = layoutBase + ".panels.json";

                // Wait briefly for file system to flush/move files (avoid transient timing issues in CI)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (!File.Exists(binaryPath) && sw.ElapsedMilliseconds < 1000)
                {
                    System.Threading.Thread.Sleep(50);
                }

                // Assert - binary layout exists and has content
                File.Exists(binaryPath).Should().BeTrue("Docking layout binary must be written when panels are docked");
                new FileInfo(binaryPath).Length.Should().BeGreaterThan(0, "Binary layout file should not be empty");

                // Panels metadata should also be saved (may be written slightly after binary)
                sw.Restart();
                while (!File.Exists(panelsJson) && sw.ElapsedMilliseconds < 1000)
                {
                    System.Threading.Thread.Sleep(25);
                }
                File.Exists(panelsJson).Should().BeTrue("Panels metadata JSON should be saved alongside the binary layout");

                // Per-panel controls should have caption buttons visible as configured by our code
                dockingManager.GetCloseButtonVisibility(leftPanel).Should().BeTrue("Left panel should have close button visible");
                dockingManager.GetAutoHideButtonVisibility(leftPanel).Should().BeTrue("Left panel should have auto-hide button visible");
                dockingManager.GetMenuButtonVisibility(leftPanel).Should().BeTrue("Left panel should have menu button visible");
            }
            finally
            {
                // Cleanup artifacts
                try { File.Delete(Path.ChangeExtension(layoutBase, ".bin")); } catch { }
                try { File.Delete(layoutBase + ".panels.json"); } catch { }
            }
        });
    }

    [Fact]
    public void DockingManager_IsConfigured_AsExpected()
    {
        _ui.Run(() =>
        {
            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: true).BuildServiceProvider();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var mainForm = new MainForm();
            _formsToDispose.Add(mainForm);

            var (dockingManager, leftPanel, rightPanel, _, _) = DockingHostFactory.CreateDockingHost(mainForm, services, panelNavigator: null, logger: loggerFactory.CreateLogger<MainForm>());

            // Basic Syncfusion recommendations / WileyWidget expectations
            dockingManager.MaximizeButtonEnabled.Should().BeTrue("Maximize button should be enabled on DockingManager");
            dockingManager.ShowCaption.Should().BeTrue("Captions should be visible so users see panel titles");
            dockingManager.ShowCaptionImages.Should().BeTrue("Caption images should be visible to show chrome affordances");

            // Fonts should be set for accessibility
            dockingManager.DockTabFont.Should().NotBeNull("Dock tab font should be set");
            dockingManager.AutoHideTabFont.Should().NotBeNull("Auto-hide tab font should be set");

            // Project decision: PersistState is intentionally disabled (we use manual persistence via DockingLayoutManager)
            dockingManager.PersistState.Should().BeFalse("WileyWidget uses manual persistence via DockingLayoutManager - PersistState should be false by design");

            // Finally, ensure there is at least one docked and enabled panel (guards SaveLayout path)
            var hostForm = dockingManager.HostControl as Form;
            hostForm.Should().NotBeNull("DockingManager must have a Form HostControl");

            var found = false;

            // Recursively scan the host form's control tree for ANY Panel that is enabled for docking
            IEnumerable<Control> EnumerateControls(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    yield return c;
                    foreach (var inner in EnumerateControls(c)) yield return inner;
                }
            }

            foreach (var c in EnumerateControls(hostForm!))
            {
                if (c is Panel p)
                {
                    try
                    {
                        if (dockingManager.GetEnableDocking(p)) { found = true; break; }
                    }
                    catch { }
                }
            }

            found.Should().BeTrue("At least one Panel in the host form should be enabled for docking");
        });
    }

    [Fact]
    public void SaveAndLoad_Roundtrip_BinaryLayout_IsLoadable()
    {
        _ui.Run(() =>
        {
            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);

            var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: true).BuildServiceProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var dlmLogger = loggerFactory.CreateLogger<DockingLayoutManager>();
            var dlm = new DockingLayoutManager(services, panelNavigator: null, dlmLogger);

            var mainForm = new MainForm();
            _formsToDispose.Add(mainForm);

            var (dockingManager, leftPanel, rightPanel, _, _) = DockingHostFactory.CreateDockingHost(mainForm, services, panelNavigator: null, logger: NullLogger<MainForm>.Instance);

            dlm.InitializeDockingManager(dockingManager);
            dlm.AttachTo(dockingManager);

            var layoutBase = Path.Combine(Path.GetTempPath(), "wiley_dock_" + Guid.NewGuid().ToString("N"));
            try
            {
                dlm.SaveLayout(dockingManager, layoutBase);

                var binaryPath = Path.ChangeExtension(layoutBase, ".bin");

                // Verify file exists before attempting load
                File.Exists(binaryPath).Should().BeTrue("Binary layout file must exist prior to load");

                // Create a fresh manager and form for load
                var newLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var newDlmLogger = newLoggerFactory.CreateLogger<DockingLayoutManager>();
                var newDlm = new DockingLayoutManager(services, panelNavigator: null, newDlmLogger);

                var newForm = new MainForm();
                _formsToDispose.Add(newForm);

                var (newDockingManager, newLeft, newRight, _, _) = DockingHostFactory.CreateDockingHost(newForm, services, panelNavigator: null, logger: NullLogger<MainForm>.Instance);

                newDlm.InitializeDockingManager(newDockingManager);
                newDlm.AttachTo(newDockingManager);

                // Load the saved layout into the new docking manager instance
                newDlm.LoadLayoutAsync(newDockingManager, newForm, layoutBase).GetAwaiter().GetResult();

                // After load, ensure caption buttons on left panel are visible (as saved)
                newDockingManager.GetCloseButtonVisibility(newLeft).Should().BeTrue();
                newDockingManager.GetAutoHideButtonVisibility(newLeft).Should().BeTrue();
                newDockingManager.GetMenuButtonVisibility(newLeft).Should().BeTrue();
            }
            finally
            {
                try { File.Delete(Path.ChangeExtension(layoutBase, ".bin")); } catch { }
                try { File.Delete(layoutBase + ".panels.json"); } catch { }
            }
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
