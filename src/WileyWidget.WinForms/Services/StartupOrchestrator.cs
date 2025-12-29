using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using WileyTheme = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Coordinates startup tasks (license registration, theming, DI validation) so they can be tested and reused.
    /// </summary>
    public interface IStartupOrchestrator
    {
        Task RegisterLicenseAsync(CancellationToken cancellationToken = default);

        Task InitializeThemeAsync(CancellationToken cancellationToken = default);

        Task ValidateServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
    }

    public sealed class StartupOrchestrator : IStartupOrchestrator
    {
        private readonly IConfiguration _configuration;
        private readonly IWinFormsDiValidator _validator;
        private readonly ILogger<StartupOrchestrator> _logger;

        public StartupOrchestrator(
            IConfiguration configuration,
            IWinFormsDiValidator validator,
            ILogger<StartupOrchestrator> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task RegisterLicenseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var licenseKey = _configuration["Syncfusion:LicenseKey"];
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                throw new InvalidOperationException("Syncfusion license key not found in configuration.");
            }

            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            _logger.LogInformation("Syncfusion license registered successfully");

            return Task.CompletedTask;
        }

        public Task InitializeThemeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            SkinManager.ApplicationVisualTheme = WileyTheme.DefaultTheme;
            _logger.LogInformation("Application theme set to {Theme}", WileyTheme.DefaultTheme);

            return Task.CompletedTask;
        }

        public Task ValidateServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = _validator.ValidateAll(serviceProvider);
            if (!result.IsValid)
            {
                _logger.LogError("DI validation failed with {ErrorCount} errors", result.Errors.Count);
                throw new InvalidOperationException(
                    $"DI validation failed with {result.Errors.Count} errors: {string.Join("; ", result.Errors)}");
            }

            _logger.LogInformation(
                "DI validation succeeded: {ServicesValidated} services validated with {Warnings} warnings",
                result.SuccessMessages.Count,
                result.Warnings.Count);

            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("DI validation warning: {Warning}", warning);
            }

            return Task.CompletedTask;
        }
    }
}
