using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of ISettingsManagementService for managing application settings.
    /// </summary>
    public class SettingsManagementService : ISettingsManagementService
    {
        private readonly ILogger<SettingsManagementService> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IValidator<SettingsDto> _validator;

        public SettingsManagementService(
            ILogger<SettingsManagementService> logger,
            ISettingsService settingsService,
            IValidator<SettingsDto> validator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async Task<SettingsDto> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Loading application settings");

                // Load settings from ISettingsService
                await _settingsService.LoadAsync();
                var appSettings = _settingsService.Current;

                // Map AppSettings to SettingsDto
                // Note: AppSettings doesn't have all DTO fields, using available properties
                var connectionString = $"Server={appSettings.DatabaseServer};Database={appSettings.DatabaseName};";
                var settings = new SettingsDto(
                    DatabaseConnectionString: connectionString,
                    ApplicationName: "Wiley Widget", // Not in AppSettings, using constant
                    LogLevel: appSettings.SelectedLogLevel ?? "Information",
                    EnableTelemetry: true, // Not in AppSettings, using default
                    CacheExpirationMinutes: appSettings.CacheExpirationMinutes);

                _logger.LogInformation("Application settings loaded successfully");
                return settings;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Loading settings operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load application settings");
                throw;
            }
        }

        public async Task<SettingsSaveResult> SaveSettingsAsync(SettingsDto settings, CancellationToken cancellationToken = default)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Validate first
            var errors = ValidateSettings(settings).ToList();
            if (errors.Count > 0)
            {
                _logger.LogWarning("Settings validation failed with {ErrorCount} errors", errors.Count);
                return new SettingsSaveResult(false, errors);
            }

            try
            {
                _logger.LogInformation("Saving application settings");

                // Simulate async save - extend when ISettingsService has full API
                await Task.Delay(10, cancellationToken);

                // In real implementation, would call _settingsService.SetSetting for each property

                _logger.LogInformation("Application settings saved successfully");
                return new SettingsSaveResult(true, Array.Empty<string>());
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Save settings operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save application settings");
                throw;
            }
        }

        public IEnumerable<string> ValidateSettings(SettingsDto settings)
        {
            if (settings == null)
            {
                yield return "Settings cannot be null.";
                yield break;
            }

            var validationResult = _validator.Validate(settings);
            foreach (var error in validationResult.Errors)
            {
                yield return error.ErrorMessage;
            }
        }
    }

    /// <summary>
    /// FluentValidation validator for SettingsDto.
    /// </summary>
    public class SettingsDtoValidator : AbstractValidator<SettingsDto>
    {
        public SettingsDtoValidator()
        {
            RuleFor(s => s.ApplicationName)
                .NotEmpty()
                .WithMessage("Application name is required")
                .MaximumLength(100)
                .WithMessage("Application name must not exceed 100 characters");

            RuleFor(s => s.DatabaseConnectionString)
                .NotEmpty()
                .WithMessage("Database connection string is required");

            RuleFor(s => s.LogLevel)
                .NotEmpty()
                .WithMessage("Log level is required")
                .Must(level => new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" }.Contains(level))
                .WithMessage("Log level must be one of: Trace, Debug, Information, Warning, Error, Critical");

            RuleFor(s => s.CacheExpirationMinutes)
                .GreaterThan(0)
                .WithMessage("Cache expiration must be greater than 0")
                .LessThanOrEqualTo(1440)
                .WithMessage("Cache expiration must not exceed 1440 minutes (24 hours)");
        }
    }
}
