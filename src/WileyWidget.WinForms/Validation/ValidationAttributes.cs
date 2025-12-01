using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Globalization;
using System.Linq;

namespace WileyWidget.WinForms.Validation
{
    /// <summary>
    /// Validates that a string value represents an existing directory path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class DirectoryExistsAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets whether to allow empty/null values (defaults to true).
        /// </summary>
        public bool AllowEmpty { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to attempt to create the directory if it doesn't exist.
        /// </summary>
        public bool CreateIfMissing { get; set; } = false;

        public DirectoryExistsAttribute() : base("The directory path '{0}' does not exist.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                return AllowEmpty
                    ? ValidationResult.Success
                    : new ValidationResult(
                        $"The {validationContext.DisplayName} field is required.",
                        new[] { validationContext.MemberName ?? string.Empty });
            }

            var path = value.ToString();
            if (string.IsNullOrWhiteSpace(path))
            {
                return AllowEmpty
                    ? ValidationResult.Success
                    : new ValidationResult(
                        $"The {validationContext.DisplayName} field is required.",
                        new[] { validationContext.MemberName ?? string.Empty });
            }

            // Check if directory exists
            if (Directory.Exists(path))
            {
                return ValidationResult.Success;
            }

            // Optionally create the directory
            if (CreateIfMissing)
            {
                try
                {
                    Directory.CreateDirectory(path);
                    return ValidationResult.Success;
                }
                catch (Exception ex)
                {
                    return new ValidationResult(
                        $"Could not create directory '{path}': {ex.Message}",
                        new[] { validationContext.MemberName ?? string.Empty });
                }
            }

            return new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                new[] { validationContext.MemberName ?? string.Empty });
        }
    }

    /// <summary>
    /// Validates that a string value is a valid log level.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ValidLogLevelAttribute : ValidationAttribute
    {
        private static readonly HashSet<string> ValidLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
        };

        public ValidLogLevelAttribute() : base("'{0}' is not a valid log level. Valid levels are: Verbose, Debug, Information, Warning, Error, Fatal.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                // Empty is typically handled by [Required] if needed
                return ValidationResult.Success;
            }

            var level = value.ToString();
            if (string.IsNullOrWhiteSpace(level) || ValidLevels.Contains(level))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                FormatErrorMessage(level),
                new[] { validationContext.MemberName ?? string.Empty });
        }
    }

    /// <summary>
    /// Validates that an integer value is within a specified range with descriptive messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class PositiveRangeAttribute : ValidationAttribute
    {
        public int Minimum { get; }
        public int Maximum { get; }
        public string? Unit { get; set; }

        public PositiveRangeAttribute(int minimum, int maximum)
            : base($"Value must be between {{0}} and {{1}}{(string.IsNullOrEmpty("") ? "" : " {2}")}.")
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public override string FormatErrorMessage(string name)
        {
            var unit = string.IsNullOrEmpty(Unit) ? "" : $" {Unit}";
            return $"{name} must be between {Minimum} and {Maximum}{unit}.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null)
            {
                return ValidationResult.Success; // Use [Required] for null checks
            }

            if (value is int intValue)
            {
                if (intValue >= Minimum && intValue <= Maximum)
                {
                    return ValidationResult.Success;
                }
            }
            else if (int.TryParse(value.ToString(), out var parsed))
            {
                if (parsed >= Minimum && parsed <= Maximum)
                {
                    return ValidationResult.Success;
                }
            }

            return new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                new[] { validationContext.MemberName ?? string.Empty });
        }
    }

    /// <summary>
    /// Validates that a string value matches a valid .NET format string pattern.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ValidFormatStringAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets the format type to validate against.
        /// </summary>
        public FormatStringType FormatType { get; set; } = FormatStringType.Any;

        public ValidFormatStringAttribute() : base("'{0}' is not a valid format string.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                return ValidationResult.Success; // Use [Required] for null checks
            }

            var format = value.ToString();
            if (string.IsNullOrWhiteSpace(format))
            {
                return ValidationResult.Success;
            }

            try
            {
                // Validate by attempting to format a test value
                switch (FormatType)
                {
                    case FormatStringType.DateTime:
                        _ = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
                        break;
                    case FormatStringType.Numeric:
                        _ = 1234.56m.ToString(format, CultureInfo.InvariantCulture);
                        break;
                    case FormatStringType.Any:
                    default:
                        // Try both - if either works, it's valid
                        var dateValid = TryFormatDate(format);
                        var numValid = TryFormatNumber(format);
                        if (!dateValid && !numValid)
                        {
                            return new ValidationResult(
                                FormatErrorMessage(validationContext.DisplayName),
                                new[] { validationContext.MemberName ?? string.Empty });
                        }
                        break;
                }

                return ValidationResult.Success;
            }
            catch (FormatException)
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }
        }

        private static bool TryFormatDate(string format)
        {
            try
            {
                _ = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFormatNumber(string format)
        {
            try
            {
                _ = 1234.56m.ToString(format, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Specifies the type of format string to validate.
    /// </summary>
    public enum FormatStringType
    {
        /// <summary>Accept any valid format string.</summary>
        Any,
        /// <summary>DateTime format strings only.</summary>
        DateTime,
        /// <summary>Numeric format strings only.</summary>
        Numeric
    }

    /// <summary>
    /// Validates that a string is not empty or whitespace-only when required.
    /// More user-friendly error messages than standard [Required].
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class NotEmptyAttribute : ValidationAttribute
    {
        public NotEmptyAttribute() : base("The {0} field cannot be empty.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null)
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a decimal or numeric value is greater than zero.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class PositiveValueAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets whether zero is considered a valid positive value.
        /// </summary>
        public bool AllowZero { get; set; } = false;

        public PositiveValueAttribute() : base("The {0} field must be a positive value.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null)
            {
                return ValidationResult.Success; // Use [Required] for null checks
            }

            var isValid = value switch
            {
                decimal d => AllowZero ? d >= 0 : d > 0,
                double d => AllowZero ? d >= 0 : d > 0,
                float f => AllowZero ? f >= 0 : f > 0,
                int i => AllowZero ? i >= 0 : i > 0,
                long l => AllowZero ? l >= 0 : l > 0,
                _ => true // Other types pass through
            };

            return isValid
                ? ValidationResult.Success
                : new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
        }
    }

    /// <summary>
    /// Validates that one property value is greater than another property's value.
    /// Useful for budget vs. expenditure comparisons.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class GreaterThanAttribute : ValidationAttribute
    {
        public string OtherPropertyName { get; }

        public GreaterThanAttribute(string otherPropertyName)
            : base("The {0} field must be greater than {1}.")
        {
            OtherPropertyName = otherPropertyName;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null)
            {
                return ValidationResult.Success; // Use [Required] for null checks
            }

            var otherProperty = validationContext.ObjectType.GetProperty(OtherPropertyName);
            if (otherProperty is null)
            {
                return new ValidationResult($"Unknown property: {OtherPropertyName}");
            }

            var otherValue = otherProperty.GetValue(validationContext.ObjectInstance);
            if (otherValue is null)
            {
                return ValidationResult.Success;
            }

            var comparison = Compare(value, otherValue);
            if (comparison > 0)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                string.Format(CultureInfo.InvariantCulture, ErrorMessageString, validationContext.DisplayName, OtherPropertyName),
                new[] { validationContext.MemberName ?? string.Empty });
        }

        private static int Compare(object value1, object value2)
        {
            if (value1 is IComparable comparable1 && value2.GetType() == value1.GetType())
            {
                return comparable1.CompareTo(value2);
            }

            // Try converting to decimal for numeric comparisons
            if (decimal.TryParse(value1.ToString(), out var d1) &&
                decimal.TryParse(value2.ToString(), out var d2))
            {
                return d1.CompareTo(d2);
            }

            return 0;
        }
    }

    /// <summary>
    /// Validates that a string is a valid account number format.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ValidAccountNumberAttribute : ValidationAttribute
    {
        public int MaxLength { get; set; } = 20;
        public bool AllowAlphanumeric { get; set; } = true;

        public ValidAccountNumberAttribute() : base("The {0} field must be a valid account number (max {1} characters).")
        {
        }

        public override string FormatErrorMessage(string name)
        {
            return string.Format(CultureInfo.InvariantCulture, ErrorMessageString, name, MaxLength);
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext is null) throw new ArgumentNullException(nameof(validationContext));
            if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                return ValidationResult.Success; // Use [Required] for null checks
            }

            var accountNumber = value.ToString();
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return ValidationResult.Success;
            }

            if (accountNumber.Length > MaxLength)
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            if (!AllowAlphanumeric && !accountNumber.All(char.IsDigit))
            {
                return new ValidationResult(
                    $"The {validationContext.DisplayName} field must contain only digits.",
                    new[] { validationContext.MemberName ?? string.Empty });
            }

            return ValidationResult.Success;
        }
    }
}
