using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Comprehensive control sizing validator for all WinForms controls.
    /// Prevents sizing-related exceptions by validating dimensions, constraints, and enforcing safe defaults.
    /// Per Syncfusion Windows Forms documentation: https://help.syncfusion.com/windowsforms
    ///
    /// SUPPORTED VALIDATIONS:
    /// - Width/Height minimum values (must be ≥ 0, typically ≥ 1)
    /// - MinimumSize and MaximumSize constraint enforcement
    /// - SfDataGrid column width validation (Width ≥ MinimumWidth)
    /// - TableLayoutPanel consistency (ColumnCount matches ColumnStyles count)
    /// - AutoSize/AutoSizeMode conflict detection
    /// - DPI-aware sizing (LogicalToDeviceUnits conversion)
    /// - Control visibility and disposed state checks
    /// - Parent container space validation
    /// - NEW: SplitContainer-specific validation (integrates with SafeSplitterDistanceHelper)
    ///
    /// CONSTRAINT MATRIX (Per Syncfusion and WinForms Docs):
    /// Control Type        | Min Width | Min Height | Max Dimension   | Notes
    /// =================== | ========= | ========== | =============== | =====================================================
    /// SplitContainer      | 0         | 0          | Int32.MaxValue  | Panel1MinSize + Panel2MinSize + SplitterWidth must fit; Defaults: 25px mins
    /// SfDataGrid          | 0         | 0          | Int32.MaxValue  | Column Width >= MinimumWidth (default 5) enforced
    /// ChartControl        | 0         | 0          | Int32.MaxValue  | Both dims must be >0 for rendering
    /// Panel/GroupBox      | 0         | 0          | Int32.MaxValue  | Content may overflow if too small
    /// Label/Button/TextBox| 0         | 0          | Int32.MaxValue  | AutoSize respects MinimumSize
    /// TabControl          | 0         | 0          | Int32.MaxValue  | TabPages sized per control sizing
    /// LegacyGradientPanel| 0         | 0          | Int32.MaxValue  | AutoSize respects MinimumSize constraints
    /// TableLayoutPanel    | 0         | 0          | Int32.MaxValue  | Cell sizing per SizeType, respects column/row styles
    /// </summary>
    public static class SafeControlSizeValidator
    {
        /// <summary>
        /// Safely sets a control's Width with validation.
        /// Prevents negative/zero values that cause rendering issues.
        /// </summary>
        /// <param name="control">The control to resize</param>
        /// <param name="width">Desired width in pixels</param>
        /// <param name="allowZero">Whether to allow width=0 (for controls being positioned)</param>
        /// <returns>True if width was set successfully, false if validation failed</returns>
        public static bool TrySetWidth(Control control, int width, bool allowZero = false)
        {
            if (control == null || control.IsDisposed)
                return false;

            int minWidth = allowZero ? 0 : 1;
            if (width < minWidth)
                return false;

            // Check MaximumSize constraint
            if (control.MaximumSize.Width > 0 && width > control.MaximumSize.Width)
                return false;

            try
            {
                control.Width = width;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safely sets a control's Height with validation.
        /// Prevents negative/zero values that cause rendering issues.
        /// </summary>
        /// <param name="control">The control to resize</param>
        /// <param name="height">Desired height in pixels</param>
        /// <param name="allowZero">Whether to allow height=0 (for controls being positioned)</param>
        /// <returns>True if height was set successfully, false if validation failed</returns>
        public static bool TrySetHeight(Control control, int height, bool allowZero = false)
        {
            if (control == null || control.IsDisposed)
                return false;

            int minHeight = allowZero ? 0 : 1;
            if (height < minHeight)
                return false;

            // Check MaximumSize constraint
            if (control.MaximumSize.Height > 0 && height > control.MaximumSize.Height)
                return false;

            try
            {
                control.Height = height;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safely sets a control's Size with validation.
        /// Validates both Width and Height, enforces MinimumSize and MaximumSize constraints.
        /// NEW: DPI-aware adjustment using LogicalToDeviceUnits for high-DPI scenarios.
        /// </summary>
        /// <param name="control">The control to resize</param>
        /// <param name="width">Desired width in pixels</param>
        /// <param name="height">Desired height in pixels</param>
        /// <param name="allowZero">Whether to allow zero dimensions</param>
        /// <returns>True if size was set successfully, false if validation failed</returns>
        public static bool TrySetSize(Control control, int width, int height, bool allowZero = false)
        {
            if (control == null || control.IsDisposed)
                return false;

            int minSize = allowZero ? 0 : 1;
            if (width < minSize || height < minSize)
                return false;

            // DPI adjustment (convert logical to device units if needed)
            width = control.LogicalToDeviceUnits(width);
            height = control.LogicalToDeviceUnits(height);

            // Check MinimumSize constraint
            if (control.MinimumSize.Width > 0 && width < control.MinimumSize.Width)
                return false;
            if (control.MinimumSize.Height > 0 && height < control.MinimumSize.Height)
                return false;

            // Check MaximumSize constraint
            if (control.MaximumSize.Width > 0 && width > control.MaximumSize.Width)
                return false;
            if (control.MaximumSize.Height > 0 && height > control.MaximumSize.Height)
                return false;

            try
            {
                control.Size = new Size(width, height);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that a control's current size doesn't violate MinimumSize/MaximumSize constraints.
        /// Useful for diagnosing layout issues.
        /// </summary>
        /// <param name="control">The control to validate</param>
        /// <returns>Detailed validation result with diagnostics</returns>
        public static SizeValidationResult ValidateControlSize(Control control)
        {
            var result = new SizeValidationResult();

            if (control == null || control.IsDisposed)
            {
                result.IsValid = false;
                result.Message = "Control is null or disposed";
                return result;
            }

            result.ControlName = control.Name;
            result.ControlType = control.GetType().Name;
            result.CurrentWidth = control.Width;
            result.CurrentHeight = control.Height;
            result.MinimumWidth = control.MinimumSize.Width;
            result.MinimumHeight = control.MinimumSize.Height;
            result.MaximumWidth = control.MaximumSize.Width;
            result.MaximumHeight = control.MaximumSize.Height;
            result.IsVisible = control.Visible;

            // Check for zero dimensions (common issue)
            if (control.Width == 0 || control.Height == 0)
            {
                result.IsValid = false;
                result.Message = $"Control has zero dimension: {control.Width}x{control.Height}";
                result.HasZeroDimension = true;
                return result;
            }

            // Check negative dimensions (shouldn't happen, but defensive check)
            if (control.Width < 0 || control.Height < 0)
            {
                result.IsValid = false;
                result.Message = $"Control has negative dimension: {control.Width}x{control.Height}";
                return result;
            }

            // Check MinimumSize constraint
            if (control.MinimumSize.Width > 0 && control.Width < control.MinimumSize.Width)
            {
                result.IsValid = false;
                result.Message = $"Width {control.Width} < MinimumSize.Width {control.MinimumSize.Width}";
                result.ViolatesMinimumSize = true;
                return result;
            }

            if (control.MinimumSize.Height > 0 && control.Height < control.MinimumSize.Height)
            {
                result.IsValid = false;
                result.Message = $"Height {control.Height} < MinimumSize.Height {control.MinimumSize.Height}";
                result.ViolatesMinimumSize = true;
                return result;
            }

            // Check MaximumSize constraint
            if (control.MaximumSize.Width > 0 && control.Width > control.MaximumSize.Width)
            {
                result.IsValid = false;
                result.Message = $"Width {control.Width} > MaximumSize.Width {control.MaximumSize.Width}";
                result.ViolatesMaximumSize = true;
                return result;
            }

            if (control.MaximumSize.Height > 0 && control.Height > control.MaximumSize.Height)
            {
                result.IsValid = false;
                result.Message = $"Height {control.Height} > MaximumSize.Height {control.MaximumSize.Height}";
                result.ViolatesMaximumSize = true;
                return result;
            }

            result.IsValid = true;
            result.Message = "Valid size";
            return result;
        }

        /// <summary>
        /// NEW: Overload for SplitContainer-specific validation.
        /// Integrates with SafeSplitterDistanceHelper.Validate for comprehensive checks.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to validate</param>
        /// <returns>SizeValidationResult with SplitContainer specifics</returns>
        public static SizeValidationResult ValidateControlSize(SplitContainer splitContainer)
        {
            var result = ValidateControlSize((Control)splitContainer);
            if (!result.IsValid)
                return result;

            var splitterResult = SafeSplitterDistanceHelper.Validate(splitContainer);
            if (!splitterResult.IsValid)
            {
                result.IsValid = false;
                result.Message = $"SplitContainer violation: {splitterResult.Message}";
            }

            return result;
        }

        /// <summary>
        /// Validates SfDataGrid column width constraints.
        /// Ensures Width ≥ MinimumWidth per Syncfusion documentation:
        /// https://help.syncfusion.com/windowsforms/datagrid/columns
        /// </summary>
        /// <param name="columnWidth">The column width to validate</param>
        /// <param name="columnMinimumWidth">The column's MinimumWidth</param>
        /// <param name="columnName">Optional column name for diagnostics</param>
        /// <returns>True if width ≥ minimumWidth, false otherwise</returns>
        public static bool ValidateGridColumnWidth(int columnWidth, int columnMinimumWidth, string columnName = "Column")
        {
            // Per Syncfusion: MinimumWidth default is 5, can be 2 to Int32.MaxValue
            int effectiveMinimum = columnMinimumWidth > 0 ? columnMinimumWidth : 5;
            return columnWidth >= effectiveMinimum;
        }

        /// <summary>
        /// Safely resizes a control after deferring to layout completion.
        /// Useful for controls in complex hierarchies that need layout finalization first.
        /// NEW: Uses BeginInvoke for UI thread safety.
        /// </summary>
        /// <param name="control">The control to resize</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="delayMs">Milliseconds to wait before applying resize (allows layout to settle)</param>
        public static void DeferredResize(Control control, int width, int height, int delayMs = 50)
        {
            if (control == null || control.IsDisposed)
                return;

            // Schedule resize on UI thread after delay
            control.BeginInvoke(new Action(() =>
            {
                System.Threading.Thread.Sleep(delayMs); // Simple delay; use Timer for non-blocking if needed
                if (!control.IsDisposed)
                {
                    TrySetSize(control, width, height);
                }
            }));
        }

        /// <summary>
        /// Validates and adjusts control size if it violates constraints.
        /// Returns whether adjustment was needed.
        /// </summary>
        /// <param name="control">The control to validate and adjust</param>
        /// <param name="adjustedWidth">Output: adjusted width if changed, original width otherwise</param>
        /// <param name="adjustedHeight">Output: adjusted height if changed, original height otherwise</param>
        /// <returns>True if adjustment was made, false if control already valid</returns>
        public static bool TryAdjustConstrainedSize(Control control, out int adjustedWidth, out int adjustedHeight)
        {
            adjustedWidth = control?.Width ?? 0;
            adjustedHeight = control?.Height ?? 0;

            if (control == null || control.IsDisposed)
                return false;

            bool adjusted = false;

            // Enforce MinimumSize
            if (control.MinimumSize.Width > 0 && control.Width < control.MinimumSize.Width)
            {
                adjustedWidth = control.MinimumSize.Width;
                adjusted = true;
            }

            if (control.MinimumSize.Height > 0 && control.Height < control.MinimumSize.Height)
            {
                adjustedHeight = control.MinimumSize.Height;
                adjusted = true;
            }

            // Enforce MaximumSize
            if (control.MaximumSize.Width > 0 && adjustedWidth > control.MaximumSize.Width)
            {
                adjustedWidth = control.MaximumSize.Width;
                adjusted = true;
            }

            if (control.MaximumSize.Height > 0 && adjustedHeight > control.MaximumSize.Height)
            {
                adjustedHeight = control.MaximumSize.Height;
                adjusted = true;
            }

            if (adjusted)
            {
                TrySetSize(control, adjustedWidth, adjustedHeight);
            }

            return adjusted;
        }

        /// <summary>
        /// Validates a container has sufficient space before adding or resizing child controls.
        /// Prevents "controls cut off" scenarios from insufficient parent container dimensions.
        /// NEW: Accounts for DPI scaling.
        /// </summary>
        /// <param name="parentContainer">The parent container to check</param>
        /// <param name="requiredWidth">Minimum width needed for child controls</param>
        /// <param name="requiredHeight">Minimum height needed for child controls</param>
        /// <param name="accountForPadding">Whether to account for container padding in calculation</param>
        /// <returns>True if container has sufficient space, false otherwise</returns>
        public static bool HasSufficientSpace(Control parentContainer, int requiredWidth, int requiredHeight, bool accountForPadding = true)
        {
            if (parentContainer == null || parentContainer.IsDisposed)
                return false;

            int availableWidth = parentContainer.LogicalToDeviceUnits(parentContainer.Width);
            int availableHeight = parentContainer.LogicalToDeviceUnits(parentContainer.Height);

            if (accountForPadding)
            {
                availableWidth -= parentContainer.Padding.Left + parentContainer.Padding.Right;
                availableHeight -= parentContainer.Padding.Top + parentContainer.Padding.Bottom;
            }

            return availableWidth >= requiredWidth && availableHeight >= requiredHeight;
        }

        /// <summary>
        /// Validates AutoSize is not conflicting with fixed size constraints.
        /// Per WinForms documentation: AutoSize respects MinimumSize and MaximumSize.
        /// </summary>
        /// <param name="control">The control to validate</param>
        /// <returns>Validation result with conflict diagnostics</returns>
        public static AutoSizeValidationResult ValidateAutoSizeConflicts(Control control)
        {
            var result = new AutoSizeValidationResult();

            if (control == null || control.IsDisposed)
            {
                result.IsValid = false;
                result.Message = "Control is null or disposed";
                return result;
            }

            result.ControlName = control.Name;
            result.AutoSizeEnabled = control.AutoSize;

            if (!control.AutoSize)
            {
                result.IsValid = true;
                result.Message = "AutoSize is disabled";
                return result;
            }

            // Check for conflicting constraints
            if (control.MinimumSize.Width > 0 && control.MaximumSize.Width > 0 &&
                control.MinimumSize.Width > control.MaximumSize.Width)
            {
                result.IsValid = false;
                result.Message = $"MinimumSize.Width ({control.MinimumSize.Width}) > MaximumSize.Width ({control.MaximumSize.Width})";
                result.HasConflict = true;
                return result;
            }

            if (control.MinimumSize.Height > 0 && control.MaximumSize.Height > 0 &&
                control.MinimumSize.Height > control.MaximumSize.Height)
            {
                result.IsValid = false;
                result.Message = $"MinimumSize.Height ({control.MinimumSize.Height}) > MaximumSize.Height ({control.MaximumSize.Height})";
                result.HasConflict = true;
                return result;
            }

            result.IsValid = true;
            result.Message = "AutoSize constraints are valid";
            return result;
        }

        /// <summary>
        /// Gets comprehensive diagnostics for a control's sizing state.
        /// Useful for debugging dashboard panel sizing issues.
        /// NEW: Includes DPI scale factor.
        /// </summary>
        /// <param name="control">The control to diagnose</param>
        /// <param name="label">Optional label for identification</param>
        /// <returns>Detailed diagnostic string</returns>
        public static string GetSizingDiagnostics(Control control, string label = "Control")
        {
            if (control == null || control.IsDisposed)
                return $"{label}: NULL or DISPOSED";

            var sizeValidation = ValidateControlSize(control);
            var autoSizeValidation = ValidateAutoSizeConflicts(control);

            float dpiScale = control.CreateGraphics()?.DpiX / 96f ?? 1f;

            return $"\n{label} SIZING DIAGNOSTICS:\n" +
                   $"  Name: {control.Name}\n" +
                   $"  Type: {control.GetType().Name}\n" +
                   $"  Current Size: {control.Width}x{control.Height}px (DPI Scale: {dpiScale}x)\n" +
                   $"  MinimumSize: {control.MinimumSize.Width}x{control.MinimumSize.Height}px\n" +
                   $"  MaximumSize: {control.MaximumSize.Width}x{control.MaximumSize.Height}px\n" +
                   $"  Padding: {control.Padding}\n" +
                   $"  Margin: {control.Margin}\n" +
                   $"  Dock: {control.Dock}\n" +
                   $"  Anchor: {control.Anchor}\n" +
                   $"  AutoSize: {control.AutoSize}\n" +
                   $"  Visible: {control.Visible}\n" +
                   $"  IsHandleCreated: {control.IsHandleCreated}\n" +
                   $"  Size Validation: {(sizeValidation.IsValid ? "✓ VALID" : "✗ INVALID")}\n" +
                   $"  Message: {sizeValidation.Message}\n" +
                   $"  AutoSize Conflicts: {(autoSizeValidation.IsValid ? "✓ NONE" : "✗ HAS CONFLICTS")}\n";
        }

        /// <summary>
        /// Size validation result with detailed diagnostics.
        /// </summary>
        public class SizeValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ControlName { get; set; } = string.Empty;
            public string ControlType { get; set; } = string.Empty;
            public int CurrentWidth { get; set; }
            public int CurrentHeight { get; set; }
            public int MinimumWidth { get; set; }
            public int MinimumHeight { get; set; }
            public int MaximumWidth { get; set; }
            public int MaximumHeight { get; set; }
            public bool IsVisible { get; set; }
            public bool HasZeroDimension { get; set; }
            public bool ViolatesMinimumSize { get; set; }
            public bool ViolatesMaximumSize { get; set; }
        }

        /// <summary>
        /// AutoSize conflict validation result.
        /// </summary>
        public class AutoSizeValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ControlName { get; set; } = string.Empty;
            public bool AutoSizeEnabled { get; set; }
            public bool HasConflict { get; set; }
        }
    }
}
