using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service for managing application settings.
    /// Wraps ISettingsService with validation and error handling.
    /// </summary>
    public interface ISettingsManagementService
    {
        /// <summary>
        /// Loads all application settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Settings DTO</returns>
        Task<SettingsDto> LoadSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves application settings after validation.
        /// </summary>
        /// <param name="settings">Settings to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or validation errors</returns>
        Task<SettingsSaveResult> SaveSettingsAsync(SettingsDto settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates settings DTO.
        /// </summary>
        /// <param name="settings">Settings to validate</param>
        /// <returns>Collection of validation error messages (empty if valid)</returns>
        IEnumerable<string> ValidateSettings(SettingsDto settings);
    }

    /// <summary>
    /// Settings data transfer object.
    /// </summary>
    public record SettingsDto(
        string DatabaseConnectionString,
        string ApplicationName,
        string LogLevel,
        bool EnableTelemetry,
        int CacheExpirationMinutes);

    /// <summary>
    /// Result of save settings operation.
    /// </summary>
    public record SettingsSaveResult(
        bool Success,
        IReadOnlyList<string> ValidationErrors);
}
