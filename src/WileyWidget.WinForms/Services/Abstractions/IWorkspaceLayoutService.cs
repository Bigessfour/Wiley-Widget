using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services.Abstractions;

/// <summary>
/// Persists and restores the user's workspace layout for the main shell.
/// </summary>
public interface IWorkspaceLayoutService
{
    /// <summary>
    /// Supplies the live docking and ribbon controls used for persistence.
    /// </summary>
    void Configure(DockingManager dockingManager, RibbonControlAdv ribbon);

    /// <summary>
    /// Saves the current layout to persistent storage.
    /// </summary>
    void SaveLayout();

    /// <summary>
    /// Restores the last saved layout if one exists.
    /// </summary>
    void LoadLayout();

    /// <summary>
    /// Deletes any saved layout so the shell returns to its default arrangement.
    /// </summary>
    void ResetLayout();
}