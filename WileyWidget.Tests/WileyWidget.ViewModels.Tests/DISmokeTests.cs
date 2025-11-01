using System;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Configuration;
using WileyWidget.Services;
using WileyWidget.ViewModels.Main;
using Xunit;

namespace WileyWidget.Tests
{
    /// <summary>
    /// Minimal DI smoke tests to catch missing registrations or constructor changes early
    /// without requiring full WPF startup.
    /// </summary>
    public class DISmokeTests
    {
        [Fact]
        public void Container_Resolves_SettingsViewModel_With_Registered_Dependencies()
        {
            // Arrange: minimal, focused container
            var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
            var container = new Container(rules);

            // Core options for SettingsViewModel
            var appOptions = Options.Create(new AppOptions());
            var optionsMonitor = Mock.Of<IOptionsMonitor<AppOptions>>(m => m.CurrentValue == appOptions.Value);

            // Register constructor dependencies with benign mocks
            container.RegisterInstance<ILogger<SettingsViewModel>>(NullLogger<SettingsViewModel>.Instance);
            container.RegisterInstance<IOptions<AppOptions>>(appOptions);
            container.RegisterInstance<IOptionsMonitor<AppOptions>>(optionsMonitor);
            container.RegisterInstance<WileyWidget.Business.Interfaces.IUnitOfWork>(new Mock<WileyWidget.Business.Interfaces.IUnitOfWork>().Object);
            container.RegisterInstance(new Mock<WileyWidget.Data.AppDbContext>().Object);
            container.RegisterInstance<ISecretVaultService>(new Mock<ISecretVaultService>().Object);
            container.RegisterInstance<IQuickBooksService>(new Mock<IQuickBooksService>().Object);
            container.RegisterInstance<ISyncfusionLicenseService>(new Mock<ISyncfusionLicenseService>().Object);
            container.RegisterInstance<IAIService>(new Mock<IAIService>().Object);
            container.RegisterInstance<IAuditService>(new Mock<IAuditService>().Object);
            container.RegisterInstance<ISettingsService>(new Mock<ISettingsService>().Object);
            container.RegisterInstance<Prism.Dialogs.IDialogService>(new Mock<Prism.Dialogs.IDialogService>().Object);

            // Register the ViewModel under test
            container.Register<SettingsViewModel>(reuse: Reuse.Transient);

            // Act
            var vm = container.Resolve<SettingsViewModel>();

            // Assert
            Assert.NotNull(vm);
        }

        [Fact]
        public void Container_Resolves_AppDbContext_When_Registered()
        {
            var container = new Container(Rules.Default.WithMicrosoftDependencyInjectionRules());
            var ctx = new Mock<WileyWidget.Data.AppDbContext>().Object;
            container.RegisterInstance(ctx);

            var resolved = container.Resolve<WileyWidget.Data.AppDbContext>();
            Assert.NotNull(resolved);
        }
    }
}
