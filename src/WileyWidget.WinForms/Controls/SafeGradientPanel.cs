using System;
using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// DEPRECATED: SafeGradientPanel has been replaced with standard Panel for layout stability.
///
/// The Syncfusion GradientPanelExt had a critical recursive bug in SetCorrectPosition() that caused
/// infinite loops when controls were resized or repositioned. This class is now just a Panel with
/// stub properties for backward compatibility with code that referenced old GradientPanelExt members.
///
/// USE THIS WHEN:
/// - You need a stable panel control without gradient features
/// - Panel layout needs to be reliable across all scenarios
///
/// THEMING:
/// Use standard System.Windows.Forms.Panel + SfSkinManager theme cascade (no gradients needed).
/// </summary>
public class SafeGradientPanel : Panel
{
    // Stub properties for backward compatibility with old Syncfusion.Windows.Forms.Tools.GradientPanelExt
    // These do nothing - just prevent CS0117 errors when existing code references them.

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

