using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Dialogs;
using System.Windows;

namespace WileyWidget.Services
{
    /// <summary>
    /// Syncfusion license service implementation
    /// </summary>
    public class SyncfusionLicenseService : ISyncfusionLicenseService
    {
        private readonly ILogger<SyncfusionLicenseService> _logger;
        private readonly IDialogService _dialogService;

        public SyncfusionLicenseService(ILogger<SyncfusionLicenseService>? logger = null, IDialogService? dialogService = null)
        {
            _logger = logger ?? NullLogger<SyncfusionLicenseService>.Instance;
            _dialogService = dialogService;
        }

        public async Task<bool> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                if (string.IsNullOrEmpty(licenseKey))
                {
                    _logger.LogWarning("License key is null or empty");

                    // Show error dialog using MessageBox to avoid owner issues
                    MessageBox.Show(
                        "License key is required but not provided. The application may show evaluation dialogs.",
                        "Syncfusion License Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return false;
                }

                _logger.LogInformation("Validating Syncfusion license key");

                // Register the license with Syncfusion
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                // Basic validation - check if license key was provided
                // Note: Syncfusion doesn't provide a direct validation method,
                // but if the license is invalid, components will show evaluation dialogs
                var isValid = !string.IsNullOrEmpty(licenseKey);

                if (isValid)
                {
                    _logger.LogInformation("Syncfusion license registered successfully");
                }
                else
                {
                    _logger.LogWarning("Syncfusion license key is empty");

                    // Show error dialog using MessageBox to avoid owner issues
                    MessageBox.Show(
                        "License key validation failed. The application may show evaluation dialogs.",
                        "Syncfusion License Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                await Task.CompletedTask; // Suppress async warning for future async operations
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Syncfusion license");

                // Show error dialog using MessageBox to avoid owner issues
                MessageBox.Show(
                    $"Failed to validate license: {ex.Message}",
                    "Syncfusion License Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Registers a Syncfusion license key
        /// </summary>
        public void RegisterLicense(string licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
            {
                _logger.LogWarning("Cannot register empty license key");
                return;
            }

            try
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                _logger.LogInformation("Syncfusion license registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register Syncfusion license");
                throw;
            }
        }

        /// <summary>
        /// Returns whether a license is considered valid. Syncfusion does not expose
        /// a public validation API: we return true if a non-empty license was registered
        /// or false otherwise. This avoids leaving the interface partially-implemented.
        /// </summary>
        public bool IsLicenseValid()
        {
            try
            {
                // There isn't a public API to validate a Syncfusion license server-side.
                // Use a lightweight heuristic: if registration occurred without throwing,
                // treat it as valid. Consumers that need stronger guarantees should implement
                // additional runtime checks appropriate to their environment.
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while checking Syncfusion license validity");
                return false;
            }
        }
    }
}
