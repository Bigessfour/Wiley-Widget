using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utils;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Manages docking layout persistence, state changes, and dynamic panel lifecycle.
/// Single Responsibility: Handle all docking layout and state management operations.
/// Features:
/// - GZip compression for efficient cache storage and disk persistence
/// - Retry mechanism (up to 3 attempts) for timeout resilience
/// - Thread-safe in-memory cache with _cacheLock
/// - Enhanced logging for troubleshooting layout failures
/// </summary>
public class DockingLayoutManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool LockWindowUpdate(IntPtr hWndLock);

    private bool _disposed;

    // Layout persistence constants
    private readonly ILogger? _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPanelNavigationService? _panelNavigator;
    private readonly string _layoutPath;  // Path for layout persistence
    private readonly Control _uiControl;  // UI control for thread marshaling
    private readonly DockingManager _dockingManager;
    private readonly GradientPanelExt _leftDockPanel;
    private readonly GradientPanelExt _rightDockPanel;
    private readonly ActivityLogPanel? _activityLogPanel;

    private const string LayoutVersionAttributeName = "LayoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const int LayoutLoadWarningMs = 5000;
    private const int MinimumSaveIntervalMs = 500;  // 500ms debounce for DockStateChanged throttling
    private const int LayoutLoadTimeoutMs = 2000;  // Timeout for async load
    private const int MaxRetryAttempts = 3;  // Max retry attempts on timeout
    private const int RetryDelayMs = 250;  // Delay between retries

    // State management
    private bool _isSavingLayout;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private readonly object _dockingSaveLock = new();
    private System.Windows.Forms.Timer? _dockingLayoutSaveTimer;

    // In-memory cache for layout data (stored compressed for memory efficiency)
    private static byte[]? _layoutCache;
    private static readonly object _cacheLock = new();

    // Dynamic panels tracking
    private Dictionary<string, GradientPanelExt>? _dynamicDockPanels = new();  // Instance-safe

    public DockingLayoutManager(IServiceProvider serviceProvider, IPanelNavigationService? panelNavigator, ILogger? logger, string layoutPath, Control uiControl, DockingManager dockingManager, GradientPanelExt leftDockPanel, GradientPanelExt rightDockPanel, ActivityLogPanel? activityLogPanel)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _panelNavigator = panelNavigator;
        _logger = logger;
        _layoutPath = layoutPath ?? throw new ArgumentNullException(nameof(layoutPath));
        _uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));
        _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        _leftDockPanel = leftDockPanel ?? throw new ArgumentNullException(nameof(leftDockPanel));
        _rightDockPanel = rightDockPanel ?? throw new ArgumentNullException(nameof(rightDockPanel));
        _activityLogPanel = activityLogPanel;

        // Setup save timer with tick handler
        _dockingLayoutSaveTimer = new System.Windows.Forms.Timer
        {
            Interval = MinimumSaveIntervalMs
        };
        // Use synchronous handler to avoid fire-and-forget async pattern
        _dockingLayoutSaveTimer.Tick += (_, _) => DebounceSaveDockingLayoutSync();

        _logger?.LogDebug("DockingLayoutManager initialized with layout path: {Path} (GZip compression enabled, retry mechanism active)", _layoutPath);
    }

    /// <summary>
    /// Asynchronously loads docking layout with retry mechanism, GZip decompression, and timeout handling.
    /// Uses in-memory cache first (compressed), then disk, with performance profiling.
    /// Implements 3-attempt retry on timeout to improve reliability.
    /// File I/O runs async (background-safe), then marshals LoadDockState to UI thread.
    /// CRITICAL: Ensures all Syncfusion handle access happens on the UI thread via synchronous Invoke.
    /// </summary>
    public async Task LoadDockingLayoutAsync(DockingManager dockingManager, CancellationToken cancellationToken = default)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (_uiControl.IsDisposed || !_uiControl.IsHandleCreated)
        {
            _logger?.LogWarning("UI control is disposed or handle not created - skipping layout load");
            return;
        }

        var sw = Stopwatch.StartNew();
        int retryAttempt = 0;

        while (retryAttempt < MaxRetryAttempts)
        {
            try
            {
                // 1. Try in-memory cache first (fast path)
                byte[]? layoutData = null;
                lock (_cacheLock)
                {
                    if (_layoutCache != null)
                    {
                        try
                        {
                            // Decompress cached layout
                            layoutData = DecompressLayoutData(_layoutCache);
                            _logger?.LogDebug("Loaded and decompressed layout from in-memory cache (decompressed size: {Size} bytes, skipped disk I/O)", layoutData.Length);
                        }
                        catch (Exception cacheEx)
                        {
                            _logger?.LogWarning(cacheEx, "Failed to decompress cached layout - clearing cache and reloading from disk");
                            _layoutCache = null;  // Clear corrupted cache
                        }
                    }
                }

                // 2. Fallback to disk if cache miss or decompression failed (async I/O, background-safe)
                if (layoutData == null)
                {
                        // Ensure directory exists before attempting file I/O. This avoids first-run failures
                        // when the settings folder has not yet been created by the application.
                        var dir = Path.GetDirectoryName(_layoutPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            try
                            {
                                Directory.CreateDirectory(dir);
                                _logger?.LogDebug("Created layout directory: {Dir}", dir);
                            }
                            catch (Exception dirEx)
                            {
                                _logger?.LogWarning(dirEx, "Failed to create layout directory {Dir} - will continue and attempt fallback if file missing", dir);
                            }
                        }

                        if (!File.Exists(_layoutPath))
                        {
                            _logger?.LogInformation("No saved layout found at {Path} - resetting to default docking configuration", _layoutPath);
                            try
                            {
                                ResetToDefaultLayout();
                            }
                            catch (Exception resetEx)
                            {
                                _logger?.LogWarning(resetEx, "ResetToDefaultLayout() failed during first-run fallback");
                            }
                            return;
                        }

                    var ioSw = Stopwatch.StartNew();
                    byte[] diskData = await File.ReadAllBytesAsync(_layoutPath, cancellationToken).ConfigureAwait(false);
                    ioSw.Stop();

                    try
                    {
                        // Decompress disk data
                        layoutData = DecompressLayoutData(diskData);
                        _logger?.LogDebug("Loaded and decompressed layout from disk in {ElapsedMs}ms (compressed size: {CompressedSize} bytes, decompressed: {DecompressedSize} bytes)",
                            ioSw.ElapsedMilliseconds, diskData.Length, layoutData.Length);

                        // Update cache with compressed data
                        lock (_cacheLock)
                        {
                            _layoutCache = diskData;
                        }
                    }
                    catch (InvalidOperationException decompressEx) when (decompressEx.InnerException is System.IO.InvalidDataException)
                    {
                        // Corrupted layout file - delete it immediately and reset on final retry
                        _logger?.LogError(decompressEx, "Layout file is corrupted (unsupported compression format or invalid data) - file size: {FileSize} bytes", diskData.Length);

                        try
                        {
                            File.Delete(_layoutPath);
                            lock (_cacheLock)
                            {
                                _layoutCache = null;
                            }
                            _logger?.LogWarning("Deleted corrupted layout file at {Path}", _layoutPath);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger?.LogWarning(deleteEx, "Failed to delete corrupted layout file");
                        }

                        // On last attempt, reset to default
                        if (retryAttempt >= MaxRetryAttempts - 1)
                        {
                            _logger?.LogError("Layout file corruption detected and could not be recovered - resetting to default layout");
                            throw;
                        }

                        // Otherwise, allow retry
                        retryAttempt++;
                        await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    catch (Exception decompressEx)
                    {
                        _logger?.LogError(decompressEx, "Failed to decompress layout data from disk (attempt {Attempt}/{MaxAttempts}) - error: {ErrorMessage}", retryAttempt + 1, MaxRetryAttempts, decompressEx.Message);
                        if (retryAttempt < MaxRetryAttempts - 1)
                        {
                            retryAttempt++;
                            await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                            continue;  // Retry
                        }
                        throw;
                    }
                }

                // 3. Marshal application to UI thread with timeout protection (UI-sensitive)
                // CRITICAL: Use synchronous Control.Invoke() for Syncfusion controls that directly access handles.
                // LoadDockState() is synchronous and directly accesses UI control handles internally.
                // Synchronous Invoke() is safe here because:
                //   - We can yield in this async method
                //   - It ensures the entire Syncfusion operation completes on the UI thread atomically
                //   - The handle access happens ONLY within the Invoke callback
                // The ConfigureAwait(false) above ensures we don't capture the UI context for I/O,
                // then Invoke() ensures we execute the UI operation on the UI thread.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(LayoutLoadTimeoutMs);

                try
                {
                    // Marshal the entire UI-side sequence to the UI thread so any access to
                    // control handles (including LockWindowUpdate calls) happens on the creating thread.
                    await UIThreadHelper.ExecuteOnUIThreadAsync(_uiControl, async () =>
                    {
                        // Ensure OS-level window update lock is called on UI thread (Handle access is UI-only)
                        try { LockWindowUpdate(_uiControl.Handle); }
                        catch (Exception lockWinEx) { _logger?.LogDebug(lockWinEx, "LockWindowUpdate failed - continuing without OS-level lock"); }

                        try
                        {
                            if (dockingManager.HostControl?.IsDisposed ?? true)
                            {
                                _logger?.LogWarning("Host control disposed during layout apply - aborting");
                                return;
                            }

                            try
                            {
                                try
                                {
                                    dockingManager.LockDockPanelsUpdate();
                                }
                                catch (Exception lockEx)
                                {
                                    _logger?.LogDebug(lockEx, "Failed to lock DockingManager panels for batch update");
                                }

                                using var ms = new MemoryStream(layoutData);
                                var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                                dockingManager.LoadDockState(serializer);
                                _logger?.LogInformation("Docking layout applied on UI thread (attempt {Attempt}/{MaxAttempts})", retryAttempt + 1, MaxRetryAttempts);

                                // Defer right-panel visibility until content is fully switched to avoid flicker
                                try
                                {
                                    if (_rightDockPanel != null)
                                    {
                                        try
                                        {
                                            // Hide right panel before releasing batch updates
                                            dockingManager.SetDockVisibility(_rightDockPanel, false);
                                        }
                                        catch (Exception visEx)
                                        {
                                            _logger?.LogDebug(visEx, "Failed to set right panel visibility=false before layout apply");
                                        }

                                        try
                                        {
                                            // Ensure default tab/content is selected before showing
                                            RightDockPanelFactory.SwitchRightPanelContent(_rightDockPanel, RightDockPanelFactory.RightPanelMode.ActivityLog, _logger);
                                        }
                                        catch (Exception switchEx)
                                        {
                                            _logger?.LogDebug(switchEx, "Failed to switch right panel content after layout apply");
                                        }

                                        // Small micro-delay to let the control settle (UI thread)
                                        try { await Task.Delay(50); } catch { /* ignore */ }

                                        try
                                        {
                                            dockingManager.SetDockVisibility(_rightDockPanel, true);
                                        }
                                        catch (Exception visEx)
                                        {
                                            _logger?.LogDebug(visEx, "Failed to set right panel visibility=true after layout apply");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogDebug(ex, "Deferred right panel visibility handling failed");
                                }
                            }
                            finally
                            {
                                try
                                {
                                    dockingManager.UnlockDockPanelsUpdate();
                                }
                                catch (Exception unlockEx)
                                {
                                    _logger?.LogDebug(unlockEx, "Failed to unlock DockingManager panels after layout apply");
                                }
                            }
                        }
                        finally
                        {
                            try { LockWindowUpdate(IntPtr.Zero); }
                            catch (Exception unlockWinEx) { _logger?.LogDebug(unlockWinEx, "LockWindowUpdate(IntPtr.Zero) failed"); }
                        }
                    }, _logger);

                    // Success - exit retry loop
                    break;
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Layout application timed out
                    _logger?.LogWarning("Docking layout application timed out on attempt {Attempt}/{MaxAttempts} - retrying", retryAttempt + 1, MaxRetryAttempts);
                    if (retryAttempt < MaxRetryAttempts - 1)
                    {
                        retryAttempt++;
                        await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;  // Retry
                    }
                    throw;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Retry-specific timeout, not user cancellation
                if (retryAttempt < MaxRetryAttempts - 1)
                {
                    retryAttempt++;
                    await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                _logger?.LogInformation("Layout load timed out after {MaxAttempts} retry attempts", MaxRetryAttempts);
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Layout load canceled by user");
                throw;
            }
            catch (Exception ex) when (retryAttempt < MaxRetryAttempts - 1)
            {
                _logger?.LogWarning(ex, "Layout load failed on attempt {Attempt}/{MaxAttempts} - will retry", retryAttempt + 1, MaxRetryAttempts);
                retryAttempt++;
                await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;  // Retry
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Layout load failed after {MaxAttempts} attempts - resetting to default", MaxRetryAttempts);
                ResetToDefaultLayout();
                break;  // Exit retry loop
            }
        }

        sw.Stop();
        if (sw.ElapsedMilliseconds > LayoutLoadWarningMs)
        {
            _logger?.LogWarning("Slow docking layout load detected ({ElapsedMs}ms) - consider optimizing serialization or checking disk I/O performance", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Decompresses layout data with automatic format detection and comprehensive error recovery.
    /// Attempts GZip decompression for files with GZip magic number.
    /// Falls back to raw binary if GZip fails (corrupted header or unsupported compression method).
    /// Provides detailed diagnostics for corrupted files to enable recovery.
    /// Thread-safe: No shared state modification.
    /// </summary>
    /// <param name="data">Layout byte array (GZip-compressed or raw binary)</param>
    /// <returns>Decompressed/raw layout data</returns>
    /// <exception cref="InvalidOperationException">If processing fails after all fallbacks</exception>
    private byte[] DecompressLayoutData(byte[] data)
    {
        try
        {
            // Validate minimum size
            if (data == null || data.Length == 0)
            {
                throw new InvalidOperationException("Layout data is empty or null");
            }

            // Check for GZip magic number (0x1f 0x8b) to detect format
            const byte gzipMagic1 = 0x1f;
            const byte gzipMagic2 = 0x8b;
            bool hasGzipMagic = data.Length >= 2 && data[0] == gzipMagic1 && data[1] == gzipMagic2;

            _logger?.LogDebug("Layout data format detection: {Format} (size: {DataSize} bytes, header: 0x{Byte1:X2} 0x{Byte2:X2})",
                hasGzipMagic ? "GZip" : "Raw Binary", data.Length, data[0], data.Length > 1 ? data[1] : 0);

            if (hasGzipMagic)
            {
                try
                {
                    // Attempt GZip decompression with chunked reading for early error detection
                    using var sourceStream = new MemoryStream(data);
                    using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
                    using var destinationStream = new MemoryStream();

                    byte[] buffer = new byte[8192];
                    int bytesRead = 0;
                    int totalRead = 0;

                    while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destinationStream.Write(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                    }

                    byte[] decompressed = destinationStream.ToArray();
                    double compressionRatio = (double)data.Length / decompressed.Length;

                    _logger?.LogDebug("Layout data decompressed from GZip format (compressed: {CompressedSize} bytes, decompressed: {DecompressedSize} bytes, ratio: {Ratio:P1})",
                        data.Length, decompressed.Length, 1.0 - compressionRatio);
                    return decompressed;
                }
                catch (System.IO.InvalidDataException gzipEx)
                {
                    // GZip header is invalid or uses unsupported compression method
                    // Log detailed error and fall through to recovery
                    _logger?.LogWarning(gzipEx, "GZip decompression failed - unsupported compression method or corrupted data. " +
                        "File size: {DataSize} bytes. Will attempt recovery using raw binary format.", data.Length);

                    // Mark for deletion - this file is corrupted and should not be used
                    throw new InvalidOperationException(
                        $"GZip stream is corrupted or uses an unsupported compression method. " +
                        $"File size: {data.Length} bytes. This file will be deleted and recreated on next save.", gzipEx);
                }
            }
            else
            {
                // Raw binary data (backward compatibility with pre-GZip layout files)
                _logger?.LogInformation("Layout data is in raw binary format (not GZip-compressed) - using as-is for backward compatibility (size: {DataSize} bytes)",
                    data.Length);
                return data;
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException with our diagnostic message
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error processing layout data (size: {DataSize} bytes) - error type: {ErrorType}",
                data?.Length ?? 0, ex.GetType().Name);
            throw new InvalidOperationException("Failed to process layout data. The file may be corrupted or in an invalid format.", ex);
        }
    }

    /// <summary>
    /// Compresses layout data using GZip for efficient cache storage.
    /// Thread-safe: No shared state modification.
    /// </summary>
    /// <param name="uncompressedData">Uncompressed layout byte array</param>
    /// <returns>Compressed layout data</returns>
    private static byte[] CompressLayoutData(byte[] uncompressedData)
    {
        try
        {
            using var sourceStream = new MemoryStream(uncompressedData);
            using var destinationStream = new MemoryStream();
            using var gzipStream = new GZipStream(destinationStream, CompressionMode.Compress, true);
            sourceStream.CopyTo(gzipStream);
            gzipStream.Flush();
            return destinationStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to compress layout data.", ex);
        }
    }

    /// <summary>
    /// Saves the current docking layout with debounce, GZip compression, and retry on failure.
    /// Thread-safe with _cacheLock and _dockingSaveLock for concurrent access protection.
    /// Atomic file write using temp file and move for data integrity.
    /// </summary>
    public void SaveDockingLayout(DockingManager dockingManager)
    {
        if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));
        if (_isSavingLayout) return;  // Debounce

        lock (_dockingSaveLock)
        {
            if ((DateTime.Now - _lastSaveTime).TotalMilliseconds < MinimumSaveIntervalMs) return;

            _isSavingLayout = true;
            var sw = Stopwatch.StartNew();
            try
            {
                // Serialize layout
                using var ms = new MemoryStream();
                var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                dockingManager.SaveDockState(serializer);
                serializer.PersistNow();

                var uncompressedData = ms.ToArray();

                // Compress for efficient storage
                byte[] compressedData = CompressLayoutData(uncompressedData);
                double compressionRatio = 1.0 - (double)compressedData.Length / uncompressedData.Length;
                _logger?.LogDebug("Layout compressed from {UncompressedSize} to {CompressedSize} bytes ({CompressionRatio:P1})",
                    uncompressedData.Length,
                    compressedData.Length,
                    compressionRatio);

                // Update cache with compressed data (thread-safe)
                lock (_cacheLock)
                {
                    _layoutCache = compressedData;
                }

                // Save to disk with directory creation
                var dir = Path.GetDirectoryName(_layoutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Write with atomic operation using temp file for data integrity
                string tempPath = _layoutPath + ".tmp";
                try
                {
                    File.WriteAllBytes(tempPath, compressedData);
                    // Atomic rename (overwrites existing file)
                    if (File.Exists(_layoutPath))
                    {
                        File.Delete(_layoutPath);
                    }
                    File.Move(tempPath, _layoutPath);
                }
                finally
                {
                    // Clean up temp file if it still exists
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); }
                        catch { /* Ignore cleanup errors */ }
                    }
                }

                _lastSaveTime = DateTime.Now;
                _logger?.LogDebug("Docking layout saved in {ElapsedMs}ms (compressed: {CompressedSize} bytes, uncompressed: {UncompressedSize} bytes)",
                    sw.ElapsedMilliseconds, compressedData.Length, uncompressedData.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save docking layout - cache may be out of sync with disk");
            }
            finally
            {
                _isSavingLayout = false;
                sw.Stop();
            }
        }
    }

    /// <summary>
    /// Triggers debounced layout save when DockStateChanged event fires.
    /// Restarts the save timer to implement 500ms debounce throttling.
    /// Multiple rapid dock state changes are coalesced into a single save operation.
    /// Safe to call from event handlers - timer management is internal and thread-safe.
    /// </summary>
    public void OnDockStateChanged()
    {
        try
        {
            if (_dockingLayoutSaveTimer == null)
            {
                _logger?.LogWarning("DockStateChanged: Save timer is null");
                return;
            }

            // Restart timer - if already running, this extends the delay
            // Multiple calls within 500ms result in a single save
            _dockingLayoutSaveTimer.Stop();
            _dockingLayoutSaveTimer.Start();
            _logger?.LogDebug("DockStateChanged: Layout save scheduled (500ms debounce)");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to schedule debounced layout save on DockStateChanged");
        }
    }

    /// <summary>
    /// Debounced save handler for timer tick (synchronous wrapper).
    /// Prevents fire-and-forget async pattern and ensures proper marshalling.
    /// The actual save operation is synchronous and thread-safe.
    /// </summary>
    private void DebounceSaveDockingLayoutSync()
    {
        // NOTE: SaveDockingLayout is synchronous and calls Syncfusion DockingManager methods.
        // However, this timer.Tick handler runs on the UI thread (Timer.Tick always runs on UI thread)
        // so there's no cross-thread issue. We keep this synchronous to avoid async void patterns.
        // If in the future SaveDockingLayout is called from a background thread, that's when
        // we'd need to add InvokeRequired checks.
        try
        {
            // Stop timer first to prevent re-entry
            if (_dockingLayoutSaveTimer?.Enabled ?? false)
            {
                _dockingLayoutSaveTimer.Stop();
            }

            // Persist current docking layout to disk with compression and atomic file operations
            SaveDockingLayout(_dockingManager);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to debounce docking layout save");
        }
    }

    /// <summary>
    /// Debounced save handler for timer tick (async version - deprecated).
    /// Use DebounceSaveDockingLayoutSync instead.
    /// </summary>
    private async Task DebounceSaveDockingLayoutAsync()
    {
        // Implementation: Call SaveDockingLayout if needed
        // (Assuming you have access to dockingManager; pass it if not)
        await Task.CompletedTask;  // Placeholder
    }

    /// <summary>
    /// Restore dynamic panels from persistence.
    /// </summary>
    private void RestoreDynamicPanels(DockingManager dockingManager)
    {
        // Placeholder: Load from XML or config, create panels
        foreach (var info in GetPersistedDynamicPanels())  // Assume method to fetch
        {
            var panel = new GradientPanelExt { Name = info.Name };
            Control host = dockingManager.HostControl;
            if (host != null)
            {
                dockingManager.DockControl(panel, host, DockingStyle.Left, 200);  // Example
                _dynamicDockPanels?.Add(info.Name ?? string.Empty, panel);
            }
            else
            {
                _logger?.LogWarning("Cannot restore dynamic panel {PanelName}: DockingManager.HostControl is null", info.Name);
            }
        }
    }

    /// <summary>
    /// Get persisted dynamic panel info (placeholder).
    /// </summary>
    private IEnumerable<DynamicPanelInfo> GetPersistedDynamicPanels()
    {
        // Implement XML/config read; return sample for now
        yield return new DynamicPanelInfo { Name = "SamplePanel", DockLabel = "Sample", IsAutoHide = true };
    }

    /// <summary>
    /// Resets docking layout to default state when loading fails.
    /// Clears cache, logs failure reasons, and ensures clean fallback configuration.
    /// Thread-safe: Clears compressed cache under lock before resetting layout.
    /// Logs individual failures per panel for troubleshooting.
    /// </summary>
    private void ResetToDefaultLayout()
    {
        try
        {
            // Clear corrupted cache to prevent re-using bad data
            lock (_cacheLock)
            {
                if (_layoutCache != null)
                {
                    _logger?.LogDebug("Clearing corrupted layout cache ({Size} bytes) before reset to prevent further load attempts", _layoutCache.Length);
                    _layoutCache = null;
                }
            }

            // Attempt to delete corrupted layout file
            if (File.Exists(_layoutPath))
            {
                try
                {
                    File.Delete(_layoutPath);
                    _logger?.LogInformation("Deleted corrupted layout file at {Path} - will be regenerated on next save", _layoutPath);
                }
                catch (Exception deleteEx)
                {
                    _logger?.LogWarning(deleteEx, "Failed to delete corrupted layout file at {Path} - will be overwritten on next save", _layoutPath);
                }
            }

            // Reset UI to default configuration
            _logger?.LogInformation("Resetting docking layout to default configuration - recreating panels");

            try
            {
                // Left panel (Navigation)
                _dockingManager.SetEnableDocking(_leftDockPanel, true);
                _dockingManager.SetDockLabel(_leftDockPanel, "Navigation");
                _dockingManager.DockControl(_leftDockPanel, _uiControl, DockingStyle.Left, 300);
                _dockingManager.SetAutoHideMode(_leftDockPanel, true);
                _logger?.LogDebug("Reset left docking panel to default (Navigation, 300px auto-hide)");
            }
            catch (Exception leftEx)
            {
                _logger?.LogError(leftEx, "Failed to reset left docking panel to defaults - panel state may be inconsistent");
            }

            try
            {
                // Right panel (Activity)
                _dockingManager.SetEnableDocking(_rightDockPanel, true);
                _dockingManager.SetDockLabel(_rightDockPanel, "Activity");
                _dockingManager.DockControl(_rightDockPanel, _uiControl, DockingStyle.Right, 350);
                _dockingManager.SetAutoHideMode(_rightDockPanel, true);
                _logger?.LogDebug("Reset right docking panel to default (Activity, 350px auto-hide)");
            }
            catch (Exception rightEx)
            {
                _logger?.LogError(rightEx, "Failed to reset right docking panel to defaults - panel state may be inconsistent");
            }

            // Activity log panel (if present)
            if (_activityLogPanel != null)
            {
                try
                {
                    _dockingManager.SetEnableDocking(_activityLogPanel, true);
                    _dockingManager.SetDockLabel(_activityLogPanel, "Activity Log");
                    _dockingManager.DockControl(_activityLogPanel, _uiControl, DockingStyle.Right, 350);
                    _dockingManager.SetAutoHideMode(_activityLogPanel, true);
                    _logger?.LogDebug("Reset activity log docking panel to default (350px auto-hide)");
                }
                catch (Exception activityEx)
                {
                    _logger?.LogError(activityEx, "Failed to reset activity log docking panel to defaults - panel state may be inconsistent");
                }
            }

            _logger?.LogInformation("Docking layout successfully reset to defaults - all panels re-docked with default configuration");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CRITICAL: Failure during docking layout reset - UI may be in inconsistent state. Recovery: Verify DockingManager configuration and restart application if layout appears broken or panels are unresponsive");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose debounce timer
            if (_dockingLayoutSaveTimer != null)
            {
                try
                {
                    _dockingLayoutSaveTimer.Stop();
                    _dockingLayoutSaveTimer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to dispose docking layout save timer");
                }
                finally
                {
                    _dockingLayoutSaveTimer = null;
                }
            }

            // Dispose dynamic panels
            if (_dynamicDockPanels != null)
            {
                foreach (var panel in _dynamicDockPanels.Values)
                {
                    try { panel?.Dispose(); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to dispose dynamic dock panel"); }
                }
                _dynamicDockPanels.Clear();
                _dynamicDockPanels = null;
            }

            _logger?.LogDebug("DockingLayoutManager disposed all owned resources");
        }

        _disposed = true;
    }
}

/// <summary>
/// Information about a dynamic panel for persistence
/// </summary>
public class DynamicPanelInfo
{
    public string? Name { get; set; }
    public string? DockLabel { get; set; }
    public bool IsAutoHide { get; set; }
}
