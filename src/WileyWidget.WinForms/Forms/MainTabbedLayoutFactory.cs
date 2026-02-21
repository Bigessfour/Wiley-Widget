using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// DISABLED: Pure TabbedMDIManager layout implementation.
    /// The TabbedMDIManager API from Syncfusion does not match the assumed interface.
    /// This factory is marked obsolete and left as reference only.
    /// </summary>
    [Obsolete("TabbedMDI implementation incomplete. Use standard docking layout instead.", error: false)]
    public static class MainTabbedLayoutFactory
    {
        [Obsolete("Not implemented")]
        public static (TabbedMDIManager TabbedMdi,
                      Panel LeftPanel,
                      Panel RightPanel)
            CreatePureTabbedLayout(MainForm mainForm, IServiceProvider sp, ILogger logger)
        {
            throw new NotImplementedException("TabbedMDI layout factory needs API fixes for Syncfusion TabbedMDIManager");
        }
    }
}
