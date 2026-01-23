namespace WileyWidget.WinForms.Controls;

using System;
using System.ComponentModel;
using System.Drawing;

/// <summary>
/// DEPRECATED: GradientPanelExt has been replaced with standard Panel for layout stability.
///
/// Previous versions caused StackOverflowException when combined with AutoSize or certain layout patterns.
/// This class is now just a Panel with stub properties for backward compatibility.
///
/// New code should use Panel directly or Panel with SfSkinManager.SetVisualStyle() for theming.
/// </summary>
public class GradientPanelExt : System.Windows.Forms.Panel
{
    public GradientPanelExt()
    {
        // Standard Panel - no gradient, no custom colors.
        // Theme is managed by SfSkinManager (form-level theme cascade).
    }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? BackgroundColor { get; set; }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? GradientEnd { get; set; }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? GradientStart { get; set; }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GradientAngle { get; set; }

    /// <summary>Backward-compatible stub. Does nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseGradient { get; set; }

    /// <summary>Backward-compatible stub. Theme name property for SfSkinManager compatibility.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ThemeName { get; set; }
}

