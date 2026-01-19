using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls;

public enum PanelMode
{
    View,
    Create,
    Edit
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public record ValidationItem(string FieldName, string Message, ValidationSeverity Severity, Control? ControlRef = null);

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationItem> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<ValidationItem>());
    public static ValidationResult Failed(params ValidationItem[] items) => new(false, items);
}

public interface ICompletablePanel
{
    bool IsLoaded { get; }
    bool IsBusy { get; set; }
    bool HasUnsavedChanges { get; }
    bool IsValid { get; }
    IReadOnlyList<ValidationItem> ValidationErrors { get; }
    PanelMode Mode { get; }

    CancellationTokenSource? CurrentOperationCts { get; }

    Task<ValidationResult> ValidateAsync(CancellationToken ct);
    void FocusFirstError();
    Task SaveAsync(CancellationToken ct);
    Task LoadAsync(CancellationToken ct);

    event EventHandler? StateChanged;
}
