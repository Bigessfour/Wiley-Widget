using System;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Comprehensive helper utility for safely configuring SplitContainer sizing and layout.
    /// Prevents InvalidOperationException during control initialization by deferring sizing operations
    /// until the control has valid dimensions. Follows Syncfusion Windows Forms API constraints:
    /// https://help.syncfusion.com/cr/windowsforms
    ///
    /// CONSTRAINT ENFORCEMENT (Per Syncfusion Docs):
    /// Panel1MinSize ≤ SplitterDistance ≤ (Dimension - Panel2MinSize - SplitterWidth)
    /// Where Dimension = Width (vertical splitter) or Height (horizontal splitter)
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

            int maxSafeMinSize = Math.Max(25, (currentDimension - splitterWidth) / 2 - 10);
            int safePanel1Min = Math.Min(panel1MinSize, maxSafeMinSize);
            int safePanel2Min = Math.Min(panel2MinSize, maxSafeMinSize);

            if (safePanel1Min != panel1MinSize || safePanel2Min != panel2MinSize)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SafeSplitterDistanceHelper] Clamped min sizes: Panel1 {panel1MinSize}->{safePanel1Min}, " +
                    $"Panel2 {panel2MinSize}->{safePanel2Min} (container={currentDimension}px, orientation={sc.Orientation})");
            }

            // Apply safe values immediately to prevent validation exceptions
            try
            {
                sc.Panel1MinSize = safePanel1Min;
                sc.Panel2MinSize = safePanel2Min;
                System.Diagnostics.Debug.WriteLine(
                    $"[SafeSplitterDistanceHelper] Initial safe values set: Panel1MinSize={safePanel1Min}, Panel2MinSize={safePanel2Min}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeSplitterDistanceHelper] Failed to set initial safe values: {ex.Message}");
                return; // Bail out if container in invalid state
            }

            // Use SizeChanged to defer initialization until control has real dimensions
            control.SizeChanged += (s, e) =>
            {
                if (!initialized)
                {
                    // Calculate minimum dimension needed
                    int requiredDimension = safePanel1Min + safePanel2Min + splitterWidth;
                    minDimension = sc.Orientation == Orientation.Horizontal
                        ? control.Height
                        : control.Width;

                    // Only initialize if we have enough space
                    if (minDimension >= requiredDimension)
                    {
                        initialized = true;
                        try
                        {
                            // Use the original requested sizes now that we have space
                            int finalPanel1Min = Math.Min(panel1MinSize, Math.Max(25, minDimension - panel2MinSize - splitterWidth - 10));
                            int finalPanel2Min = Math.Min(panel2MinSize, Math.Max(25, minDimension - panel1MinSize - splitterWidth - 10));

                            sc.Panel1MinSize = finalPanel1Min;
                            sc.Panel2MinSize = finalPanel2Min;
                            TrySetSplitterDistance(sc, desiredDistance);

                            System.Diagnostics.Debug.WriteLine(
                                $"[SafeSplitterDistanceHelper] SplitContainer configured: Orientation={sc.Orientation}, " +
                                $"Panel1Min={finalPanel1Min}, Panel2Min={finalPanel2Min}, Distance={sc.SplitterDistance}, Dimension={minDimension}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SafeSplitterDistanceHelper] Configuration failed: {ex.Message}");
                            // Silently ignore - layout may still be adjusting
                        }
                    }
                }
            };
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

            Control control = splitContainer;
            dynamic sc = splitContainer;

            // CRITICAL FIX: Wait for handle to exist before invoking
            // BeginInvoke requires a valid window handle; calling it before the handle
            // is created throws InvalidOperationException. This commonly happens when
            // controls are created in InitializeControls() during parent's OnHandleCreated.
            if (!control.IsHandleCreated)
            {
                // Defer until handle is created (one-shot handler)
                EventHandler? handleCreatedHandler = null;
                handleCreatedHandler = (s, e) =>
                {
                    // Unsubscribe immediately (one-shot pattern)
                    control.HandleCreated -= handleCreatedHandler;

                    // Verify control wasn't disposed during handle creation
                    if (control.IsDisposed)
                        return;

                    try
                    {
                        TrySetSplitterDistance(sc, desiredDistance);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SafeSplitterDistanceHelper] Deferred set failed: {ex.Message}");
                    }
                };

                control.HandleCreated += handleCreatedHandler;
                return;
            }

            // Handle already exists - safe to invoke immediately
            control.BeginInvoke(new Action(() =>
            {
                try
                {
                    TrySetSplitterDistance(sc, desiredDistance);
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
            try
            {
                Control control = splitContainer;
                if (!control.IsHandleCreated)
                    return false;

                // Get valid bounds based on current dimension and min sizes
                if (!TryGetBounds(splitContainer, out int minDistance, out int maxDistance))
                {
                    return false; // Not enough room to satisfy min-size constraints
                }

                // Clamp desired distance to valid bounds
                var safeDist = EnforceBounds(desiredDistance, minDistance, maxDistance);

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
                    splitContainer.SplitterDistance = safeDist;
                }

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Syncfusion SplitContainerAdv threw due to constraint violation
                // This can happen when Panel1MinSize > available dimension
                // Return false to allow caller to handle gracefully
                return false;
            }
            catch (ArgumentException)
            {
                // Out of bounds - return false to allow caller to handle
                return false;
            }
            catch (InvalidOperationException)
            {
                // Underlying control rejected the value (likely due to layout race); caller can retry later
                return false;
            }
            catch (Exception ex)
            {
                // Any other exception during property reads or sets - fail gracefully
                Console.WriteLine($"Container: {splitContainer.Width}x{splitContainer.Height} (Min1: {splitContainer.Panel1MinSize}, Min2: {splitContainer.Panel2MinSize}), SplitWidth: {splitContainer.SplitterWidth}, Tried: {desiredDistance}, Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates a safe splitter distance based on a percentage of the available space.
        /// For horizontal splitters: calculates based on Height
        /// For vertical splitters: calculates based on Width
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
            percentage = Math.Max(0.0, Math.Min(1.0, percentage));

            int dimension = splitContainer.Orientation == Orientation.Horizontal
                ? splitContainer.Height
                : splitContainer.Width;

            if (dimension <= 0)
                return Math.Max(100, splitContainer.Panel1MinSize);

            int proposed = (int)(dimension * percentage);
            return EnforceBounds(proposed, minDistance, maxDistance);
        }

        /// <summary>
        /// Enforces splitter distance bounds: Panel1MinSize ≤ distance ≤ (Width/Height - Panel2MinSize)
        /// </summary>
        private static int EnforceBounds(int proposedDistance, int minDistance, int maxDistance)
        {
            if (proposedDistance < minDistance)
                return minDistance;

            if (proposedDistance > maxDistance)
                return maxDistance;

            return proposedDistance;
        }

        /// <summary>
        /// Computes the valid splitter distance bounds, respecting Panel1MinSize, Panel2MinSize, and SplitterWidth.
        /// Returns false when the available dimension is smaller than the sum of both minima plus the splitter, in which case no
        /// valid SplitterDistance can be applied per WinForms/Syncfusion SplitContainer rules.
        /// </summary>
        private static bool TryGetBounds(dynamic splitContainer, out int minDistance, out int maxDistance)
        {
            minDistance = 0;
            maxDistance = 0;

            try
            {
                if (splitContainer == null || splitContainer.IsDisposed)
                {
                    return false;
                }

                // Read properties with defensive null coalescing
                minDistance = splitContainer.Panel1MinSize ?? 0;
                int panel2Min = splitContainer.Panel2MinSize ?? 0;
                int splitterWidth = splitContainer.SplitterWidth ?? 5;

                int dimension = splitContainer.Orientation == Orientation.Horizontal
                    ? splitContainer.Height
                    : splitContainer.Width;

                // Documented constraint: Panel1MinSize <= SplitterDistance <= ClientSize - SplitterWidth - Panel2MinSize
                // (refer to SplitContainer/SplitContainerAdv API docs)
                int required = minDistance + panel2Min + splitterWidth;
                if (dimension < required)
                {
                    return false;
                }

                maxDistance = dimension - panel2Min - splitterWidth;
                return maxDistance >= minDistance;  // Ensure max >= min
            }
            catch
            {
                // If we can't even read the properties, we can't proceed safely
                return false;
            }
        }

        /// <summary>
        /// Sets up a resize handler that maintains a proportional splitter distance during window resize.
        /// Useful for panels that should maintain a 50/50 split or similar proportional layout.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="proportionForPanel1">The proportion for Panel1 (0.5 = 50%)</param>
        public static void SetupProportionalResizing(dynamic splitContainer, double proportionForPanel1 = 0.5)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            Control control = splitContainer;
            dynamic sc = splitContainer;

            control.Resize += (sender, e) =>
            {
                try
                {
                    var safeDist = CalculateSafeDistance(sc, proportionForPanel1);
                    TrySetSplitterDistance(sc, safeDist);
                }
                catch
                {
                    // Ignore resize failures - layout is still adjusting
                }
            };
        }

        /// <summary>
        /// Advanced configuration for SplitContainer with comprehensive sizing control.
        /// Respects all Syncfusion sizing constraints and properties according to official documentation.
        /// Per https://help.syncfusion.com/cr/windowsforms - SplitterDistance constraint:
        /// Panel1MinSize ≤ SplitterDistance ≤ (Dimension - Panel2MinSize - SplitterWidth)
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to configure</param>
        /// <param name="panel1MinSize">Minimum size for Panel1 (0 to disable)</param>
        /// <param name="panel2MinSize">Minimum size for Panel2 (0 to disable)</param>
        /// <param name="desiredDistance">Desired splitter position</param>
        /// <param name="splitterWidth">Optional custom splitter width in pixels</param>
        public static void ConfigureSafeSplitContainerAdvanced(
            dynamic splitContainer,
            int panel1MinSize = 0,
            int panel2MinSize = 0,
            int desiredDistance = 100,
            int? splitterWidth = null)
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return;

            Control control = splitContainer;
            dynamic sc = splitContainer;

            bool initialized = false;
            int minRequiredDimension = 0;

            // Store SplitterWidth if provided
            if (splitterWidth.HasValue && splitterWidth.Value > 0)
            {
                try { sc.SplitterWidth = splitterWidth.Value; } catch { }
            }

            // Use SizeChanged to defer initialization until control has real dimensions
            control.SizeChanged += (s, e) =>
            {
                if (!initialized)
                {
                    // Calculate current dimension based on orientation
                    int currentDimension = sc.Orientation == Orientation.Horizontal
                        ? control.Height
                        : control.Width;

                    // [CRITICAL FIX] Clamp min sizes to prevent exceeding container dimensions
                    int splWidth = 0;
                    try { splWidth = sc.SplitterWidth; } catch { splWidth = 5; }

                    // Calculate available space for both panels
                    int availableForPanels = Math.Max(0, currentDimension - splWidth);

                    // FIXED LOGIC: Ensure sum of min sizes never exceeds available space
                    int safePanel1 = Math.Min(panel1MinSize, availableForPanels - Math.Max(1, panel2MinSize));
                    int safePanel2 = Math.Min(panel2MinSize, availableForPanels - Math.Max(1, panel1MinSize));

                    // If requested sizes exceed available space, scale them down proportionally
                    int requestedTotal = panel1MinSize + panel2MinSize;
                    if (requestedTotal > availableForPanels)
                    {
                        // Scale down proportionally while maintaining minimum viable sizes
                        const int absoluteMin = 25; // Absolute minimum for any panel

                        if (availableForPanels < (absoluteMin * 2))
                        {
                            // Not enough space for both panels - skip initialization
                            return;
                        }

                        // Calculate proportional sizes and CLAMP to available space
                        double scale = (double)(availableForPanels - absoluteMin * 2) / (requestedTotal - absoluteMin * 2);
                        safePanel1 = Math.Min(Math.Max(absoluteMin, (int)(panel1MinSize * scale)), availableForPanels / 2);
                        safePanel2 = Math.Min(availableForPanels - safePanel1, Math.Max(absoluteMin, (int)(panel2MinSize * scale)));

                        // Final defensive clamp: ensure safePanel2 never exceeds remaining space
                        if (safePanel2 > availableForPanels - safePanel1)
                        {
                            safePanel2 = Math.Max(absoluteMin, availableForPanels - safePanel1);
                        }

                        System.Diagnostics.Debug.WriteLine(
                            $"[SafeSplitterDistanceHelper] Scaled min sizes: Panel1 {panel1MinSize}→{safePanel1}, " +
                            $"Panel2 {panel2MinSize}→{safePanel2} (Available={availableForPanels}px)");
                    }
                    else
                    {
                        // Clamp to absolute maximums
                        safePanel1 = Math.Min(panel1MinSize, availableForPanels - 25);
                        safePanel2 = Math.Min(panel2MinSize, availableForPanels - safePanel1);
                    }

                    // Calculate minimum required dimension
                    int minDistance = Math.Max(0, safePanel1);
                    int maxDistance = currentDimension - Math.Max(0, safePanel2) - splWidth;
                    if (maxDistance < minDistance)
                    {
                        return;
                    }

                    int safeDistance = EnforceBounds(desiredDistance, minDistance, maxDistance);
                    int panel1Size = Math.Max(0, safeDistance);
                    int panel2Size = Math.Max(0, currentDimension - safeDistance - splWidth);

                    safePanel1 = Math.Min(Math.Max(0, safePanel1), panel1Size);
                    safePanel2 = Math.Min(Math.Max(0, safePanel2), panel2Size);

                    minRequiredDimension = safePanel1 + safePanel2 + splWidth;

                    // Only initialize if we have enough space
                    if (currentDimension >= minRequiredDimension)
                    {
                        initialized = true;
                        try
                        {
                            // Set min sizes safely - order matters!
                            // Set Panel1MinSize first, then Panel2MinSize
                            if (safePanel1 > 0)
                                sc.Panel1MinSize = safePanel1;
                            if (safePanel2 > 0)
                                sc.Panel2MinSize = safePanel2;

                            // Set splitter distance with bounds enforcement
                            TrySetSplitterDistance(sc, safeDistance);

                            // Log initialization for debugging
                            System.Diagnostics.Debug.WriteLine(
                                $"[SafeSplitterDistanceHelper] SplitContainer configured: " +
                                $"Orientation={sc.Orientation}, " +
                                $"Panel1Min={sc.Panel1MinSize}, " +
                                $"Panel2Min={sc.Panel2MinSize}, " +
                                $"Distance={sc.SplitterDistance}, " +
                                $"Dimension={currentDimension}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[SafeSplitterDistanceHelper] Configuration failed: {ex.Message}");
                        }
                    }
                }
            };
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

            int dimension = splitContainer.Orientation == Orientation.Horizontal
                ? splitContainer.Height
                : splitContainer.Width;

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
        /// </summary>
        /// <param name="splitContainer">The SplitContainer to diagnose</param>
        /// <param name="label">Optional label for identification in output</param>
        /// <returns>Detailed diagnostic string</returns>
        public static string GetDiagnostics(dynamic splitContainer, string label = "SplitContainer")
        {
            if (splitContainer == null || splitContainer.IsDisposed)
                return $"{label}: NULL or DISPOSED";

            var validation = Validate(splitContainer);

            return $"\n{label} DIAGNOSTICS:\n" +
                   $"  Name: {splitContainer.Name}\n" +
                   $"  Status: {(validation.IsValid ? "✓ VALID" : "✗ INVALID")}\n" +
                   $"  {validation}\n" +
                   $"  IsHandleCreated: {splitContainer.IsHandleCreated}\n" +
                   $"  IsDisposed: {splitContainer.IsDisposed}\n" +
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
            minValid = 0;
            maxValid = 0;

            if (splitContainer == null || splitContainer.IsDisposed)
                return false;

            if (!TryGetBounds(splitContainer, out minValid, out maxValid))
                return false;

            return minValid <= maxValid;
        }

        /// <summary>
        /// Clamps the SplitContainer minimum sizes so they fit inside the current client dimension plus a safety buffer.
        /// Panel2MinSize is reduced first and Panel1MinSize only if the overflow remains.
        /// </summary>
        /// <param name="splitContainer">The SplitContainer or SplitContainerAdv that should be adjusted.</param>
        /// <param name="fallbackMin">Minimum pixel size to enforce for each panel.</param>
        /// <param name="safetyBuffer">Pixels reserved for the splitter and breathing room.</param>
        /// <returns>True if any panel minimum was lowered.</returns>
        public static bool ClampMinSizesToAvailableSpace(dynamic splitContainer, int fallbackMin = 25, int safetyBuffer = 40)
        {
            if (splitContainer == null || splitContainer.IsDisposed || fallbackMin <= 0)
                return false;

            try
            {
                int dimension = splitContainer.Orientation == Orientation.Horizontal
                    ? splitContainer.Height
                    : splitContainer.Width;
                int splitterWidth = Math.Max(splitContainer.SplitterWidth ?? 1, 1);
                int available = Math.Max(0, dimension - splitterWidth - safetyBuffer);

                int panel1Min = splitContainer.Panel1MinSize ?? 0;
                int panel2Min = splitContainer.Panel2MinSize ?? 0;
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

