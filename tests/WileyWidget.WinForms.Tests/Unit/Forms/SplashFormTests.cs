using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Moq;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using Xunit;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class SplashFormTests
    {
        [StaFact]
        public void Constructor_InHeadlessMode_DoesNotStartUiThread()
        {
            // Arrange - set headless env var
            var prev = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            try
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

                // Act
                var splash = new SplashForm();

                // Access private field _uiThread via reflection
                var fi = typeof(SplashForm).GetField("_uiThread", BindingFlags.Instance | BindingFlags.NonPublic);
                fi.Should().NotBeNull();
                var uiThread = fi!.GetValue(splash) as Thread;

                // Assert: no UI thread started in headless mode
                uiThread.Should().BeNull();

                // Cleanup
                splash.Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", prev);
            }
        }

        [StaFact]
        public void Report_UpdatesUiControls_OnSplashUiThread()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var splash = new SplashForm();

            try
            {
                // Retrieve private controls
                var msgField = typeof(SplashForm).GetField("_messageLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                var progressField = typeof(SplashForm).GetField("_progressBar", BindingFlags.Instance | BindingFlags.NonPublic);
                msgField.Should().NotBeNull();
                progressField.Should().NotBeNull();

                var label = msgField!.GetValue(splash) as Label;
                if (label is null)
                {
                    throw new InvalidOperationException("Splash message label not found.");
                }

                var progressControl = progressField!.GetValue(splash) as Control;
                if (progressControl is null)
                {
                    throw new InvalidOperationException("Splash progress control not found.");
                }

                // Ensure we're on a different thread than the controls (InvokeRequired should be true)
                bool invokeRequired = label!.InvokeRequired;
                invokeRequired.Should().BeTrue();

                // Act: report a message and progress
                splash.Report(0.42, "Working", isIndeterminate: false);

                // Wait for update to propagate (give UI thread time)
                var success = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        var currentTextObj = label.Invoke(new Func<string>(() => label.Text));
                        if (currentTextObj is not string currentText) return false;

                        var currentValue = (int)progressControl.Invoke(new Func<int>(() =>
                        {
                            if (progressControl is Syncfusion.Windows.Forms.Tools.ProgressBarAdv adv)
                                return adv.Value;
                            if (progressControl is ProgressBar bar)
                                return bar.Value;
                            return 0;
                        }));

                        return currentText == "Working" && currentValue >= 41 && currentValue <= 43;
                    }
                    catch
                    {
                        return false;
                    }
                }, TimeSpan.FromSeconds(2));

                // Assert
                success.Should().BeTrue("UI controls should be updated via the splash UI thread");
            }
            finally
            {
                splash.Dispose();
            }
        }

        [StaFact]
        public void Dispose_CancelsAndAllowsUiThreadToExit_WithoutBlocking()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var splash = new SplashForm();

            // Access private ui thread
            var fi = typeof(SplashForm).GetField("_uiThread", BindingFlags.Instance | BindingFlags.NonPublic);
            fi.Should().NotBeNull();
            var uiThread = fi!.GetValue(splash) as Thread;

            // If no UI thread (possible on some CI), treat as pass
            if (uiThread == null)
            {
                splash.Dispose();
                return;
            }

            // Act: Dispose and ensure Dispose returns promptly
            var sw = Stopwatch.StartNew();
            splash.Dispose();
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Dispose should not block waiting for UI thread to join");

            // Wait briefly for thread to exit via cancellation callback
            var exited = SpinWait.SpinUntil(() => !uiThread.IsAlive, TimeSpan.FromSeconds(2));
            exited.Should().BeTrue("UI thread should exit after cancellation and form close");
        }

        [StaFact]
        public void ConcurrentReports_DoNotFreezeUi()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var splash = new SplashForm();

            try
            {
                var msgField = typeof(SplashForm).GetField("_messageLabel", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var progressField = typeof(SplashForm).GetField("_progressBar", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var label = (Label)msgField.GetValue(splash)!;
                var progress = progressField.GetValue(splash) as Control;

                const int parallelUpdates = 200;
                var tasks = new Task[parallelUpdates];

                // Act: fire many concurrent reports from thread-pool threads
                for (int i = 0; i < parallelUpdates; i++)
                {
                    var idx = i;
                    tasks[i] = Task.Run(() => splash.Report(idx / (double)parallelUpdates, $"Msg {idx}", isIndeterminate: false));
                }

                // Wait for all reports to be queued
                Task.WaitAll(tasks, TimeSpan.FromSeconds(5));

                // Allow the UI thread to process posted updates
                var processed = SpinWait.SpinUntil(() =>
                {
                    try
                    {
                        var txtObj = label.Invoke(new Func<string>(() => label.Text));
                        if (txtObj is not string txt) return false;
                        return txt != "Initializing...";
                    }
                    catch
                    {
                        return false;
                    }
                }, TimeSpan.FromSeconds(3));

                processed.Should().BeTrue("UI should process concurrent updates without freezing");
            }
            finally
            {
                splash.Dispose();
            }
        }
    }
}
