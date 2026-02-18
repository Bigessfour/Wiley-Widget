using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.Controls.Base
{
    /// <summary>
    /// Common contract for panels that support async loading, validation, and completion.
    /// </summary>
    public interface ICompletablePanel
    {
        Task LoadAsync(CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
        Task<ValidationResult> ValidateAsync(CancellationToken ct = default);
        void FocusFirstError();
    }

    public record ValidationResult(bool IsValid, ValidationItem[] Errors)
    {
        public static ValidationResult Success => new(true, Array.Empty<ValidationItem>());
        public static ValidationResult Failed(params ValidationItem[] errors) => new(false, errors);
    }

    public record ValidationItem(string FieldName, string Message, ValidationSeverity Severity = ValidationSeverity.Error, System.Windows.Forms.Control? ControlRef = null);

    public enum ValidationSeverity { Info, Warning, Error }
}