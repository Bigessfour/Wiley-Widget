using WileyWidget.Models;

namespace WileyWidget.UI.Dialogs;

/// <summary>
/// Abstraction for editing an Enterprise entity via UI dialog. Allows the ViewModel
/// to remain decoupled from concrete WPF window implementations and facilitates
/// unit testing by supplying mock implementations.
/// </summary>
public interface IEnterpriseEditorDialog
{
    /// <summary>
    /// Displays an edit dialog for the provided enterprise and returns the possibly
    /// modified instance. Returns null if the operation was cancelled.
    /// </summary>
    /// <param name="enterprise">Enterprise to edit.</param>
    /// <returns>Edited enterprise instance or null if cancelled.</returns>
    Enterprise Show(Enterprise enterprise);
}

/// <summary>
/// Null-object implementation used until a concrete dialog is supplied. Simply
/// returns the original instance without modification.
/// </summary>
public sealed class NoOpEnterpriseEditorDialog : IEnterpriseEditorDialog
{
    public Enterprise Show(Enterprise enterprise) => enterprise;
}