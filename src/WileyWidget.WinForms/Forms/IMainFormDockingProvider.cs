using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Minimal surface required by <see cref="PanelNavigationService"/> to access docking manager and central panel.
    /// Implemented by <see cref="MainForm"/>.
    /// </summary>
    public interface IMainFormDockingProvider
    {
        /// <summary>
        /// Gets the application's <see cref="DockingManager"/> instance.
        /// </summary>
        DockingManager GetDockingManager();

        /// <summary>
        /// Gets the central document panel control used for docking panels.
        /// </summary>
        Control GetCentralDocumentPanel();
    }
}