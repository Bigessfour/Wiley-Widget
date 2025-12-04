using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests
{
    public class ExceptionHandlingTests
    {
        [Fact]
        public void MainForm_ShowChildForm_Throws_When_ServiceNotRegistered_And_LogsError()
        {
            // Arrange: an empty provider where the child form is not registered
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var mockLogger = new Mock<ILogger<MainForm>>();
            using var mainForm = new MainForm(provider, mockLogger.Object);

            // Use reflection to invoke the private generic ShowChildForm<TForm, TViewModel>()
            var showMethod = typeof(MainForm).GetMethod("ShowChildForm", BindingFlags.NonPublic | BindingFlags.Instance);
            var generic = showMethod.MakeGenericMethod(typeof(AccountsForm), typeof(AccountsViewModel));

            // Act
            Action act = () => generic.Invoke(mainForm, null);

            // Assert: reflection wraps thrown exceptions in TargetInvocationException; inner should indicate missing service
            act.Should().Throw<TargetInvocationException>().Where(ex => ex.InnerException is InvalidOperationException);

            // And ensure the logger attempted to log an error (verify via recorded invocations to avoid delegate generic type mismatches)
            var errorLogged = mockLogger.Invocations.Any(inv => inv.Method.Name == "Log" && inv.Arguments.Count > 0 && inv.Arguments[0] is LogLevel ll && ll == LogLevel.Error);
            errorLogged.Should().BeTrue();
        }

        [Fact]
        public void MainViewModel_Constructor_Throws_When_LoggerThrows()
        {
            // Arrange
            var logger = new ThrowingLogger<MainViewModel>();
            var mockDashboardService = new Mock<WileyWidget.Services.Abstractions.IMainDashboardService>();

            // Act
            Action act = () => new MainViewModel(logger, mockDashboardService.Object);

            // Assert
            act.Should().Throw<Exception>().WithMessage("logger fail");
        }

        private class ThrowingLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                throw new InvalidOperationException("logger fail");
            }

            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
