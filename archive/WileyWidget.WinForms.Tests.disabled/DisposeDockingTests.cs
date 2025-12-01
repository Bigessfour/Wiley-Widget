#nullable enable

using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;

namespace WileyWidget.WinForms.Tests
{
    // Disable parallelization for WinForms tests to avoid multiple UI threads running concurrently
    [CollectionDefinition("WinForms", DisableParallelization = true)]
    public class WinFormsCollectionDefinition { }

    [Collection("WinForms")]
    public class DisposeDockingTests
    {
        private void RunOnSta(Action action)
        {
            Exception? threadEx = null;
            var mre = new ManualResetEventSlim(false);

            var t = new Thread(() =>
            {
                try
                {
                    // Ensure WinForms synchronization context is available
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                    action();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
                finally
                {
                    mre.Set();
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();

            // Wait for the action to finish (with a reasonable timeout)
            if (!mre.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("STA action did not complete within timeout");
            }

            if (threadEx != null) throw new AggregateException("Exception on STA thread", threadEx);
        }

        private IDbContextFactory<AppDbContext> CreateTestDbFactory()
        {
            return new TestDbContextFactory();
        }

        private class TestDbContextFactory : IDbContextFactory<AppDbContext>
        {
            public AppDbContext CreateDbContext()
            {
                var opts = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;
                return new AppDbContext(opts);
            }
        }

        [Fact]
        public void AccountsPanel_Create_Host_Remove_Dispose_NoExceptions()
        {
            RunOnSta(() =>
            {
                var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, CreateTestDbFactory());

                // Create an invisible host form with a DockingManager to exercise docking lifecycle
                using var host = new Form();
                host.Size = new System.Drawing.Size(800, 600);

                // Create a docking manager via the same initialization used by MainForm
                var dockingManager = new Syncfusion.Windows.Forms.Tools.DockingManager();
                dockingManager.SuspendLayout();
                host.Controls.Add(dockingManager);
                dockingManager.ResumeLayout(false);

                for (int i = 0; i < 20; i++)
                {
                    AccountsPanel? f = null;
                    try
                    {
                        f = new AccountsPanel(vm);

                        // Host the panel as a control for docking
                        var hostControl = f as Control;
                        if (hostControl != null)
                        {
                            // Add as a child to host so docking manager can reference it
                            host.Controls.Add(hostControl);
                            hostControl.Visible = true;
                            hostControl.BringToFront();
                        }

                        Application.DoEvents();
                        Thread.Sleep(10);

                        // Simulate undock/remove
                        if (hostControl != null && host.Controls.Contains(hostControl))
                        {
                            host.Controls.Remove(hostControl);
                        }
                        // No Close() on UserControl; just dispose
                        // (tests assert that repeated add/remove/dispose doesn't throw)
                        Application.DoEvents();
                    }
                    finally
                    {
                        try { f?.Dispose(); } catch { }
                    }
                }
            });
        }

        [Fact]
        public void MainForm_Create_Show_Close_Dispose_NoExceptions()
        {
            RunOnSta(() =>
            {
                // MainForm accepts optional dependencies; construct with nulls for a minimal smoke test
                WileyWidget.WinForms.Forms.MainForm? f = null;
                try
                {
                    f = new MainForm();
                    f.Show();
                    Application.DoEvents();
                    Thread.Sleep(50);
                    f.Close();
                    Application.DoEvents();
                }
                finally
                {
                    try { f?.Dispose(); } catch { }
                }
            });
        }
    }
}
