using System;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    internal void InvokeInitializeChrome()
    {
        InitializeChrome();
    }

    internal void InvokeOnLoad()
    {
        OnLoad(EventArgs.Empty);
    }
}
