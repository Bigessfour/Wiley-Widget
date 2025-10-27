using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Serilog;
using WileyWidget.Startup;
using Xunit;

namespace WileyWidget.Startup.Tests
{
    public class BootstrapHelpersTests
    {
        public BootstrapHelpersTests()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        [Fact]
        public void UnwrapTargetInvocationException_WithNull_ReturnsInvalidOperationException()
        {
            var result = BootstrapHelpers.UnwrapTargetInvocationException(null);

            result.Should().BeOfType<InvalidOperationException>();
            result.Message.Should().Contain("Exception was null");
        }

        [Fact]
        public void UnwrapTargetInvocationException_WithTargetInvocationException_ReturnsInnerException()
        {
            var innerException = new InvalidOperationException("Inner error");
            var targetException = new System.Reflection.TargetInvocationException(innerException);

            var result = BootstrapHelpers.UnwrapTargetInvocationException(targetException);

            result.Should().BeSameAs(innerException);
        }

        [Fact]
        public void UnwrapTargetInvocationException_WithAggregateException_ReturnsInnerException()
        {
            var innerException = new InvalidOperationException("Inner error");
            var aggregateException = new AggregateException(innerException);

            var result = BootstrapHelpers.UnwrapTargetInvocationException(aggregateException);

            result.Should().BeSameAs(innerException);
        }

        [Fact]
        public void UnwrapTargetInvocationException_WithNestedExceptions_UnwrapsAll()
        {
            var innerException = new InvalidOperationException("Actual error");
            var targetException = new System.Reflection.TargetInvocationException(innerException);
            var aggregateException = new AggregateException(targetException);

            var result = BootstrapHelpers.UnwrapTargetInvocationException(aggregateException);

            result.Should().BeSameAs(innerException);
        }

        [Fact]
        public void RetryOnException_SucceedsOnFirstAttempt()
        {
            var mockOperation = new Mock<Func<string>>();
            mockOperation.Setup(x => x()).Returns("success");

            var result = BootstrapHelpers.RetryOnException(mockOperation.Object);

            result.Should().Be("success");
            mockOperation.Verify(x => x(), Times.Once);
        }

        [Fact]
        public void RetryOnException_RetriesOnFailureAndSucceeds()
        {
            var mockOperation = new Mock<Func<string>>();
            var callCount = 0;
            mockOperation.Setup(x => x()).Returns(() =>
            {
                callCount++;
                if (callCount < 3)
                    throw new InvalidOperationException($"Attempt {callCount} failed");
                return "success";
            });

            var result = BootstrapHelpers.RetryOnException(mockOperation.Object, maxAttempts: 5, initialDelayMs: 10);

            result.Should().Be("success");
            mockOperation.Verify(x => x(), Times.Exactly(3));
        }

        [Fact]
        public void RetryOnException_ThrowsAfterMaxAttempts()
        {
            var mockOperation = new Mock<Func<string>>();
            mockOperation.Setup(x => x()).Throws<InvalidOperationException>();

            var action = () => BootstrapHelpers.RetryOnException(mockOperation.Object, maxAttempts: 3, initialDelayMs: 10);

            action.Should().Throw<InvalidOperationException>();
            mockOperation.Verify(x => x(), Times.Exactly(3));
        }

        [Fact]
        public void RetryOnException_WithNullOperation_ThrowsArgumentNullException()
        {
            Func<string>? nullOperation = null;

            var action = () => BootstrapHelpers.RetryOnException(nullOperation!, maxAttempts: 3);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("operation");
        }

        [Fact]
        public async Task RetryOnExceptionAsync_SucceedsOnFirstAttempt()
        {
            var mockOperation = new Mock<Func<Task<string>>>();
            mockOperation.Setup(x => x()).ReturnsAsync("success");

            var result = await BootstrapHelpers.RetryOnExceptionAsync(mockOperation.Object);

            result.Should().Be("success");
            mockOperation.Verify(x => x(), Times.Once);
        }

        [Fact]
        public async Task RetryOnExceptionAsync_RetriesOnFailureAndSucceeds()
        {
            var callCount = 0;
            Func<Task<string>> operation = () =>
            {
                callCount++;
                if (callCount < 3)
                    throw new InvalidOperationException($"Attempt {callCount} failed");
                return Task.FromResult("success");
            };

            var result = await BootstrapHelpers.RetryOnExceptionAsync(operation, maxAttempts: 5, initialDelayMs: 10);

            result.Should().Be("success");
            callCount.Should().Be(3);
        }

        [Fact]
        public async Task RetryOnExceptionAsync_ThrowsAfterMaxAttempts()
        {
            var mockOperation = new Mock<Func<Task<string>>>();
            mockOperation.Setup(x => x()).ThrowsAsync(new InvalidOperationException("Always fails"));

            var action = async () => await BootstrapHelpers.RetryOnExceptionAsync(mockOperation.Object, maxAttempts: 3, initialDelayMs: 10);

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Always fails");
            mockOperation.Verify(x => x(), Times.Exactly(3));
        }

        [Fact]
        public async Task RetryOnExceptionAsync_RespectsCancellationToken()
        {
            var cts = new CancellationTokenSource();
            var callCount = 0;

            Func<Task<string>> operation = async () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("First attempt");
                }
                await Task.Delay(100);
                return "success";
            };

            cts.CancelAfter(50);

            var action = async () => await BootstrapHelpers.RetryOnExceptionAsync(
                operation,
                maxAttempts: 5,
                initialDelayMs: 100,
                cancellationToken: cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
            callCount.Should().Be(1);
        }

        [Fact]
        public async Task RetryOnExceptionAsync_WithNullOperation_ThrowsArgumentNullException()
        {
            Func<Task<string>>? nullOperation = null;

            var action = async () => await BootstrapHelpers.RetryOnExceptionAsync(nullOperation!);

            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("operation");
        }

        [Fact]
        public void LogExceptionDetails_WithNullException_DoesNotThrow()
        {
            var action = () => BootstrapHelpers.LogExceptionDetails(null);

            action.Should().NotThrow();
        }

        [Fact]
        public void LogExceptionDetails_WithSimpleException_Logs()
        {
            var exception = new InvalidOperationException("Test error");

            var action = () => BootstrapHelpers.LogExceptionDetails(exception);

            action.Should().NotThrow();
        }

        [Fact]
        public void LogExceptionDetails_WithNestedExceptions_LogsAll()
        {
            var innerException = new ArgumentException("Inner error");
            var middleException = new InvalidOperationException("Middle error", innerException);
            var outerException = new Exception("Outer error", middleException);

            var action = () => BootstrapHelpers.LogExceptionDetails(outerException);

            action.Should().NotThrow();
        }

        [Fact]
        public void LogExceptionDetails_WithoutStackTrace_DoesNotIncludeStackTrace()
        {
            var exception = new InvalidOperationException("Test error");

            var action = () => BootstrapHelpers.LogExceptionDetails(exception, includeStackTrace: false);

            action.Should().NotThrow();
        }
    }
}
