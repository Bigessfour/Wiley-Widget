using System.Windows.Forms;

namespace WileyWidget.WinForms
{
    /// <summary>
    /// Interface for managing views in the application.
    /// Provides abstraction for showing forms and docking panels.
    /// </summary>
    public interface IViewManager
    {
        /// <summary>
        /// Shows a view as a child form with its associated view model.
        /// </summary>
        /// <typeparam name="TForm">The form type to show.</typeparam>
        /// <typeparam name="TViewModel">The view model type.</typeparam>
        /// <param name="allowMultiple">Whether to allow multiple instances of the same form.</param>
        void ShowView<TForm, TViewModel>(bool allowMultiple = false)
            where TForm : Form
            where TViewModel : class;

        /// <summary>
        /// Docks a user control panel in the docking manager.
        /// </summary>
        /// <typeparam name="TControl">The user control type to dock.</typeparam>
        /// <param name="panelName">The name of the panel.</param>
        /// <param name="style">The docking style.</param>
        void DockPanel<TControl>(string panelName, Syncfusion.Windows.Forms.Tools.DockingStyle style)
            where TControl : UserControl;
    }
}
