namespace WileyWidget.WinForms.Controls.Base;

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// DEPRECATED: This class exists only for backward compatibility with code
/// that previously used custom gradient panels (GradientPanelExt / SafeGradientPanel).
///
/// It has been replaced by standard System.Windows.Forms.Panel + SfSkinManager theming.
/// All gradient / corner / custom color features have been removed due to
/// layout instability bugs (StackOverflowException, recursive positioning).
///
/// New code should use plain Panel directly.
/// Existing code should be migrated to Panel over time.
/// </summary>
public class LegacyGradientPanel : Panel
{
    // Backward-compatibility stubs — these do nothing
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? BackgroundColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? GradientEnd { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? GradientStart { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GradientAngle { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseGradient { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeName { get; set; }
}

[System.Obsolete("GradientPanelExt has been replaced by LegacyGradientPanel. Use LegacyGradientPanel or Panel for new code.", false)]
public class GradientPanelExt : LegacyGradientPanel
{
}
