#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using WileyWidget.WinForms.Forms;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests
{
    public class MainFormTests
    {
        private MainViewModel CreateTestMainViewModel()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            return new MainViewModel(
                messenger: null,
                logger: NullLogger<MainViewModel>.Instance,
                dbContextFactory: factory);
        }

        [Test]
        public void Ctor_InitsDocking()
        {
            using var form = new MainForm(CreateTestMainViewModel());
            var dmField = typeof(MainForm).GetField("_dockingManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var dm = dmField!.GetValue(form);
            dm.Should().NotBeNull();
        }

        [Test]
        public void DockUserControlPanel_Existing_ActivatesViaDockingManager()
        {
            using var form = new MainForm(CreateTestMainViewModel());

            // Insert fake docking manager which records calls
            var fake = new FakeDockingManager();
            var dmField = typeof(MainForm).GetField("_dockingManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
            dmField.SetValue(form, fake);

            // Add a dummy existing control to internal dictionary
            var dockedField = typeof(MainForm).GetField("_dockedControls", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dict = dockedField.GetValue(form) as Dictionary<string, Control>;
            dict!.Add("Accounts", new UserControl() { Name = "Accounts" });

            // Invoke DockUserControlPanel<TPanel> with AccountsPanel generic - should activate existing
            var method = typeof(MainForm).GetMethod("DockUserControlPanel", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var generic = method.MakeGenericMethod(typeof(WileyWidget.WinForms.Controls.AccountsPanel));
            generic.Invoke(form, new object?[] { "Accounts" });

            fake.SetDockVisibilityCalled.Should().BeTrue();
            fake.ActivateControlCalled.Should().BeTrue();
        }

        [Test]
        public void SaveDockingLayout_CallsSaveOnManager()
        {
            using var form = new MainForm(CreateTestMainViewModel());
            var fake = new FakeDockingManager();
            var dmField = typeof(MainForm).GetField("_dockingManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
            dmField.SetValue(form, fake);

            var method = typeof(MainForm).GetMethod("SaveDockingLayout", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(form, null);

            fake.SaveDockStateCalled.Should().BeTrue();
        }

        [Test]
        public void ExceptionHandlers_AreRegistered()
        {
            using var form = new MainForm(CreateTestMainViewModel());
            var threadField = typeof(MainForm).GetField("_threadExceptionHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var domField = typeof(MainForm).GetField("_domainExceptionHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var thr = threadField.GetValue(form);
            var dom = domField.GetValue(form);

            thr.Should().NotBeNull();
            dom.Should().NotBeNull();

            // Ensure calling handler doesn't throw
            var handler = thr as System.Threading.ThreadExceptionEventHandler;
            Assert.DoesNotThrow(() => handler?.Invoke(this, new System.Threading.ThreadExceptionEventArgs(new InvalidOperationException("test"))));

            var domHandler = dom as UnhandledExceptionEventHandler;
            Assert.DoesNotThrow(() => domHandler?.Invoke(this, new UnhandledExceptionEventArgs(new InvalidOperationException("boom"), false)));
        }

        private class FakeDockingManager
        {
            public bool SetDockVisibilityCalled { get; private set; }
            public bool ActivateControlCalled { get; private set; }
            public bool SaveDockStateCalled { get; private set; }

            public void SetDockVisibility(Control c, bool v)
            {
                SetDockVisibilityCalled = true;
            }

            public void ActivateControl(Control c)
            {
                ActivateControlCalled = true;
            }

            public void SaveDockState(object serializer)
            {
                SaveDockStateCalled = true;
            }

            // Methods used in other areas but not needed for these tests are intentionally omitted
        }
    }
}
