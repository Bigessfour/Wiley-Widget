#nullable enable

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using WileyWidget.Services.Threading;
// removed xUnit references — using NUnit attributes and Apartment for STA tests

namespace WileyWidget.WinForms.Tests
{
    public class UIThreadHelperTests
    {
        [Test]
        public void Initialize_CapturesContext_WhenAvailable()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var ctx = new TestSyncContext();
                SynchronizationContext.SetSynchronizationContext(ctx);

                var helper = new WinFormsDispatcherHelper();
                // When constructed on a thread with a SynchronizationContext we should be able to check access
                helper.CheckAccess().Should().BeTrue();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [Test]
        public void Invoke_ThrowsOnNullAction()
        {
            var helper = new WinFormsDispatcherHelper();
            Action? act = null;
            FluentActions.Invoking(() => helper.Invoke(act!)).Should().Throw<ArgumentNullException>();
        }

        [Apartment(ApartmentState.STA)]
        public async Task BeginInvoke_OnUI_RunsImmediate()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var ctx = new TestSyncContext();
                SynchronizationContext.SetSynchronizationContext(ctx);

                var helper = new WinFormsDispatcherHelper();
                var ran = false;

                await helper.InvokeAsync(() => ran = true);

                ran.Should().BeTrue();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [Apartment(ApartmentState.STA)]
        public async Task BeginInvoke_OffUI_Marshals()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var ctx = new TestSyncContext();
                SynchronizationContext.SetSynchronizationContext(ctx);

                var helper = new WinFormsDispatcherHelper();

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Run from a background thread where SynchronizationContext.Current != _uiContext
                await Task.Run(async () =>
                {
                    await helper.InvokeAsync(() => tcs.SetResult(true));
                });

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                completed.Should().Be(tcs.Task);
                tcs.Task.Result.Should().BeTrue();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [Test]
        public void Invoke_WithNoContext_LogsWarning_And_ExecutesInline()
        {
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var loggerMock = new Mock<ILogger<WinFormsDispatcherHelper>>();
                var helper = new WinFormsDispatcherHelper(loggerMock.Object);

                var called = false;
                helper.Invoke(() => called = true);

                called.Should().BeTrue();
                loggerMock.Verify(l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.AtLeastOnce);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        private class TestSyncContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state)
            {
                // Simulate UI-thread execution by invoking synchronously
                d(state);
            }

            public override void Send(SendOrPostCallback d, object? state)
            {
                d(state);
            }
        }
    }
}
