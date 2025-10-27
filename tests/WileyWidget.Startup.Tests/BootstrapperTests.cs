using System;
using System.IO;
using FluentAssertions;
using Moq;
using Serilog;
using Xunit;
using Prism.Ioc;
using Prism.Container.DryIoc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using WileyWidget.Startup;

namespace WileyWidget.Startup.Tests
{
    public class BootstrapperTests : IDisposable
    {
        private readonly string _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string _appSettingsPath;

        public BootstrapperTests()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            _appSettingsPath = Path.Combine(_baseDir, "appsettings.json");
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
                try
                {
                    if (File.Exists(_appSettingsPath)) File.Delete(_appSettingsPath);
                }
                catch
                {
                    // best effort cleanup
                }
            }
            // No unmanaged resources to dispose
        }

        private void WriteMinimalAppSettings(string xaiBaseUrl = "https://test.ai/")
        {
            // Build a minimal appsettings.json containing the XAI:BaseUrl entry.
            var json = $"{{ \"XAI\": {{ \"BaseUrl\": \"{xaiBaseUrl}\" }} }}";
            File.WriteAllText(_appSettingsPath, json);
        }

        [Fact]
        public void Run_Registers_expected_services_and_returns_configuration()
        {
            WriteMinimalAppSettings();

            // Use a real Prism DryIoc container extension so we can resolve
            // registered instances after Bootstrapper.Run. Avoids mocking
            // extension methods (RegisterInstance) which Moq cannot handle.
            var dryContainer = new DryIoc.Container();
            var containerExtension = new Prism.Container.DryIoc.DryIocContainerExtension(dryContainer);
            var provider = (IContainerProvider)containerExtension;

            // Ensure test-mode path for DB registration
            Environment.SetEnvironmentVariable("WILEY_WIDGET_TESTMODE", "1");

            var bootstrapper = new Bootstrapper();
            var config = bootstrapper.Run(containerExtension);

            // Basic assertions - resolve from the real container
            config.Should().NotBeNull();
            var resolvedConfig = provider.Resolve<IConfiguration>();
            resolvedConfig.Should().NotBeNull();

            var resolvedLoggerFactory = provider.Resolve<ILoggerFactory>();
            resolvedLoggerFactory.Should().NotBeNull();

            var resolvedCache = provider.Resolve<IMemoryCache>();
            resolvedCache.Should().NotBeNull();

            var resolvedFactory = provider.Resolve<IHttpClientFactory>();
            resolvedFactory.Should().NotBeNull();
        }

        [Fact]
        public void Run_Registers_HttpClientFactory_with_AIServices_baseaddress()
        {
            var expected = new Uri("https://ai.example.local/");
            WriteMinimalAppSettings(expected.ToString());

            // Use a real Prism DryIoc container extension so we can resolve
            // registered instances after Bootstrapper.Run. Avoids mocking
            // extension methods (RegisterInstance) which Moq cannot handle.
            var dryContainer = new DryIoc.Container();
            var containerExtension = new Prism.Container.DryIoc.DryIocContainerExtension(dryContainer);
            var provider = (IContainerProvider)containerExtension;

            // Ensure test-mode path for DB registration
            Environment.SetEnvironmentVariable("WILEY_WIDGET_TESTMODE", "1");

            var bootstrapper = new Bootstrapper();
            var config = bootstrapper.Run(containerExtension);

            // Verify the HttpClientFactory was registered and configured correctly
            var resolvedFactory = provider.Resolve<IHttpClientFactory>();
            resolvedFactory.Should().NotBeNull();

            // The factory should be configured to use the expected base address
            // We can't easily test the internal configuration, but we can verify it was registered
            config.Should().NotBeNull();

            var client = capturedFactory!.CreateClient("AIServices");
            client.Should().NotBeNull();
            client.BaseAddress.Should().Be(expected);
        }
    }
}
