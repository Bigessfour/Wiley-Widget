using System.Diagnostics;
using SyncfusionGradientPanelExt = Syncfusion.Windows.Forms.Tools.GradientPanelExt;

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
public class SafeGradientPanel : SyncfusionGradientPanelExt
{
    private const int MaxSafeRecursionDepth = 8;
    private int _recursionDepth;
    private bool _isSettingBounds;

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (_isSettingBounds)
        {
#if DEBUG
            Debug.WriteLine($"SafeGradientPanel recursion guard: skip nested SetBoundsCore (depth={_recursionDepth})");
#endif
            return;
        }

        if (_recursionDepth >= MaxSafeRecursionDepth)
        {
#if DEBUG
            Debug.Fail($"SafeGradientPanel recursion depth exceeded ({_recursionDepth})");
#endif
            return;
        }

        try
        {
            _isSettingBounds = true;
            _recursionDepth++;
            base.SetBoundsCore(x, y, width, height, specified);
        }
        finally
        {
            _recursionDepth--;
            _isSettingBounds = false;
        }
    }
}
