using System;
using System.Threading.Tasks;
using Xunit;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Tests.Unit.Extensions
{
    public class ControlSafeExtensionsTests
    {
        [Fact]
        public async Task WithTimeout_CompletesBeforeTimeout_ReturnsResult()
        {
            // Arrange
            var task = Task.FromResult(42);

            // Act
            var result = await task.WithTimeout(TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task WithTimeout_TimeoutExceeded_ThrowsTimeoutException()
        {
            // Arrange
            var task = Task.Delay(TimeSpan.FromSeconds(2));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => task.WithTimeout(TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public async Task WithTimeout_WithCancellationToken_CompletesBeforeTimeout_ReturnsResult()
        {
            // Arrange
            var task = Task.FromResult(42);
            var cts = new System.Threading.CancellationTokenSource();

            // Act
            var result = await task.WithTimeout(TimeSpan.FromSeconds(1), cts.Token);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task WithTimeout_WithCancellationToken_TimeoutExceeded_ThrowsTimeoutException()
        {
            // Arrange
            var task = Task.Delay(TimeSpan.FromSeconds(2));
            var cts = new System.Threading.CancellationTokenSource();

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => task.WithTimeout(TimeSpan.FromMilliseconds(100), cts.Token));
        }

        [Fact]
        public async Task WithTimeout_NonGeneric_CompletesBeforeTimeout_Completes()
        {
            // Arrange
            var task = Task.CompletedTask;

            // Act
            await task.WithTimeout(TimeSpan.FromSeconds(1));

            // Assert - no exception
        }

        [Fact]
        public async Task WithTimeout_NonGeneric_TimeoutExceeded_ThrowsTimeoutException()
        {
            // Arrange
            var task = Task.Delay(TimeSpan.FromSeconds(2));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => task.WithTimeout(TimeSpan.FromMilliseconds(100)));
        }
    }
}
