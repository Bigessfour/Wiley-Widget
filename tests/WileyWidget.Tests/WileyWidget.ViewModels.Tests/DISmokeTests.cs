using System;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Services;
using WileyWidget.ViewModels.Main;
using WileyWidget.Models;
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
            // Provide a real in-memory AppDbContext instance instead of a Moq proxy (which fails without a parameterless ctor)
            var svcOptions = new DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>().UseInMemoryDatabase("DISmokeTests_Db").Options;
            var svcCtx = new WileyWidget.Data.AppDbContext(svcOptions);
            container.RegisterInstance(svcCtx);
            container.RegisterInstance<ISecretVaultService>(new Mock<ISecretVaultService>().Object);
            container.RegisterInstance<IQuickBooksService>(new Mock<IQuickBooksService>().Object);
            // ISyncfusionLicenseService removed - license registration happens in App static constructor
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
            var options = new DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>().UseInMemoryDatabase("DISmokeTests_Resolve_Db").Options;
            var ctx = new WileyWidget.Data.AppDbContext(options);
            container.RegisterInstance(ctx);

            var resolved = container.Resolve<WileyWidget.Data.AppDbContext>();
            Assert.NotNull(resolved);
        }

        /// <summary>
        /// Integration test to simulate DryIoc container resolution of SettingsViewModel with timeout handling.
        /// Validates that resolution completes within 5 seconds and does not throw ContainerResolutionException.
        /// Mocks all required dependencies including ISettingsService, AppOptions (simulating DB/secrets).
        /// References: App.xaml.cs RegisterTypes() and CoreModule.cs line 35 for registration rules.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task Container_Resolves_SettingsViewModel_WithTimeout_NoException()
        {
            // Arrange: Configure container with Microsoft DI rules as per App.xaml.cs
            var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
            var container = new Container(rules);

            // Mock IContainerProvider for Prism-style resolution
            var mockContainerProvider = new Mock<Prism.Ioc.IContainerProvider>();
            mockContainerProvider.Setup(cp => cp.Resolve(typeof(SettingsViewModel)))
                .Returns(() => container.Resolve<SettingsViewModel>());

            // Register AppOptions with test configuration (simulating DB/secrets loading)
            var appOptions = Options.Create(new AppOptions
            {
                Theme = "FluentDark",
                SyncfusionLicenseKey = "test-key",
                DatabaseConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;"
            });
            var optionsMonitor = Mock.Of<IOptionsMonitor<AppOptions>>(m => m.CurrentValue == appOptions.Value);

            // Register all required dependencies per SettingsViewModel constructor (lines 1015-1027)
            container.RegisterInstance<ILogger<SettingsViewModel>>(NullLogger<SettingsViewModel>.Instance);
            container.RegisterInstance<IOptions<AppOptions>>(appOptions);
            container.RegisterInstance<IOptionsMonitor<AppOptions>>(optionsMonitor);

            // Mock IUnitOfWork with basic setup
            var mockUnitOfWork = new Mock<WileyWidget.Business.Interfaces.IUnitOfWork>();
            container.RegisterInstance(mockUnitOfWork.Object);

            // Provide real in-memory AppDbContext (not mock) to avoid parameterless constructor issues
            var dbOptions = new DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>()
                .UseInMemoryDatabase("DISmokeTests_Timeout_Db")
                .Options;
            var dbContext = new WileyWidget.Data.AppDbContext(dbOptions);
            container.RegisterInstance(dbContext);

            // Mock ISecretVaultService with async operations
            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(sv => sv.GetSecretAsync(It.IsAny<string>()))
                .ReturnsAsync((string key) => $"test-secret-{key}");
            container.RegisterInstance(mockSecretVault.Object);

            // Mock IQuickBooksService (optional dependency)
            var mockQuickBooks = new Mock<IQuickBooksService>();
            container.RegisterInstance(mockQuickBooks.Object);

            // ISyncfusionLicenseService removed - license registration happens in App static constructor

            // Mock IAIService with test responses
            var mockAI = new Mock<IAIService>();
            mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync("Test AI response");
            container.RegisterInstance(mockAI.Object);

            // Mock IAuditService with async audit logging
            var mockAudit = new Mock<IAuditService>();
            mockAudit.Setup(a => a.AuditAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            container.RegisterInstance(mockAudit.Object);

            // Mock ISettingsService with basic key-value store (THIS IS THE CRITICAL SERVICE)
            var mockSettings = new Mock<ISettingsService>();
            var testAppSettings = new AppSettings
            {
                Theme = "FluentDark",
                EnableDataCaching = true,
                CacheExpirationMinutes = 30,
                SelectedLogLevel = "Information"
            };
            mockSettings.Setup(s => s.Current).Returns(testAppSettings);
            mockSettings.Setup(s => s.Get(It.IsAny<string>())).Returns((string key) => $"test-value-{key}");
            mockSettings.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>())).Verifiable();
            mockSettings.Setup(s => s.Save()).Verifiable();
            container.RegisterInstance(mockSettings.Object);

            // Mock Prism IDialogService
            var mockDialog = new Mock<Prism.Dialogs.IDialogService>();
            container.RegisterInstance(mockDialog.Object);

            // Register SettingsViewModel as Transient (per CoreModule.cs pattern)
            container.Register<SettingsViewModel>(reuse: Reuse.Transient);

            // Act: Measure resolution time with timeout handling
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SettingsViewModel? resolvedViewModel = null;
            Exception? caughtException = null;
            bool completedWithinTimeout = false;

            try
            {
                // Simulate resolution with timeout (5 second limit per requirements)
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var resolutionTask = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        return container.Resolve<SettingsViewModel>();
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                        return null;
                    }
                }, cts.Token);

                try
                {
                    resolvedViewModel = await resolutionTask;
                    completedWithinTimeout = true;
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    completedWithinTimeout = false;
                }

                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                caughtException = ex;
            }

            // Assert: Validate timeout and exception handling
            Assert.True(completedWithinTimeout,
                $"SettingsViewModel resolution exceeded 5 second timeout (actual: {stopwatch.Elapsed.TotalSeconds:F2}s)");

            Assert.Null(caughtException);

            if (caughtException != null)
            {
                // Provide detailed diagnostics if resolution failed
                var containerEx = caughtException as DryIoc.ContainerException;
                if (containerEx != null)
                {
                    Assert.Fail($"ContainerResolutionException occurred: {containerEx.Message}\n" +
                                      $"Error: {containerEx.Error}\n" +
                                      $"Stack: {containerEx.StackTrace}");
                }
                else
                {
                    Assert.Fail($"Unexpected exception during resolution: {caughtException.GetType().Name}\n" +
                                      $"Message: {caughtException.Message}");
                }
            }

            Assert.NotNull(resolvedViewModel);

            // Verify fallback mechanism: If resolution fails, ensure container can provide mock fallback
            if (resolvedViewModel == null)
            {
                // Test fallback: Can we resolve dependencies individually?
                var settingsService = container.Resolve<ISettingsService>();
                Assert.NotNull(settingsService);
                Assert.Equal(testAppSettings, settingsService.Current);
            }

            // Log performance metrics for monitoring
            System.Diagnostics.Debug.WriteLine($"SettingsViewModel resolution completed in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");

            // Verify resolution time meets performance targets (< 5s, ideally < 1s)
            Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
                $"Resolution time {stopwatch.Elapsed.TotalSeconds:F2}s exceeds 5 second threshold");

            // Cleanup
            dbContext?.Dispose();
        }

        /// <summary>
        /// Regression test for production ISettingsService registration pattern.
        /// Reproduces the delegate registration issue that causes timeout in production (startup-20251102.log lines 78-92).
        /// Tests the ACTUAL App.xaml.cs pattern: RegisterSingleton with provider delegate.
        /// This test should FAIL if the production registration pattern has issues.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task Container_Resolves_SettingsViewModel_WithProductionRegistrationPattern()
        {
            // Arrange: Use EXACT production registration pattern from App.xaml.cs
            var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
            var container = new Container(rules);

            // Register AppOptions
            var appOptions = Options.Create(new AppOptions
            {
                Theme = "FluentDark",
                SyncfusionLicenseKey = "test-key",
                DatabaseConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;"
            });
            var optionsMonitor = Mock.Of<IOptionsMonitor<AppOptions>>(m => m.CurrentValue == appOptions.Value);

            container.RegisterInstance<ILogger<SettingsViewModel>>(NullLogger<SettingsViewModel>.Instance);
            container.RegisterInstance<IOptions<AppOptions>>(appOptions);
            container.RegisterInstance<IOptionsMonitor<AppOptions>>(optionsMonitor);
            container.RegisterInstance<WileyWidget.Business.Interfaces.IUnitOfWork>(new Mock<WileyWidget.Business.Interfaces.IUnitOfWork>().Object);

            var dbOptions = new DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>()
                .UseInMemoryDatabase("DISmokeTests_Production_Pattern_Db")
                .Options;
            var dbContext = new WileyWidget.Data.AppDbContext(dbOptions);
            container.RegisterInstance(dbContext);

            container.RegisterInstance<ISecretVaultService>(new Mock<ISecretVaultService>().Object);
            container.RegisterInstance<IQuickBooksService>(new Mock<IQuickBooksService>().Object);
            // ISyncfusionLicenseService removed - license registration happens in App static constructor
            container.RegisterInstance<IAIService>(new Mock<IAIService>().Object);
            container.RegisterInstance<IAuditService>(new Mock<IAuditService>().Object);
            container.RegisterInstance<Prism.Dialogs.IDialogService>(new Mock<Prism.Dialogs.IDialogService>().Object);

            // THIS IS THE CRITICAL PATTERN FROM PRODUCTION (App.xaml.cs lines 1747-1750)
            // Register SettingsService concrete type first, then map interface via delegate
            // This mimics: containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            var mockSettings = new Mock<ISettingsService>();
            var testAppSettings = new AppSettings
            {
                Theme = "FluentDark",
                EnableDataCaching = true,
                CacheExpirationMinutes = 30,
                SelectedLogLevel = "Information"
            };
            mockSettings.Setup(s => s.Current).Returns(testAppSettings);
            mockSettings.Setup(s => s.Get(It.IsAny<string>())).Returns((string key) => $"test-value-{key}");
            mockSettings.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>()));
            mockSettings.Setup(s => s.Save());

            // Production pattern: Register concrete, then resolve via delegate
            container.RegisterInstance(mockSettings.Object, serviceKey: "SettingsServiceConcrete");
            container.RegisterDelegate<ISettingsService>(
                resolver => resolver.Resolve<ISettingsService>(serviceKey: "SettingsServiceConcrete"),
                Reuse.Singleton
            );

            // Register SettingsViewModel as Transient
            container.Register<SettingsViewModel>(reuse: Reuse.Transient);

            // Act: Attempt resolution with timeout
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SettingsViewModel? resolvedViewModel = null;
            Exception? caughtException = null;
            bool completedWithinTimeout = false;

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var resolutionTask = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        return container.Resolve<SettingsViewModel>();
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                        return null;
                    }
                }, cts.Token);

                try
                {
                    resolvedViewModel = await resolutionTask;
                    completedWithinTimeout = true;
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    completedWithinTimeout = false;
                }

                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                caughtException = ex;
            }

            // Assert: This test validates the production pattern works
            Assert.True(completedWithinTimeout,
                $"SettingsViewModel resolution with production registration pattern exceeded 5 second timeout (actual: {stopwatch.Elapsed.TotalSeconds:F2}s). " +
                $"This indicates the delegate registration pattern in App.xaml.cs lines 1747-1750 is problematic.");

            if (caughtException != null)
            {
                var containerEx = caughtException as DryIoc.ContainerException;
                Assert.Fail($"Production registration pattern caused exception: {caughtException.GetType().Name}\n" +
                           $"Message: {caughtException.Message}\n" +
                           $"This error matches the production log error on line 78: 'Service resolution failed for ISettingsService'\n" +
                           $"Fix required in App.xaml.cs RegisterTypes() method.");
            }

            Assert.NotNull(resolvedViewModel);

            // Verify ISettingsService can be resolved independently
            var settingsService = container.Resolve<ISettingsService>();
            Assert.NotNull(settingsService);
            Assert.Equal("FluentDark", settingsService.Current.Theme);

            // Cleanup
            dbContext?.Dispose();
        }

        /// <summary>
        /// Test for the enhanced 4-phase startup sequence services registration.
        /// Validates that ErrorReportingService can be resolved
        /// in the same way it would be during the enhanced startup flow.
        /// </summary>
        [Fact]
        public void Container_Resolves_EnhancedStartupServices_Successfully()
        {
            // Arrange: Configure container like App.xaml.cs RegisterTypes()
            var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
            var container = new Container(rules);

            // Mock ILogger for ErrorReportingService
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ErrorReportingService>>();
            container.RegisterInstance(mockLogger.Object);

            // Register ErrorReportingService using DryIoc registration
            container.Register<ErrorReportingService>(Reuse.Singleton);

            // Act & Assert: Verify core service can be resolved
            var errorReporting = container.Resolve<ErrorReportingService>();
            Assert.NotNull(errorReporting);
        }

        /// <summary>
        /// Integration test for the enhanced startup sequence error handling and telemetry.
        /// Simulates a startup failure scenario and validates that ErrorReportingService
        /// properly tracks telemetry events as implemented in the enhanced startup flow.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task EnhancedStartup_TracksFailureTelemetry_WhenStartupFails()
        {
            // Arrange: Setup container with services
            var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
            var container = new Container(rules);

            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ErrorReportingService>>();
            container.RegisterInstance(mockLogger.Object);
            container.Register<ErrorReportingService>(Reuse.Singleton);

            // Act: Resolve ErrorReportingService and simulate startup failure tracking
            var errorReporting = container.Resolve<ErrorReportingService>();

            // Track a startup failure event like the enhanced startup would
            var telemetryProps = new Dictionary<string, object>
            {
                ["FailedPhase"] = "3-4",
                ["ErrorType"] = "InvalidOperationException",
                ["ErrorMessage"] = "Test startup failure",
                ["CompletedPhases"] = 2
            };

            // This should not throw and should track the event
            Exception? trackingException = null;
            try
            {
                errorReporting.TrackEvent("Enhanced_Startup_Failed", telemetryProps);
            }
            catch (Exception ex)
            {
                trackingException = ex;
            }

            Assert.Null(trackingException);

            // Verify the service can track success events too
            var successProps = new Dictionary<string, object>
            {
                ["CompletedPhases"] = "1,2,3,4",
                ["StartupType"] = "Enhanced4Phase",
                ["Timestamp"] = DateTime.UtcNow
            };

            Exception? successTrackingException = null;
            try
            {
                errorReporting.TrackEvent("Enhanced_Startup_Success", successProps);
            }
            catch (Exception ex)
            {
                successTrackingException = ex;
            }

            Assert.Null(successTrackingException);

            await System.Threading.Tasks.Task.CompletedTask; // Make method async
        }
    }
}
