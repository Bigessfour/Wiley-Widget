using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.Tests.Unit
{
    public class TabbedMdiContextMenuTests
    {
        private class DummyDisposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        // Test helper type that intentionally has the same type-name as Syncfusion's TabControlAdv
        // so that the MainForm reflection-based scanners detect it in its control tree.
        public class TabControlAdv : System.Windows.Forms.Control
        {
            // 'new' intentionally hides the inherited ContextMenuStrip property so tests can inspect
            // the value using the same property name the production code looks for via reflection.
            public new object ContextMenuStrip { get; set; }
            public object ContextMenuPlaceHolder { get; set; }
            public object ContextMenuStripPlaceHolder { get; set; }
        }

        private MainForm CreateMainForm()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var mockLogger = new Mock<ILogger<MainForm>>();
            var config = new ConfigurationBuilder().Build();
            return new MainForm(provider, mockLogger.Object, config);
        }

        [Fact]
        public void ResetTabControlContextMenuProperty_nulls_and_disposes()
        {
            using var mainForm = CreateMainForm();

            var fake = new TabControlAdv();
            var dd = new DummyDisposable();
            fake.ContextMenuStrip = dd;

            var method = typeof(MainForm).GetMethod("ResetTabControlContextMenuProperty", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            method.Invoke(mainForm, new object[] { fake, "ContextMenuStrip" });

            fake.ContextMenuStrip.Should().BeNull();
            dd.Disposed.Should().BeTrue();
        }

        [Fact]
        public void ClearTabbedTabControlContextMenu_clears_all_found_tabcontrols()
        {
            using var mainForm = CreateMainForm();

            var fake1 = new TabControlAdv();
            var fake2 = new TabControlAdv();
            var d1 = new DummyDisposable();
            var d2 = new DummyDisposable();
            fake1.ContextMenuStrip = d1;
            fake2.ContextMenuPlaceHolder = d2;

            // Add to the form's Control tree so FindAllTabControlAdvInstances will find them
            mainForm.Controls.Add(fake1);
            mainForm.Controls.Add(fake2);

            var method = typeof(MainForm).GetMethod("ClearTabbedTabControlContextMenu", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            method.Invoke(mainForm, null);

            fake1.ContextMenuStrip.Should().BeNull();
            d1.Disposed.Should().BeTrue();

            fake2.ContextMenuPlaceHolder.Should().BeNull();
            d2.Disposed.Should().BeTrue();
        }

        [Fact]
        public void ConfigureTabContextMenu_assigns_context_menu_to_specific_tabcontrol()
        {
            using var mainForm = CreateMainForm();

            var fake = new TabControlAdv();
            fake.ContextMenuStrip = null;

            var method = typeof(MainForm).GetMethod("ConfigureTabContextMenu", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            method.Invoke(mainForm, new object[] { fake });

            fake.ContextMenuStrip.Should().NotBeNull();
            fake.ContextMenuStrip.Should().BeOfType<System.Windows.Forms.ContextMenuStrip>();
        }
    }
}
