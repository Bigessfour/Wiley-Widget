namespace WileyWidget.WinForms.Controls.Base;

/// <summary>
/// Defines a control contract that supports runtime theme application.
/// </summary>
public interface IThemable
{
    /// <summary>
    /// Applies a Syncfusion theme name to the control.
    /// </summary>
    /// <param name="themeName">Theme name such as Office2019Colorful.</param>
    void ApplyTheme(string themeName);
}
