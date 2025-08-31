using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace WileyWidget.Converters;

/// <summary>
/// Validation rule for xAI API keys with comprehensive format checking
/// </summary>
public class ApiKeyValidationRule : ValidationRule
{
    /// <summary>
    /// Minimum length for xAI API keys
    /// </summary>
    public int MinLength { get; set; } = 20;

    /// <summary>
    /// Maximum length for xAI API keys
    /// </summary>
    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// Whether to allow empty strings (for optional validation)
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <summary>
    /// Custom error message
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Validates the xAI API key format and requirements
    /// </summary>
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var apiKey = value as string;

        // Check for null/empty
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (AllowEmpty)
                return ValidationResult.ValidResult;

            return new ValidationResult(false,
                ErrorMessage ?? "API key is required");
        }

        // Trim whitespace for validation
        apiKey = apiKey.Trim();

        // Check length
        if (apiKey.Length < MinLength)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"API key must be at least {MinLength} characters long");
        }

        if (apiKey.Length > MaxLength)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"API key must be no more than {MaxLength} characters long");
        }

        // Check for valid characters (alphanumeric, hyphens, underscores, dots)
        var validPattern = @"^[a-zA-Z0-9\-_.]+$";
        if (!Regex.IsMatch(apiKey, validPattern))
        {
            return new ValidationResult(false,
                ErrorMessage ?? "API key contains invalid characters. Only letters, numbers, hyphens, underscores, and dots are allowed");
        }

        // Check for common invalid patterns
        if (apiKey.Contains(" "))
        {
            return new ValidationResult(false,
                ErrorMessage ?? "API key cannot contain spaces");
        }

        // Check for obviously fake/test keys
        var fakeKeyPatterns = new[] { "test", "fake", "dummy", "example", "your-key-here" };
        foreach (var pattern in fakeKeyPatterns)
        {
            if (apiKey.ToLower().Contains(pattern))
            {
                return new ValidationResult(false,
                    ErrorMessage ?? "This appears to be a test or placeholder API key");
            }
        }

        // Basic entropy check (should have mix of character types)
        bool hasUpper = apiKey.Any(char.IsUpper);
        bool hasLower = apiKey.Any(char.IsLower);
        bool hasDigit = apiKey.Any(char.IsDigit);
        bool hasSpecial = apiKey.Any(c => !char.IsLetterOrDigit(c));

        int entropyScore = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);

        if (entropyScore < 2)
        {
            return new ValidationResult(false,
                ErrorMessage ?? "API key should contain a mix of uppercase, lowercase, numbers, and special characters for better security");
        }

        return ValidationResult.ValidResult;
    }
}

/// <summary>
/// Validation rule for positive integer values
/// </summary>
public class PositiveIntegerValidationRule : ValidationRule
{
    /// <summary>
    /// Minimum allowed value
    /// </summary>
    public int MinValue { get; set; } = 1;

    /// <summary>
    /// Maximum allowed value
    /// </summary>
    public int MaxValue { get; set; } = int.MaxValue;

    /// <summary>
    /// Whether to allow empty strings
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <summary>
    /// Custom error message
    /// </summary>
    public string ErrorMessage { get; set; }

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var input = value as string;

        if (string.IsNullOrWhiteSpace(input))
        {
            if (AllowEmpty)
                return ValidationResult.ValidResult;

            return new ValidationResult(false,
                ErrorMessage ?? "Value is required");
        }

        if (!int.TryParse(input, NumberStyles.Integer, cultureInfo, out int result))
        {
            return new ValidationResult(false,
                ErrorMessage ?? "Please enter a valid whole number");
        }

        if (result < MinValue)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"Value must be at least {MinValue}");
        }

        if (result > MaxValue)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"Value must be no more than {MaxValue}");
        }

        return ValidationResult.ValidResult;
    }
}

/// <summary>
/// Validation rule for positive decimal values (currency/money)
/// </summary>
public class PositiveDecimalValidationRule : ValidationRule
{
    /// <summary>
    /// Minimum allowed value
    /// </summary>
    public decimal MinValue { get; set; } = 0.01M;

    /// <summary>
    /// Maximum allowed value
    /// </summary>
    public decimal MaxValue { get; set; } = 10000M;

    /// <summary>
    /// Whether to allow empty strings
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <summary>
    /// Custom error message
    /// </summary>
    public string ErrorMessage { get; set; }

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var input = value as string;

        if (string.IsNullOrWhiteSpace(input))
        {
            if (AllowEmpty)
                return ValidationResult.ValidResult;

            return new ValidationResult(false,
                ErrorMessage ?? "Value is required");
        }

        if (!decimal.TryParse(input, NumberStyles.Currency, cultureInfo, out decimal result))
        {
            return new ValidationResult(false,
                ErrorMessage ?? "Please enter a valid monetary amount");
        }

        if (result < MinValue)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"Amount must be at least {MinValue:C}");
        }

        if (result > MaxValue)
        {
            return new ValidationResult(false,
                ErrorMessage ?? $"Amount must be no more than {MaxValue:C}");
        }

        return ValidationResult.ValidResult;
    }
}
