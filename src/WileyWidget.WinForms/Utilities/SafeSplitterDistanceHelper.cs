using System;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Comprehensive helper utility for safely configuring SplitContainer sizing and layout.
    /// Prevents InvalidOperationException during control initialization by deferring sizing operations
    /// until the control has valid dimensions. Follows Syncfusion Windows Forms API constraints:
    /// https://help.syncfusion.com/windowsforms/splitcontainer/splitcontaineradv
    ///
    /// CONSTRAINT ENFORCEMENT (Per Syncfusion Docs):
    /// Panel1MinSize ≤ SplitterDistance ≤ (Dimension - Panel2MinSize - SplitterWidth)
    /// Where Dimension = Width (vertical splitter) or Height (horizontal splitter)
    /// Defaults: Panel1MinSize/Panel2MinSize = 25, SplitterWidth varies (e.g., 20 in examples)
    ///
    /// Usage Patterns:
    ///
    /// 1. SAFE DEFERRED SIZING (Recommended for initialization):
    ///    SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(
    ///        splitContainer, panel1Min: 200, panel2Min: 150, desiredDistance: 400);
    ///
    /// 2. ADVANCED CONFIGURATION (With custom splitter width):
    ///    SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
    ///        splitContainer, panel1MinSize: 200, panel2MinSize: 150,
    ///        desiredDistance: 400, splitterWidth: 6);
    ///
    /// 3. VALIDATION & DIAGNOSTICS:
    ///    var result = SafeSplitterDistanceHelper.Validate(splitContainer);
    ///    if (!result.IsValid) Debug.WriteLine(result.ToString());
    ///
    /// 4. PROPORTIONAL RESIZING (During window resize):
    ///    SafeSplitterDistanceHelper.SetupProportionalResizing(splitContainer, 0.5); // 50/50 split
    ///
    /// 5. DIRECT ASSIGNMENT (When you know control is sized):
    ///    if (SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, distance))
    ///    {
    ///        // Success
    ///    }
    /// </summary>
    public static class SafeSplitterDistanceHelper
    {
        /// <summary>
        /// Configures a SplitContainer with safe min sizes and splitter distance, deferring all assignments
        /// until the control is properly sized. Use this when creating SplitContainers with Panel1MinSize
        /// and Panel2MinSize to avoid InvalidOperationException during initialization.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="panel1MinSize">Desired Panel1MinSize (will be deferred until control is sized)</param>
        /// <param name="panel2MinSize">Desired Panel2MinSize (will be deferred until control is sized)</param>
        /// <param name="desiredDistance">Desired SplitterDistance (will be deferred until control is sized)</param>
        public static void ConfigureSafeSplitContainer(dynamic splitContainer, int panel1MinSize, int panel2MinSize, int desiredDistance)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            Control control = splitContainer;
            dynamic sc = splitContainer;

            bool initialized = false;
            int minDimension = sc.Orientation == Orientation.Horizontal
                ? control.Height
                : control.Width;

            // [CRITICAL FIX] Clamp min-sizes to safe fallback if they're too large for initial container
            // This prevents ArgumentOutOfRangeException when container starts small
            int currentDimension = sc.Orientation == Orientation.Horizontal
                ? control.Height
                : control.Width;

            int splitterWidth = 0;
            try { splitterWidth = sc.SplitterWidth; } catch { splitterWidth = 5; }

            // [CRITICAL FIX] Defer ALL property sets until control has valid handle + dimensions
            // Setting Panel1MinSize/Panel2MinSize before handle creation causes cascade of InvalidOperationException
            // Reference: https://github.com/dotnet/winforms/issues/4000 (similar timing issues)

            System.Diagnostics.Debug.WriteLine(
                $"[SafeSplitterDistanceHelper] Deferring configuration: Panel1={panel1MinSize}, Panel2={panel2MinSize}, " +
                $"Distance={desiredDistance}, CurrentDim={currentDimension}px, Orientation={sc.Orientation}");

            // Use HandleCreated + SizeChanged to defer initialization until control is ready
            EventHandler? initHandler = null;
            initHandler = (s, e) =>
            {
                if (initialized || control.IsDisposed)
                    return;

                // Wait for valid dimensions (>0)
                minDimension = sc.Orientation == Orientation.Horizontal
                    ? control.Height
                    : control.Width;

                if (minDimension <= 0)
                    return; // Still not sized, wait for next event

                // Calculate safe min sizes based on actual dimensions
                int requiredDimension = panel1MinSize + panel2MinSize + splitterWidth;

                if (minDimension < requiredDimension)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SafeSplitterDistanceHelper] Container too small: {minDimension}px < required {requiredDimension}px - waiting");
                    return;
                }

                initialized = true;

                // Unsubscribe (one-shot pattern)
                control.HandleCreated -= initHandler;
                control.SizeChanged -= initHandler;

                try
                {
                    // Clamp to safe bounds
                    int finalPanel1Min = Math.Min(panel1MinSize, Math.Max(25, minDimension - panel2MinSize - splitterWidth - 10));
                    int finalPanel2Min = Math.Min(panel2MinSize, Math.Max(25, minDimension - panel1MinSize - splitterWidth - 10));

                    // Set min sizes
                    sc.Panel1MinSize = finalPanel1Min;
                    sc.Panel2MinSize = finalPanel2Min;

                    // Calculate safe distance bounds
                    int minDistance = finalPanel1Min;
                    int maxDistance = minDimension - finalPanel2Min - splitterWidth;
                    int safeDistance = Math.Clamp(desiredDistance, minDistance, maxDistance);

                    if (safeDistance != desiredDistance)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SafeSplitterDistanceHelper] Clamped distance: {desiredDistance} → {safeDistance} (bounds: {minDistance}-{maxDistance})");
                    }

                    // Set splitter distance
                    TrySetSplitterDistance(sc, safeDistance);

                    System.Diagnostics.Debug.WriteLine(
                        $"[SafeSplitterDistanceHelper] SplitContainer configured: Orientation={sc.Orientation}, " +
                        $"Panel1Min={finalPanel1Min}, Panel2Min={finalPanel2Min}, Distance={sc.SplitterDistance}, Dimension={minDimension}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SafeSplitterDistanceHelper] Configuration failed: {ex.Message}");
                    // Don't rethrow - layout may still be adjusting
                }
            };

            // Subscribe to both events for maximum compatibility
            if (!control.IsHandleCreated)
            {
                control.HandleCreated += initHandler;
            }
            else
            {
                // Handle already exists - trigger immediately via SizeChanged path
                control.SizeChanged += initHandler;
            }
        }

        /// <summary>
        /// Advanced configuration with optional splitter width override.
        /// Defers settings until control is ready, clamps mins proportionally if overflowing.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="panel1MinSize">Desired Panel1MinSize</param>
        /// <param name="panel2MinSize">Desired Panel2MinSize</param>
        /// <param name="desiredDistance">Desired SplitterDistance</param>
        /// <param name="targetSplitterWidth">Optional SplitterWidth override (default: current)</param>
        public static void ConfigureSafeSplitContainerAdvanced(dynamic splitContainer, int panel1MinSize, int panel2MinSize, int desiredDistance, int targetSplitterWidth = -1)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            bool initialized = false;

            EventHandler? initHandler = null;
            initHandler = (s, e) =>
            {
                if (initialized || splitContainer.IsDisposed)
                    return;

                int currentDimension = splitContainer.Orientation == Orientation.Vertical
                    ? splitContainer.Width
                    : splitContainer.Height;

                if (currentDimension <= 0)
                    return;

                int splWidth = targetSplitterWidth > 0 ? targetSplitterWidth : splitContainer.SplitterWidth;

                // Calculate available space for both panels
                int availableForPanels = Math.Max(0, currentDimension - splWidth);

                // FIXED LOGIC: Ensure sum of min sizes never exceeds available space
                int requestedTotal = panel1MinSize + panel2MinSize;
                int safePanel1 = panel1MinSize;
                int safePanel2 = panel2MinSize;

                if (requestedTotal > availableForPanels)
                {
                    // Scale down proportionally while maintaining minimum viable sizes
                    const int absoluteMin = 25; // Syncfusion default

                    if (availableForPanels < (absoluteMin * 2))
                    {
                        // Not enough space for both panels - skip initialization
                        return;
                    }

                    // Calculate proportional sizes and CLAMP to available space
                    double scale = (double)availableForPanels / requestedTotal;
                    safePanel1 = Math.Max(absoluteMin, (int)(panel1MinSize * scale));
                    safePanel2 = Math.Max(absoluteMin, availableForPanels - safePanel1);

                    System.Diagnostics.Debug.WriteLine(
                        $"[SafeSplitterDistanceHelper] Scaled min sizes: Panel1 {panel1MinSize}→{safePanel1}, " +
                        $"Panel2 {panel2MinSize}→{safePanel2} (Available={availableForPanels}px)");
                }

                // Calculate minimum required dimension
                int minRequiredDimension = safePanel1 + safePanel2 + splWidth;

                // Only initialize if we have enough space
                if (currentDimension >= minRequiredDimension)
                {
                    initialized = true;

                    // Unsubscribe (one-shot pattern)
                    splitContainer.HandleCreated -= initHandler;
                    splitContainer.SizeChanged -= initHandler;

                    try
                    {
                        // Set SplitterWidth if provided
                        if (targetSplitterWidth > 0)
                            splitContainer.SplitterWidth = targetSplitterWidth;

                        // Set min sizes safely - order matters! Panel1 first
                        splitContainer.Panel1MinSize = safePanel1;
                        splitContainer.Panel2MinSize = safePanel2;

                        // Calculate safe distance bounds per Syncfusion formula
                        int minDistance = safePanel1;
                        int maxDistance = currentDimension - safePanel2 - splWidth;
                        int safeDistance = Math.Clamp(desiredDistance, minDistance, maxDistance);

                        // Set splitter distance
                        TrySetSplitterDistance(splitContainer, safeDistance);

                        System.Diagnostics.Debug.WriteLine(
                            $"[SafeSplitterDistanceHelper] SplitContainer configured (Advanced): " +
                            $"Orientation={splitContainer.Orientation}, " +
                            $"Panel1Min={splitContainer.Panel1MinSize}, " +
                            $"Panel2Min={splitContainer.Panel2MinSize}, " +
                            $"Distance={splitContainer.SplitterDistance}, " +
                            $"Dimension={currentDimension}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SafeSplitterDistanceHelper] Configuration failed: {ex.Message}");
                    }
                }
            };

            // Subscribe to both events for maximum compatibility
            if (!splitContainer.IsHandleCreated)
            {
                splitContainer.HandleCreated += initHandler;
            }
            splitContainer.SizeChanged += initHandler;
        }

        /// <summary>
        /// Defers SplitterDistance assignment until the next UI message loop iteration,
        /// allowing the parent container to complete layout before setting the value.
        /// This prevents InvalidOperationException during initialization.
        ///
        /// CRITICAL: Checks if the control's window handle is created before invoking.
        /// If the handle doesn't exist yet, subscribes to HandleCreated event and defers
        /// the operation until the handle is created. This prevents InvalidOperationException
        /// when BeginInvoke is called on controls without window handles.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="desiredDistance">The desired splitter position</param>
        public static void SetSplitterDistanceDeferred(dynamic splitContainer, int desiredDistance)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            // CRITICAL FIX: Wait for handle to exist before invoking
            // BeginInvoke requires a valid window handle; calling it before the handle
            // is created throws InvalidOperationException. This commonly happens when
            // controls are created in InitializeControls() during parent's OnHandleCreated.
            if (!splitContainer.IsHandleCreated)
            {
                // Defer until handle is created (one-shot handler)
                EventHandler? handleCreatedHandler = null;
                handleCreatedHandler = (s, e) =>
                {
                    // Unsubscribe immediately (one-shot pattern)
                    splitContainer.HandleCreated -= handleCreatedHandler;

                    // Verify control wasn't disposed during handle creation
                    if (splitContainer.IsDisposed)
                        return;

                    try
                    {
                        TrySetSplitterDistance(splitContainer, desiredDistance);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SafeSplitterDistanceHelper] Deferred set failed: {ex.Message}");
                    }
                };

                splitContainer.HandleCreated += handleCreatedHandler;
                return;
            }

            // Handle already exists - safe to invoke immediately
            splitContainer.BeginInvoke(new Action(() =>
            {
                try
                {
                    TrySetSplitterDistance(splitContainer, desiredDistance);
                }
                catch
                {
                    // Silently ignore failures - layout may still be adjusting
                }
            }));
        }

        /// <summary>
        /// Attempts to set SplitterDistance with automatic bounds checking.
        /// Returns true if successful, false if the desired distance is out of bounds.
        ///
        /// CRITICAL VALIDATIONS:
        /// 1. Control must not be null or disposed
        /// 2. Control must have a created window handle (IsHandleCreated)
        /// 3. Dimension must be sufficient for min size constraints
        /// 4. Proposed distance must be within valid bounds
        /// 5. Min sizes must not exceed available dimension
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="desiredDistance">The desired splitter position</param>
        /// <returns>True if successful, false if out of bounds or unable to set</returns>
        public static bool TrySetSplitterDistance(dynamic splitContainer, int desiredDistance)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return false;

            // CRITICAL: Control must have a window handle to set properties safely
            // Attempting to set SplitterDistance on a control without a handle can cause
            // InvalidOperationException or underlying constraint violations
            if (!splitContainer.IsHandleCreated)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SafeSplitterDistanceHelper] Skipping early set - no handle created yet. Desired distance: {desiredDistance}px");
                return false;
            }

            // CRITICAL: Dimension must be non-zero
            // Setting SplitterDistance on a zero-sized control causes InvalidOperationException
            Control control = splitContainer;
            int currentDimension = (splitContainer.Orientation == Orientation.Horizontal)
                ? control.Height
                : control.Width;

            if (currentDimension <= 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SafeSplitterDistanceHelper] Skipping early set - zero width/height. " +
                    $"Dimension={currentDimension}px, Desired distance={desiredDistance}px, Orientation={splitContainer.Orientation}");
                return false;
            }

            // Get valid bounds based on current dimension and min sizes
            if (!TryGetBounds(splitContainer, out int minDistance, out int maxDistance))
            {
                return false; // Not enough room to satisfy min-size constraints
            }

            // Clamp desired distance to valid bounds
            var safeDist = Math.Clamp(desiredDistance, minDistance, maxDistance);

            // Pre-validate: ensure the clamped distance is still within bounds
            // This catches race conditions where the container dimension changed
            if (safeDist < minDistance || safeDist > maxDistance)
            {
                return false;
            }

            // Only set if value actually changed (avoid unnecessary setter calls)
            int currentDistance = splitContainer.SplitterDistance;
            if (currentDistance != safeDist)
            {
                try
                {
                    splitContainer.SplitterDistance = safeDist;
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Syncfusion SplitContainerAdv threw due to constraint violation
                    return false;
                }
                catch (ArgumentException)
                {
                    // Out of bounds
                    return false;
                }
                catch (InvalidOperationException)
                {
                    // Underlying control rejected the value (likely due to layout race)
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates a safe splitter distance based on a percentage of the available space.
        /// For vertical splitters: calculates based on Width
        /// For horizontal splitters: calculates based on Height
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to measure</param>
        /// <param name="percentage">The desired percentage for Panel1 (0.0 to 1.0). Default: 0.5 (50%)</param>
        /// <returns>A safe SplitterDistance value within valid bounds</returns>
        public static int CalculateSafeDistance(dynamic splitContainer, double percentage = 0.5)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return 100;

            if (!TryGetBounds(splitContainer, out int minDistance, out int maxDistance))
            {
                return minDistance; // Return the minimum to keep callers within the tightest bound
            }

            // Clamp percentage to valid range
            percentage = Math.Clamp(percentage, 0.0, 1.0);

            int dimension = splitContainer.Orientation == Orientation.Vertical
                ? splitContainer.Width
                : splitContainer.Height;

            if (dimension <= 0)
                return Math.Max(25, splitContainer.Panel1MinSize);  // Syncfusion default min

            int proposed = (int)(dimension * percentage);
            return Math.Clamp(proposed, minDistance, maxDistance);
        }

        /// <summary>
        /// Sets up proportional resizing for the SplitContainer on parent form resize.
        /// Maintains the given ratio as the container size changes.
        /// NEW: Finishing touch for dynamic layouts.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to setup</param>
        /// <param name="ratio">The desired Panel1 ratio (0.0 to 1.0)</param>
        public static void SetupProportionalResizing(dynamic splitContainer, double ratio)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            Control control = splitContainer;
            control.SizeChanged += (s, e) =>
            {
                int safeDistance = CalculateSafeDistance(splitContainer, ratio);
                TrySetSplitterDistance(splitContainer, safeDistance);
            };
        }

        /// <summary>
        /// Internal: Gets current bounds for splitter distance.
        /// </summary>
        private static bool TryGetBounds(dynamic splitContainer, out int min, out int max)
        {
            min = splitContainer.Panel1MinSize;
            max = (splitContainer.Orientation == Orientation.Vertical ? splitContainer.Width : splitContainer.Height)
                  - splitContainer.SplitterWidth - splitContainer.Panel2MinSize;
            return min <= max && max > 0;
        }

        /// <summary>
        /// Validates that a SplitContainer's current configuration is within valid bounds.
        /// Returns diagnostics info for troubleshooting sizing issues.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to validate</param>
        /// <returns>Validation result with diagnostic info</returns>
        public static SplitterValidationResult Validate(dynamic splitContainer)
        {
            var result = new SplitterValidationResult();

            if (splitContainer == null || splitContainer.IsDisposed)
            {
                result.IsValid = false;
                result.Message = "SplitContainer is null or disposed";
                return result;
            }

            int dimension = splitContainer.Orientation == Orientation.Vertical
                ? splitContainer.Width
                : splitContainer.Height;

            int minRequired = splitContainer.Panel1MinSize + splitContainer.Panel2MinSize + splitContainer.SplitterWidth;
            int minDistance = splitContainer.Panel1MinSize;
            int maxDistance = dimension - splitContainer.Panel2MinSize - splitContainer.SplitterWidth;

            result.Orientation = splitContainer.Orientation.ToString();
            result.CurrentDimension = dimension;
            result.Panel1MinSize = splitContainer.Panel1MinSize;
            result.Panel2MinSize = splitContainer.Panel2MinSize;
            result.SplitterWidth = splitContainer.SplitterWidth;
            result.CurrentDistance = splitContainer.SplitterDistance;
            result.ValidDistanceMin = minDistance;
            result.ValidDistanceMax = maxDistance;
            result.MinRequiredDimension = minRequired;

            if (dimension < minRequired)
            {
                result.IsValid = false;
                result.Message = $"Insufficient space: Dimension={dimension} < MinRequired={minRequired}. " +
                                 $"Need at least {minRequired}px for Panel1({splitContainer.Panel1MinSize}px) + " +
                                 $"Panel2({splitContainer.Panel2MinSize}px) + Splitter({splitContainer.SplitterWidth}px)";
            }
            else if (splitContainer.SplitterDistance < minDistance || splitContainer.SplitterDistance > maxDistance)
            {
                result.IsValid = false;
                result.Message = $"SplitterDistance {splitContainer.SplitterDistance} out of bounds [{minDistance}, {maxDistance}]";
            }
            else
            {
                result.IsValid = true;
                result.Message = "Valid configuration";
            }

            return result;
        }

        /// <summary>
        /// Gets a detailed diagnostic report for a SplitContainer's sizing state.
        /// Useful for debugging layout issues in complex panel hierarchies.
        /// NEW: Integrates with SafeControlSizeValidator diagnostics.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to diagnose</param>
        /// <param name="label">Optional label for identification in output</param>
        /// <returns>Detailed diagnostic string</returns>
        public static string GetDiagnostics(dynamic splitContainer, string label = "SplitContainer")
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return $"{label}: NULL or DISPOSED";

            var validation = Validate(splitContainer);
            var controlDiag = SafeControlSizeValidator.GetSizingDiagnostics(splitContainer, label);

            return controlDiag + "\n" +
                   $"  Status: {(validation.IsValid ? "✓ VALID" : "✗ INVALID")}\n" +
                   $"  {validation}\n" +
                   $"  IsHandleCreated: {splitContainer.IsHandleCreated}\n" +
                   $"  FixedPanel: {splitContainer.FixedPanel}\n" +
                   $"  BorderStyle: {splitContainer.BorderStyle}";
        }

        /// <summary>
        /// Gets the current valid splitter distance range for a SplitContainer.
        /// Useful for understanding constraints before attempting to set a distance.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to query</param>
        /// <param name="minValid">Output: minimum valid distance</param>
        /// <param name="maxValid">Output: maximum valid distance</param>
        /// <returns>True if range is valid (has non-zero width), false if not enough space</returns>
        public static bool TryGetValidDistanceRange(dynamic splitContainer, out int minValid, out int maxValid)
        {
            return TryGetBounds(splitContainer, out minValid, out maxValid);
        }

        /// <summary>
        /// Clamps the SplitContainer minimum sizes so they fit inside the current client dimension plus a safety buffer.
        /// Panel2MinSize is reduced first and Panel1MinSize only if the overflow remains.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer or SplitContainerAdv that should be adjusted.</param>
        /// <param name="fallbackMin">Minimum pixel size to enforce for each panel (Syncfusion default: 25).</param>
        /// <param name="safetyBuffer">Pixels reserved for the splitter and breathing room.</param>
        /// <returns>True if any panel minimum was lowered.</returns>
        public static bool ClampMinSizesToAvailableSpace(dynamic splitContainer, int fallbackMin = 25, int safetyBuffer = 40)
        {
            if (splitContainer == null || splitContainer.IsDisposed || fallbackMin <= 0)
                return false;

            try
            {
                int dimension = splitContainer.Orientation == Orientation.Vertical
                    ? splitContainer.Width
                    : splitContainer.Height;
                int splitterWidth = Math.Max(splitContainer.SplitterWidth, 1);
                int available = Math.Max(0, dimension - splitterWidth - safetyBuffer);

                int panel1Min = splitContainer.Panel1MinSize;
                int panel2Min = splitContainer.Panel2MinSize;
                int maxCombined = Math.Max(available, fallbackMin * 2);
                int currentSum = panel1Min + panel2Min;

                if (currentSum <= maxCombined)
                    return false;

                int excess = currentSum - maxCombined;
                int originalPanel1 = panel1Min;
                int originalPanel2 = panel2Min;

                if (panel2Min > fallbackMin)
                {
                    int reduce = Math.Min(panel2Min - fallbackMin, excess);
                    panel2Min -= reduce;
                    excess -= reduce;
                }

                if (excess > 0 && panel1Min > fallbackMin)
                {
                    int reduce = Math.Min(panel1Min - fallbackMin, excess);
                    panel1Min -= reduce;
                    excess -= reduce;
                }

                if (excess > 0)
                {
                    panel2Min = Math.Max(fallbackMin, panel2Min - excess);
                    excess = 0;
                }

                panel1Min = Math.Max(fallbackMin, panel1Min);
                panel2Min = Math.Max(fallbackMin, panel2Min);

                bool changed = false;
                if (panel1Min != originalPanel1)
                {
                    splitContainer.Panel1MinSize = panel1Min;
                    changed = true;
                }

                if (panel2Min != originalPanel2)
                {
                    splitContainer.Panel2MinSize = panel2Min;
                    changed = true;
                }

                if (changed)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SafeSplitterDistanceHelper] Clamped min sizes: Panel1 {originalPanel1}px → {panel1Min}px, " +
                        $"Panel2 {originalPanel2}px → {panel2Min}px (Dimension={dimension}px, Available={available}px)");
                    return true;
                }
            }
            catch
            {
                // Keep the helper failure-safe
            }

            return false;
        }

        /// <summary>
        /// Configuration result and diagnostics for SplitContainer validation.
        /// </summary>
        public class SplitterValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Orientation { get; set; } = string.Empty;
            public int CurrentDimension { get; set; }
            public int Panel1MinSize { get; set; }
            public int Panel2MinSize { get; set; }
            public int SplitterWidth { get; set; }
            public int CurrentDistance { get; set; }
            public int ValidDistanceMin { get; set; }
            public int ValidDistanceMax { get; set; }
            public int MinRequiredDimension { get; set; }

            public override string ToString() =>
                $"[{(IsValid ? "✓" : "✗")}] {Message}\n" +
                $"  Orientation: {Orientation}\n" +
                $"  Dimension: {CurrentDimension}px (need {MinRequiredDimension}px)\n" +
                $"  Panel1Min: {Panel1MinSize}px, Panel2Min: {Panel2MinSize}px, SplitterWidth: {SplitterWidth}px\n" +
                $"  SplitterDistance: {CurrentDistance}px (valid range: [{ValidDistanceMin}, {ValidDistanceMax}])";
        }
    }
}

