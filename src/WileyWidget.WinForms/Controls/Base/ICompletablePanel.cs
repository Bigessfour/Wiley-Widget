using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls.Base;

/// <summary>
/// Defines the contract for panels that support completion workflows including loading, validation, saving, and state management.
/// This interface ensures consistent lifecycle management across all Wiley-Widget panels.
/// </summary>
public enum PanelMode
{
    /// <summary>
    /// Panel is in read-only view mode.
    /// </summary>
    View,

    /// <summary>
    /// Panel is creating a new item.
    /// </summary>
    Create,

    /// <summary>
    /// Panel is editing an existing item.
    /// </summary>
    Edit
}

/// <summary>
/// Represents the severity level of a validation error.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message - does not prevent saving.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - user should be aware but can proceed.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - prevents saving until resolved.
    /// </summary>
    Error
}

/// <summary>
/// Represents a single validation result item with field information and error details.
/// </summary>
/// <param name="FieldName">The name of the field that failed validation.</param>
/// <param name="Message">The user-friendly error message.</param>
/// <param name="Severity">The severity level of the validation issue.</param>
/// <param name="ControlRef">Optional reference to the UI control associated with this field.</param>
public record ValidationItem(string FieldName, string Message, ValidationSeverity Severity, Control? ControlRef = null);

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
/// <param name="IsValid">True if validation passed, false if there are errors.</param>
/// <param name="Errors">List of validation errors (empty if valid).</param>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationItem> Errors)
{
    /// <summary>
    /// Gets a singleton instance representing successful validation.
    /// </summary>
    public static ValidationResult Success { get; } = new(true, Array.Empty<ValidationItem>());

    /// <summary>
    /// Creates a failed validation result with the specified error items.
    /// </summary>
    /// <param name="items">The validation errors that occurred.</param>
    /// <returns>A ValidationResult indicating failure.</returns>
    public static ValidationResult Failed(params ValidationItem[] items) => new(false, items);
}

/// <summary>
/// Interface for panels that support completion workflows including loading, validation, saving, and state management.
/// Implementers should raise StateChanged when IsLoaded, IsBusy, HasUnsavedChanges, or Mode change.
/// </summary>
public interface ICompletablePanel
{
    /// <summary>
    /// Gets a value indicating whether the panel has been loaded and is ready for user interaction.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the panel is currently performing an async operation.
    /// Implementers should raise <see cref="StateChanged"/> when this value changes.
    /// </summary>
    bool IsBusy { get; set; }

    /// <summary>
    /// Gets a value indicating whether the panel has unsaved changes.
    /// </summary>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// Gets a value indicating whether the panel's current state is valid (no validation errors).
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Gets the list of current validation errors.
    /// </summary>
    IReadOnlyList<ValidationItem> ValidationErrors { get; }

    /// <summary>
    /// Gets the current operational mode of the panel.
    /// </summary>
    PanelMode? Mode { get; }

    /// <summary>
    /// Gets the cancellation token source for the current operation, if any.
    /// Implementers should cancel and dispose this in their Dispose method.
    /// </summary>
    CancellationTokenSource? CurrentOperationCts { get; }

    /// <summary>
    /// Validates the panel's current state asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A ValidationResult indicating success or failure with error details.</returns>
    Task<ValidationResult> ValidateAsync(CancellationToken ct);

    /// <summary>
    /// Validates the panel's current state asynchronously with optional progress reporting.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <param name="progress">Optional progress reporter for validation stages.</param>
    /// <returns>A ValidationResult indicating success or failure with error details.</returns>
    Task<ValidationResult> ValidateAsync(CancellationToken ct, IProgress<string>? progress)
        => ValidateAsync(ct);

    /// <summary>
    /// Focuses the first control that has a validation error.
    /// </summary>
    void FocusFirstError();

    /// <summary>
    /// Saves the panel's data asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveAsync(CancellationToken ct);

    /// <summary>
    /// Saves the panel's data asynchronously with optional progress reporting.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <param name="progress">Optional progress reporter for save stages.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveAsync(CancellationToken ct, IProgress<string>? progress)
        => SaveAsync(ct);

    /// <summary>
    /// Loads the panel's data asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the load operation.</returns>
    Task LoadAsync(CancellationToken ct);

    /// <summary>
    /// Loads the panel's data asynchronously with optional progress reporting.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <param name="progress">Optional progress reporter for load stages.</param>
    /// <returns>A task representing the load operation.</returns>
    Task LoadAsync(CancellationToken ct, IProgress<string>? progress)
        => LoadAsync(ct);

    /// <summary>
    /// Event raised when panel state changes (IsLoaded, IsBusy, HasUnsavedChanges, Mode).
    /// UI elements should bind to this to update enable/disable states.
    /// </summary>
    event EventHandler? StateChanged;
}
