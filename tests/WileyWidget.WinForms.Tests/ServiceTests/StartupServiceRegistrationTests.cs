using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.Data;

namespace WileyWidget.WinForms.Tests.ServiceTests
{
    public class StartupServiceRegistrationTests
    {
        [Fact]
        public void ConfigureServices_Resolves_CriticalStartupServices()
        {
            // Arrange: empty configuration -> triggers InMemory DB fallback
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            // Act
            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            // Assert: critical services should resolve
            var telemetry = sp.GetService(typeof(ITelemetryService)) as ITelemetryService;
            var reporter = sp.GetService(typeof(ErrorReportingService)) as ErrorReportingService;

            // AppDbContext is registered as scoped, resolve inside scope
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;

            var mainForm = sp.GetService(typeof(WileyWidget.WinForms.Forms.MainForm));
            var accountsViewModel = sp.GetService(typeof(WileyWidget.WinForms.ViewModels.AccountsViewModel));

            Assert.NotNull(telemetry);
            Assert.NotNull(reporter);
            Assert.NotNull(db);
            Assert.NotNull(mainForm);
            Assert.NotNull(accountsViewModel);
        }
    }
}
