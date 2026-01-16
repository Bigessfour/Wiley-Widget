using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Runtime.Serialization;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Manages docking layout persistence with non-blocking load and automatic fallback.
    /// Prevents UI freeze by using background task with 1-second timeout.
    /// Includes high-resolution profiling for disk I/O and layout application.
    /// </summary>
    public class DockingStateManager
    {
        private readonly string _layoutPath;
        private readonly ILogger<DockingStateManager> _logger;
        private bool _layoutLoaded;

        // In-memory cache to mitigate slow disk I/O on subsequent requests
        private static byte[]? _layoutCache;
        private static readonly object _cacheLock = new();

        public DockingStateManager(string layoutPath, ILogger<DockingStateManager> logger)
        {
            _layoutPath = layoutPath ?? throw new ArgumentNullException(nameof(layoutPath));
            _logger = logger;
        }

        /// <summary>
        /// Non-blocking layout load with 1-second timeout and automatic fallback to defaults.
        /// Returns immediately; layout load happens on background thread with telemetry.
        /// </summary>
        public void TryLoadLayout(DockingManager dockingManager)
        {
            if (_layoutLoaded) return;

            try
            {
                // Non-blocking load with timeout to prevent UI freeze
                var loadTask = Task.Run(() =>
                {
                    var totalSw = Stopwatch.StartNew();
                    long diskReadMs = 0;
                    long applyLayoutMs = 0;

                    try
                    {
                        byte[]? layoutData = null;

                        // 1. Try In-Memory Cache first
                        lock (_cacheLock)
                        {
                            if (_layoutCache != null)
                            {
                                layoutData = _layoutCache;
                                _logger.LogDebug("Using in-memory layout cache to skip disk I/O");
                            }
                        }

                        // 2. Fallback to Disk I/O if cache empty
                        if (layoutData == null)
                        {
                            if (!File.Exists(_layoutPath))
                            {
                                _logger.LogDebug("No cached layout found at {LayoutPath} - docking will use defaults", _layoutPath);
                                return;
                            }

                            var diskSw = Stopwatch.StartNew();
                            layoutData = File.ReadAllBytes(_layoutPath);
                            diskSw.Stop();
                            diskReadMs = diskSw.ElapsedMilliseconds;

                            lock (_cacheLock)
                            {
                                _layoutCache = layoutData;
                            }
                        }

                        // 3. Apply layout via MemoryStream
                        if (layoutData != null && layoutData.Length > 0)
                        {
                            var applySw = Stopwatch.StartNew();
                            using (var ms = new MemoryStream(layoutData))
                            {
                                var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                                dockingManager.LoadDockState(serializer);
                            }
                            applySw.Stop();
                            applyLayoutMs = applySw.ElapsedMilliseconds;
                        }

                        totalSw.Stop();
                        _logger.LogInformation("✓ Docking layout restored. Total: {TotalMs}ms (DiskRead: {DiskMs}ms, ApplyLayout: {ApplyMs}ms, CacheHit: {CacheHit})", 
                            totalSw.ElapsedMilliseconds, diskReadMs, applyLayoutMs, diskReadMs == 0);

                        if (diskReadMs > 500)
                        {
                            _logger.LogWarning("Performance Alert: Slow Disk I/O detected ({DiskMs}ms). Consider using SSD or investigating background processes.", diskReadMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load cached docking layout - using defaults instead");
                    }
                });

                // Wait max 1 second for layout load (non-blocking timeout)
                bool completed = loadTask.Wait(TimeSpan.FromSeconds(1));
                if (!completed)
                {
                    _logger.LogWarning("Docking layout load timed out after 1 second - using defaults. (Layout initialization will continue in background)");
                }
            }
            finally
            {
                _layoutLoaded = true;
            }
        }

        /// <summary>
        /// Saves the current docking layout to cache file and in-memory cache for next session.
        /// </summary>
        public void SaveLayout(DockingManager dockingManager)
        {
            if (dockingManager == null) throw new ArgumentNullException(nameof(dockingManager));

            try
            {
                var sw = Stopwatch.StartNew();
                
                using (var ms = new MemoryStream())
                {
                    var serializer = new AppStateSerializer(SerializeMode.BinaryFmtStream, ms);
                    dockingManager.SaveDockState(serializer);
                    serializer.PersistNow();

                    var layoutData = ms.ToArray();
                    
                    // Update in-memory cache
                    lock (_cacheLock)
                    {
                        _layoutCache = layoutData;
                    }

                    // Save to disk
                    var directory = Path.GetDirectoryName(_layoutPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(_layoutPath, layoutData);
                }

                sw.Stop();
                _logger.LogDebug("✓ Docking layout saved to cache at {LayoutPath} in {ElapsedMs}ms", _layoutPath, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save docking layout to cache");
            }
        }
    }
}

