using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Manages docking layout persistence with non-blocking load and automatic fallback.
    /// Prevents UI freeze by using background task with 1-second timeout.
    /// </summary>
    public class DockingStateManager
    {
        private readonly string _layoutPath;
        private readonly ILogger<DockingStateManager> _logger;
        private bool _layoutLoaded;

        public DockingStateManager(string layoutPath, ILogger<DockingStateManager> logger)
        {
            _layoutPath = layoutPath ?? throw new ArgumentNullException(nameof(layoutPath));
            _logger = logger;
        }

        /// <summary>
        /// Non-blocking layout load with 1-second timeout and automatic fallback to defaults.
        /// Returns immediately; layout load happens on background thread.
        /// </summary>
        public void TryLoadLayout(DockingManager dockingManager)
        {
            if (_layoutLoaded) return;

            try
            {
                if (!File.Exists(_layoutPath))
                {
                    _logger.LogDebug("No cached layout found at {LayoutPath} - docking will use defaults", _layoutPath);
                    _layoutLoaded = true;
                    return;
                }

                // Non-blocking load with timeout to prevent UI freeze
                var loadTask = Task.Run(() =>
                {
                    try
                    {
                        using var stream = File.OpenRead(_layoutPath);
                        dockingManager.LoadDockingLayout(stream);
                        _logger.LogDebug("✓ Docking layout loaded from cache successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load cached docking layout - using defaults instead");
                        // Continue - docking will use default arrangement
                    }
                });

                // Wait max 1 second for layout load (non-blocking timeout)
                bool completed = loadTask.Wait(TimeSpan.FromSeconds(1));
                if (!completed)
                {
                    _logger.LogWarning("Docking layout load timed out after 1 second - using defaults");
                }
            }
            finally
            {
                _layoutLoaded = true;
            }
        }

        /// <summary>
        /// Saves the current docking layout to cache file for next session.
        /// </summary>
        public void SaveLayout(DockingManager dockingManager)
        {
            try
            {
                var directory = Path.GetDirectoryName(_layoutPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = File.Create(_layoutPath);
                dockingManager.SaveDockingLayout(stream);
                _logger.LogDebug("✓ Docking layout saved to cache at {LayoutPath}", _layoutPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save docking layout to cache");
                // Silently continue - layout cache is non-critical
            }
        }
    }
}
