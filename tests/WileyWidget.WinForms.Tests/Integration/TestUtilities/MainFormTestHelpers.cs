using System;
using System.Reflection;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.Tests.Integration.TestUtilities;

internal static class MainFormTestHelpers
{
    private static readonly MethodInfo? OnLoadMethod = typeof(MainForm).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? InitializeChromeMethod = typeof(MainForm).GetMethod("InitializeChrome", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void InvokeOnLoad(this MainForm form)
    {
        if (OnLoadMethod is null)
        {
            throw new InvalidOperationException("Unable to locate MainForm.OnLoad via reflection.");
        }

        OnLoadMethod.Invoke(form, new object[] { EventArgs.Empty });
    }

    public static void InvokeInitializeChrome(this MainForm form)
    {
        if (InitializeChromeMethod is null)
        {
            throw new InvalidOperationException("Unable to locate MainForm.InitializeChrome via reflection.");
        }

        InitializeChromeMethod.Invoke(form, null);
    }
}
