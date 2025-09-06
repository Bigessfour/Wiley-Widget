#nullable enable

using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Configuration;

/// <summary>
/// Configuration settings for xAI API integration
/// </summary>
public class XAiSettings
{
    [Required(ErrorMessage = "xAI API key is required")]
    [MinLength(10, ErrorMessage = "API key must be at least 10 characters")]
    public string? ApiKey { get; set; }

    [Required(ErrorMessage = "xAI Base URL is required")]
    [Url(ErrorMessage = "Base URL must be a valid URL")]
    public string? BaseUrl { get; set; } = "https://api.x.ai/v1";

    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 10, ErrorMessage = "Max retries must be between 1 and 10")]
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Validation classes for service configurations
/// </summary>
public static class ServiceValidation
{
    public class XAiSettingsValidation : IValidateOptions<XAiSettings>
    {
        public ValidateOptionsResult Validate(string? name, XAiSettings options)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(options);

            if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown error");
                return ValidateOptionsResult.Fail(errors);
            }

            return ValidateOptionsResult.Success;
        }
    }

    public class DatabaseSettingsValidation : IValidateOptions<DatabaseSettings>
    {
        public ValidateOptionsResult Validate(string? name, DatabaseSettings options)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(options);

            if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown error");
                return ValidateOptionsResult.Fail(errors);
            }

            return ValidateOptionsResult.Success;
        }
    }
}
