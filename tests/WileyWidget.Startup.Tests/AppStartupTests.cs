using Xunit;
using Moq;
using Prism.Ioc;
using Prism.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Markup;
using WileyWidget;
using WileyWidget.Services;
using FluentAssertions;
using System;

namespace WileyWidget.Startup.Tests
{
    public class AppStartupTests : IDisposable
    {
        private App? _app;

        public AppStartupTests()
        {
            // Setup minimal logging for tests
            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _app?.Shutdown();
                _app = null;
            }
        }

        [Fact]
        public void CreateContainerExtension_ReturnsDryIoc()
        {
            _app = new App();
            var method = typeof(App).GetMethod("CreateContainerExtension", BindingFlags.NonPublic | BindingFlags.Instance);
            var extension = method?.Invoke(_app, null);
            extension.Should().BeOfType<DryIoc.Prism.DryIocContainerExtension>();
        }

        [Fact]
        public void RegisterModuleWithHealthTracking_RegistersAndTracks()
        {
            var mockCatalog = new Mock<IModuleCatalog>();
            var mockHealth = new Mock<IModuleHealthService>();
            _app = new App();

            var method = typeof(App).GetMethod("RegisterModuleWithHealthTracking", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_app, new object[] { mockCatalog.Object, mockHealth.Object, "TestModule", (Action)(() => mockCatalog.Setup(c => c.AddModule(typeof(TestModule)))) });

            mockHealth.Verify(h => h.RegisterModule("TestModule"), Times.Once);
            // Note: MarkModuleInitialized is called in catch, so not verified here for success case
        }

        [Fact]
        public void SetupGlobalExceptionHandling_HandlesUIExceptions()
        {
            _app = new App();
            var method = typeof(App).GetMethod("SetupGlobalExceptionHandling", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_app, null);

            // Simulate DispatcherUnhandledException
            var ex = new XamlParseException("Test");
            var args = new DispatcherUnhandledExceptionEventArgs { Exception = ex };
            Application.Current.DispatcherUnhandledException?.Invoke(null, args);
            args.Handled.Should().BeTrue();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void RegisterModuleWithHealthTracking_HandlesRetries(int retryCount)
        {
            var mockCatalog = new Mock<IModuleCatalog>();
            var mockHealth = new Mock<IModuleHealthService>();
            _app = new App();

            // Simulate failure on first attempts
            int callCount = 0;
            Action registerAction = () =>
            {
                callCount++;
                if (callCount < retryCount)
                    throw new Exception("Simulated failure");
            };

            var method = typeof(App).GetMethod("RegisterModuleWithHealthTracking", BindingFlags.NonPublic | BindingFlags.Instance);
            if (callCount < retryCount)
            {
                Action act = () => method?.Invoke(_app, new object[] { mockCatalog.Object, mockHealth.Object, "TestModule", registerAction });
                act.Should().Throw<Exception>();
            }
            else
            {
                method?.Invoke(_app, new object[] { mockCatalog.Object, mockHealth.Object, "TestModule", registerAction });
                mockHealth.Verify(h => h.RegisterModule("TestModule"), Times.Once);
            }
        }

        [Fact]
        public void RegisterModuleWithHealthTracking_HandlesNullParameters()
        {
            _app = new App();
            var method = typeof(App).GetMethod("RegisterModuleWithHealthTracking", BindingFlags.NonPublic | BindingFlags.Instance);

            Action act = () => method?.Invoke(_app, new object[] { null, null, null, null });
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void SetupGlobalExceptionHandling_HandlesDeepExceptionChains()
        {
            _app = new App();
            var method = typeof(App).GetMethod("SetupGlobalExceptionHandling", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_app, null);

            // Create deep exception chain
            Exception? inner = new Exception("Inner");
            for (int i = 0; i < 25; i++)
            {
                inner = new Exception($"Level {i}", inner);
            }

            var args = new DispatcherUnhandledExceptionEventArgs { Exception = inner };
            Application.Current.DispatcherUnhandledException?.Invoke(null, args);
            args.Handled.Should().BeTrue();
        }

        [Fact]
        public void CreateContainerExtension_WithMockConfiguration()
        {
            var mockConfig = new Mock<IConfigurationRoot>();
            // Assuming App has a way to inject config, but for now, just test the method
            _app = new App();
            var method = typeof(App).GetMethod("CreateContainerExtension", BindingFlags.NonPublic | BindingFlags.Instance);
            var extension = method?.Invoke(_app, null);
            extension.Should().NotBeNull();
        }

        // Integration test using FLAUI for UI startup
        [Fact]
        public void AppStartup_IntegrationTest()
        {
            // This would use FLAUI to start the app and verify UI elements
            // For now, placeholder
            _app = new App();
            _app.Should().NotBeNull();
            // FLAUI code would go here: var app = FlaUI.Core.Application.Launch("WileyWidget.exe");
            // Then assert on UI elements
        }
    }

    // Dummy module for testing
    public class TestModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider) { }
        public void RegisterTypes(IContainerRegistry containerRegistry) { }
    }
}
