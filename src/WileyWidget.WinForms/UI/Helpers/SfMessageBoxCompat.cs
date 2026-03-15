using System.Windows.Forms;

namespace WileyWidget.WinForms.UI.Helpers;

/// <summary>
/// Compatibility shim that routes MessageBox usage through the themed dialog helper.
/// </summary>
public static class SfMessageBoxCompat
{
    public static DialogResult Show(string text) =>
        SfDialogHelper.ShowDialogResult(null, string.Empty, text, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption) =>
        SfDialogHelper.ShowDialogResult(null, caption, text, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) =>
        SfDialogHelper.ShowDialogResult(null, caption, text, buttons, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        SfDialogHelper.ShowDialogResult(null, caption, text, buttons, icon);

    public static DialogResult Show(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton) =>
        SfDialogHelper.ShowDialogResult(null, caption, text, buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window owner, string text) =>
        SfDialogHelper.ShowDialogResult(owner, string.Empty, text, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window owner, string text, string caption) =>
        SfDialogHelper.ShowDialogResult(owner, caption, text, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) =>
        SfDialogHelper.ShowDialogResult(owner, caption, text, buttons, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        SfDialogHelper.ShowDialogResult(owner, caption, text, buttons, icon);

    public static DialogResult Show(
        IWin32Window owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton) =>
        SfDialogHelper.ShowDialogResult(owner, caption, text, buttons, icon, defaultButton);
}