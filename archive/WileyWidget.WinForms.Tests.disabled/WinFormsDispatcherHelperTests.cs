#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Threading;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Tests
{
    public class WinFormsDispatcherHelperTests
    {
        [Fact]
        public void Invoke_WithNoContext_ExecutesInline()
        {
            // Ensure no sync context is set so helper treats current thread as UI
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                var helper = new WinFormsDispatcherHelper();
                var called = false;
                helper.Invoke(() => called = true);
                Assert.True(called);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [Fact]
        public async Task InvokeAsync_WithCustomContext_PostsToContext()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var ctx = new TestSyncContext();
                SynchronizationContext.SetSynchronizationContext(ctx);

                var helper = new WinFormsDispatcherHelper();

                bool called = false;
                await helper.InvokeAsync(() => called = true);

                // Our TestSyncContext executes post'd delegates synchronously, so the task should be completed
                Assert.True(called);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [Fact]
        public async Task InvokeAsync_ActionThrows_RecordsTelemetry()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var ctx = new TestSyncContext();
                SynchronizationContext.SetSynchronizationContext(ctx);

                var mockTelemetry = new Mock<ITelemetryService>();
                var logger = NullLogger<WinFormsDispatcherHelper>.Instance;
                var helper = new WinFormsDispatcherHelper(logger, mockTelemetry.Object);

                // Action throws inside UI context
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.InvokeAsync(() => throw new InvalidOperationException("boom")));

                mockTelemetry.Verify(t => t.RecordException(It.IsAny<Exception>(), It.IsAny<(string, object?)[]>()), Times.AtLeastOnce);
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
