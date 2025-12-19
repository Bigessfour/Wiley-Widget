using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using FluentAssertions;
using Xunit;
using WileyWidget.Services.Threading;
using WileyWidget.WinForms.Tests.Infrastructure;

namespace WileyWidget.WinForms.Tests.Unit.Services.Threading
{
    public class SynchronizationContextDispatcherHelperTests : IDisposable
    {
        private readonly WinFormsUiThreadFixture _uiFixture;

        public SynchronizationContextDispatcherHelperTests()
        {
            _uiFixture = new WinFormsUiThreadFixture();
        }

        [Fact]
        public void CheckAccess_OnUiThread_ReturnsTrue()
        {
            SynchronizationContextDispatcherHelper? helper = null;

            _uiFixture.Run(() =>
            {
                helper = new SynchronizationContextDispatcherHelper(SynchronizationContext.Current);
                helper.CheckAccess().Should().BeTrue();
            });
        }

        [Fact]
        public void Invoke_FromBackgroundThread_ExecutesOnUiThread()
        {
            SynchronizationContextDispatcherHelper? helper = null;
            int uiThreadId = 0;
            bool ranOnUi = false;

            _uiFixture.Run(() =>
            {
                uiThreadId = Thread.CurrentThread.ManagedThreadId;
                helper = new SynchronizationContextDispatcherHelper(SynchronizationContext.Current);
            });

            Task.Run(() =>
            {
                helper!.Invoke(() => ranOnUi = Thread.CurrentThread.ManagedThreadId == uiThreadId);
            }).GetAwaiter().GetResult();

            ranOnUi.Should().BeTrue();
        }

        [Fact]
        public void InvokeT_FromBackgroundThread_ReturnsValueFromUiThread()
        {
            SynchronizationContextDispatcherHelper? helper = null;
            int uiThreadId = 0;

            _uiFixture.Run(() =>
            {
                uiThreadId = Thread.CurrentThread.ManagedThreadId;
                helper = new SynchronizationContextDispatcherHelper(SynchronizationContext.Current);
            });

            var result = Task.Run(() => helper!.Invoke(() => Thread.CurrentThread.ManagedThreadId)).GetAwaiter().GetResult();

            result.Should().Be(uiThreadId);
        }

        [Fact]
        public async Task InvokeAsync_PostsToUiThread()
        {
            SynchronizationContextDispatcherHelper? helper = null;
            int uiThreadId = 0;
            bool ranOnUi = false;

            _uiFixture.Run(() =>
            {
                uiThreadId = Thread.CurrentThread.ManagedThreadId;
                helper = new SynchronizationContextDispatcherHelper(SynchronizationContext.Current);
            });

            await helper!.InvokeAsync(() => ranOnUi = Thread.CurrentThread.ManagedThreadId == uiThreadId).ConfigureAwait(false);

            ranOnUi.Should().BeTrue();
        }

        [Fact]
        public void Invoke_WithSendNotSupported_UsesPostAndWait_ExecutesOnContextThread()
        {
            using var ctxt = new NoSendSynchronizationContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            int executedThreadId = 0;

            helper.Invoke(() => executedThreadId = Thread.CurrentThread.ManagedThreadId);

            executedThreadId.Should().Be(ctxt.ThreadId);
        }

        [Fact]
        public void Invoke_WithSendNotSupported_ExceptionPropagates()
        {
            using var ctxt = new NoSendSynchronizationContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            Action act = () => helper.Invoke(() => throw new InvalidOperationException("boom"));

            act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public void InvokeT_WithSendNotSupported_ReturnsValueAndExecutesOnContextThread()
        {
            using var ctxt = new NoSendSynchronizationContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            int executedThreadId = 0;

            var result = helper.Invoke(() =>
            {
                executedThreadId = Thread.CurrentThread.ManagedThreadId;
                return "ok";
            });

            result.Should().Be("ok");
            executedThreadId.Should().Be(ctxt.ThreadId);
        }

        [Fact]
        public void Invoke_PostThrows_ExecutesSynchronouslyOnCallingThread()
        {
            var ctxt = new PostThrowsSyncContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            int callingThread = Thread.CurrentThread.ManagedThreadId;
            int executedThread = 0;

            helper.Invoke(() => executedThread = Thread.CurrentThread.ManagedThreadId);

            executedThread.Should().Be(callingThread);
        }

        [Fact]
        public void Invoke_SendDelegateThrows_ExceptionPropagates()
        {
            using var ctxt = new SendQueueSyncContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            Action act = () => helper.Invoke(() => throw new InvalidOperationException("boom"));

            act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public void InvokeT_SendDelegateThrows_ExceptionPropagates()
        {
            using var ctxt = new SendQueueSyncContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            Action act = () => helper.Invoke<string>(() => throw new InvalidOperationException("boom"));

            act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public void InvokeT_PostThrows_ExecutesSynchronouslyOnCallingThread()
        {
            var ctxt = new PostThrowsSyncContext();
            var helper = new SynchronizationContextDispatcherHelper(ctxt);

            int callingThread = Thread.CurrentThread.ManagedThreadId;

            var result = helper.Invoke(() => "ok");

            result.Should().Be("ok");
        }

        public void Dispose()
        {
            _uiFixture.Dispose();
        }

        private sealed class NoSendSynchronizationContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
            private readonly Thread _thread;
            public int ThreadId { get; private set; }

            public NoSendSynchronizationContext()
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "NoSendSyncContextThread" };
                _thread.Start();

                // Wait for the thread to initialize ThreadId
                while (ThreadId == 0) Thread.Sleep(1);
            }

            private void Run()
            {
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        item.Callback(item.State);
                    }
                    catch
                    {
                        // swallow to avoid crashing the thread in tests
                    }
                }
            }

            public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
            public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

            public void Dispose()
            {
                _queue.CompleteAdding();
                _thread.Join(1000);
            }
        }

        private sealed class PostThrowsSyncContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state) => throw new InvalidOperationException("Post failed");
            public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();
        }

        private sealed class SendQueueSyncContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State, TaskCompletionSource<object?>? Tcs)> _queue = new();
            private readonly Thread _thread;

            public SendQueueSyncContext()
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "SendQueueSyncContextThread" };
                _thread.Start();
            }

            private void Run()
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    var (callback, state, tcs) = item;
                    try
                    {
                        callback(state);
                        tcs?.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs?.SetException(ex);
                    }
                }
            }

            public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state, null));

            public override void Send(SendOrPostCallback d, object? state)
            {
                var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Add((d, state, tcs));
                tcs.Task.GetAwaiter().GetResult();
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
                _thread.Join(1000);
            }
        }
    }
}
