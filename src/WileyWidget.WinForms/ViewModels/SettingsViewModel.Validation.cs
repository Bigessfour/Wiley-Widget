using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Validation-related partial class for <see cref="SettingsViewModel"/>.
    /// </summary>
    public partial class SettingsViewModel
    {
        /// <summary>
        /// Validates all settings properties and returns whether the model is valid.
        /// Call this before saving settings to ensure all values are acceptable.
        /// </summary>
        /// <returns><c>true</c> if all properties pass validation; otherwise, <c>false</c>.</returns>
        public bool ValidateSettings()
        {
            // Validate all annotated properties
            ValidateAllProperties();
            return !HasErrors;
        }

        /// <summary>
        /// Gets a user-friendly summary of all validation errors.
        /// </summary>
        /// <returns>A list of error messages, or empty if no errors.</returns>
        public IReadOnlyList<string> GetValidationSummary()
        {
            var errors = new List<string>();

            foreach (var propertyName in new[]
            {
                nameof(ConnectionString),
                nameof(AutoSaveIntervalMinutes),
                nameof(DefaultExportPath),
                nameof(DateFormat),
                nameof(CurrencyFormat),
                nameof(LogLevel)
            })
            {
                var propertyErrors = GetErrors(propertyName)?
                    .Cast<ValidationResult>()
                    .Where(r => r != ValidationResult.Success && r.ErrorMessage != null)
                    .Select(r => r.ErrorMessage!)
                    .ToList();

                if (propertyErrors?.Count > 0)
                {
                    errors.AddRange(propertyErrors);
                }
            }

            return errors;
        }

        /// <summary>
        /// Custom validation for cross-property rules.
        /// Called after property-level validation completes.
        /// </summary>
        private void ValidateCrossPropertyRules()
        {
            // Example: Auto-save interval should be reasonable when auto-save is enabled
            if (AutoSaveEnabled && AutoSaveIntervalMinutes < 1)
            {
                // This would already be caught by [Range] attribute, but shows pattern
                // for more complex cross-property rules
            }

            // Example: If logging is disabled, log level doesn't matter
            // (no validation needed in this case)
        }
    }
}
