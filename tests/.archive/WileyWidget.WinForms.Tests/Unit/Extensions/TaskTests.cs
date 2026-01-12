using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Extensions
{
    /// <summary>
    /// Tests for System.Threading.Tasks.Task behaviors.
    /// </summary>
    public class TaskTests
    {
        [Fact]
        public async Task Task_CompletesSuccessfully()
        {
            var completed = false;
            await Task.Run(() => completed = true);
            Assert.True(completed);
        }

        [Fact]
        public async Task Task_ReturnsExpectedResult()
        {
            var result = await Task.Run(() => 42);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Task_ThrowsException()
        {
            async Task ThrowAsync() => await Task.Run(() => throw new InvalidOperationException("fail"));
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(ThrowAsync);
            Assert.Equal("fail", ex.Message);
        }

        [Fact]
        public async Task Task_CanBeCancelled()
        {
            using var cts = new CancellationTokenSource();
            var token = cts.Token;
            var started = false;
            var startSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(async () =>
            {
                started = true;
                startSignal.TrySetResult(null);
                await Task.Delay(5000, token);
            }, token);

            await startSignal.Task;

            cts.Cancel();

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
            Assert.True(started);
        }

        [Fact]
        public async Task Task_DelayCompletes()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(100);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds >= 90);
        }

        [Fact]
        public async Task Task_WhenAll_WaitsForAllTasks()
        {
            var t1 = Task.Delay(50);
            var t2 = Task.Delay(100);
            await Task.WhenAll(t1, t2);
            Assert.True(t1.IsCompleted && t2.IsCompleted);
        }

        [Fact]
        public async Task Task_WhenAny_CompletesOnFirst()
        {
            var t1 = Task.Delay(50);
            var t2 = Task.Delay(200);
            var completed = await Task.WhenAny(t1, t2);
            Assert.Equal(t1, completed);
        }

    }
}
