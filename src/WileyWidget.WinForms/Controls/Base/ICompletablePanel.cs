#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.Controls.Base
{
    /// <summary>
    /// Common contract for user-facing panels that participate in the Sacred Panel Skeleton
    /// lifecycle (WileyWidgetUIStandards §1, §3, and §8).
    /// <para>
    /// A panel that implements this interface supports async loading, validation, save, and
    /// first-error focus so that callers (host forms, wizard coordinators) can treat all
    /// compliant panels uniformly without knowing their concrete type.
    /// </para>
    /// <para>
    /// <see cref="ScopedPanelBase{TViewModel}"/> provides a default implementation of every
    /// member; concrete panels override only the members they need to customise.
    /// </para>
    /// </summary>
    public interface ICompletablePanel
    {
        /// <summary>
        /// Asynchronously loads or reloads data into the panel.
        /// Implementations must be idempotent — safe to call multiple times.
        /// </summary>
        /// <param name="ct">Token allowing the caller to cancel the operation.</param>
        Task LoadAsync(CancellationToken ct = default);

        /// <summary>
        /// Asynchronously persists any pending changes produced by this panel.
        /// Must be a no-op when there are no unsaved changes.
        /// </summary>
        /// <param name="ct">Token allowing the caller to cancel the operation.</param>
        Task SaveAsync(CancellationToken ct = default);

        /// <summary>
        /// Validates all user inputs on this panel and returns a structured result.
        /// Implementations should populate an internal error list and return
        /// <see cref="ValidationResult.Success"/> or
        /// <see cref="ValidationResult.Failed(ValidationItem[])"/>.
        /// </summary>
        /// <param name="ct">Token allowing the caller to cancel the operation.</param>
        Task<ValidationResult> ValidateAsync(CancellationToken ct = default);

        /// <summary>
        /// Moves keyboard focus to the first control that has a validation error.
        /// No-op when there are no current errors.
        /// </summary>
        void FocusFirstError();
    }

    /// <summary>Structured result returned by <see cref="ICompletablePanel.ValidateAsync"/>.</summary>
    /// <param name="IsValid">Whether all inputs passed validation.</param>
    /// <param name="Errors">Zero or more validation items describing failures.</param>
    public record ValidationResult(bool IsValid, ValidationItem[] Errors)
    {
        /// <summary>A pre-built success result with an empty error list.</summary>
        public static ValidationResult Success => new(true, Array.Empty<ValidationItem>());

        /// <summary>Creates a failure result from one or more <see cref="ValidationItem"/> instances.</summary>
        public static ValidationResult Failed(params ValidationItem[] errors) => new(false, errors);
    }

    /// <summary>A single validation failure produced by <see cref="ICompletablePanel.ValidateAsync"/>.</summary>
    /// <param name="FieldName">The data-binding field name or control name that failed.</param>
    /// <param name="Message">Human-readable description of the failure.</param>
    /// <param name="Severity">Severity classification; defaults to <see cref="ValidationSeverity.Error"/>.</param>
    /// <param name="ControlRef">Optional reference to the WinForms control associated with this failure.</param>
    public record ValidationItem(
        string FieldName,
        string Message,
        ValidationSeverity Severity = ValidationSeverity.Error,
        System.Windows.Forms.Control? ControlRef = null);

    /// <summary>Severity levels for <see cref="ValidationItem"/>.</summary>
    public enum ValidationSeverity { Info, Warning, Error }
}
