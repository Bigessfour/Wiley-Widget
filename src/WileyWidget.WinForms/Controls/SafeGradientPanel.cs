using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// GradientPanelExt with recursion protection for SetCorrectPosition infinite loop bug.
/// 
/// CRITICAL BUG FIX:
/// Syncfusion's GradientPanelExt.SetCorrectPosition() can trigger infinite recursion:
///   OnSizeChanged → SetBoundsCore → SetCorrectPosition → set_Size → OnSizeChanged → ...
/// 
/// This wrapper adds a recursion guard to break the loop while preserving gradient functionality.
/// 
/// USE THIS WHEN:
/// - You need gradient panel styling from Syncfusion
/// - Panel contains dynamically sized child controls
/// - Layout involves nested panels or complex control hierarchies
/// 
/// ALTERNATIVE (RECOMMENDED):
/// Use standard System.Windows.Forms.Panel + SfSkinManager theme cascade (no gradients needed).
/// </summary>
public class SafeGradientPanel : GradientPanelExt
{
    private bool _isSettingBounds;

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        // Break recursion: if we're already in SetBoundsCore, skip nested calls
        if (_isSettingBounds)
        {
            return;
        }

        try
        {
            _isSettingBounds = true;
            base.SetBoundsCore(x, y, width, height, specified);
        }
        finally
        {
            _isSettingBounds = false;
        }
    }
}
