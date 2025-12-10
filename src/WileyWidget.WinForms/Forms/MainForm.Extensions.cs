using System.Windows.Forms;

namespace WileyWidget.WinForms.Forms
{
    public partial class MainForm
    {
        /// <summary>
        /// Minimal bridge for panels expecting ShowPanel. Delegates to DockUserControlPanel when available.
        /// </summary>
        public void ShowPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                var method = GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.MakeGenericMethod(typeof(TPanel)).Invoke(this, new object[] { panelName });
            }
            catch
            {
                // Fallback no-op for now
            }
        }
    }
}
