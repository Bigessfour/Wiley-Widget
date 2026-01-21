using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Implementation of IWindowStateService using Windows Registry for persistence.
    /// Stores window position, size, state (minimized/maximized/normal) under HKEY_CURRENT_USER\Software\WileyWidget.
    /// </summary>
    public class WindowStateService : IWindowStateService
    {
        protected virtual string WindowStateRegistryPath => @"Software\WileyWidget\WindowState";
        protected virtual string MruRegistryPath => @"Software\WileyWidget\MRU";
        protected virtual int MaxMruSize => 10;

        private readonly ILogger<WindowStateService> _logger;

        public WindowStateService(ILogger<WindowStateService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Restores the window's position, size, and state from the Windows Registry.
        /// If no registry data exists, the window maintains its current size and position.
        /// Validates that the restored position is visible on the current display.
        /// </summary>
        public void RestoreWindowState(Form form)
        {
            if (form == null)
            {
                _logger.LogWarning("RestoreWindowState called with null form");
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(WindowStateRegistryPath);
                if (key == null)
                {
                    _logger.LogDebug("No window state registry key found; using default form layout");
                    return;
                }

                var left = key.GetValue("Left") as int?;
                var top = key.GetValue("Top") as int?;
                var width = key.GetValue("Width") as int?;
                var height = key.GetValue("Height") as int?;
                var state = key.GetValue("WindowState") as int?;

                if (left.HasValue && top.HasValue && width.HasValue && height.HasValue)
                {
                    // Validate that the restored position is visible on the current display
                    var screen = Screen.FromPoint(new Point(left.Value, top.Value));
                    var workingArea = screen.WorkingArea;

                    // Ensure window is visible (at least partially) within screen bounds
                    if (left.Value < workingArea.Right && top.Value < workingArea.Bottom &&
                        left.Value + width.Value > workingArea.Left && top.Value + height.Value > workingArea.Top)
                    {
                        form.Location = new Point(left.Value, top.Value);
                        form.Size = new Size(width.Value, height.Value);
                        _logger.LogDebug("Window state restored: Position=({Left},{Top}), Size=({Width}x{Height})",
                            left.Value, top.Value, width.Value, height.Value);
                    }
                    else
                    {
                        _logger.LogDebug("Restored window position outside visible screen area; using defaults");
                    }
                }

                if (state.HasValue && Enum.IsDefined(typeof(FormWindowState), state.Value))
                {
                    form.WindowState = (FormWindowState)state.Value;
                    _logger.LogDebug("Window state restored: WindowState={State}", state.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore window state from registry; using defaults");
                // Non-fatal: application continues with default window layout
            }
        }

        /// <summary>
        /// Saves the window's current position, size, and state to the Windows Registry.
        /// Called during form closing to preserve user's preferences for next session.
        /// </summary>
        public void SaveWindowState(Form form)
        {
            if (form == null)
            {
                _logger.LogWarning("SaveWindowState called with null form");
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(WindowStateRegistryPath);
                if (key == null)
                {
                    _logger.LogWarning("Could not create registry key for window state persistence");
                    return;
                }

                key.SetValue("Left", form.Location.X, RegistryValueKind.DWord);
                key.SetValue("Top", form.Location.Y, RegistryValueKind.DWord);
                key.SetValue("Width", form.Size.Width, RegistryValueKind.DWord);
                key.SetValue("Height", form.Size.Height, RegistryValueKind.DWord);
                key.SetValue("WindowState", (int)form.WindowState, RegistryValueKind.DWord);

                _logger.LogDebug("Window state saved: Position=({Left},{Top}), Size=({Width}x{Height}), State={State}",
                    form.Location.X, form.Location.Y, form.Size.Width, form.Size.Height, form.WindowState);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save window state to registry");
                // Non-fatal: application continues even if persistence fails
            }
        }

        /// <summary>
        /// Loads the Most Recently Used (MRU) file list from the Windows Registry.
        /// Returns an empty list if no MRU data exists or registry access fails.
        /// </summary>
        public List<string> LoadMru()
        {
            var mruList = new List<string>();

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(MruRegistryPath);
                if (key == null)
                {
                    _logger.LogDebug("No MRU registry key found; returning empty list");
                    return mruList;
                }

                for (int i = 0; i < MaxMruSize; i++)
                {
                    var value = key.GetValue($"File{i}") as string;
                    if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    {
                        mruList.Add(value);
                    }
                }

                _logger.LogDebug("Loaded {Count} MRU entries from registry", mruList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load MRU from registry; returning empty list");
                // Non-fatal: application continues with empty MRU list
            }

            return mruList;
        }

        /// <summary>
        /// Saves the Most Recently Used (MRU) file list to the Windows Registry.
        /// Overwrites any existing MRU data. Only saves files that currently exist.
        /// </summary>
        public void SaveMru(List<string> mruList)
        {
            if (mruList == null || mruList.Count == 0)
            {
                _logger.LogDebug("MRU list is empty; clearing registry MRU entries");
                ClearMru();
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(MruRegistryPath);
                if (key == null)
                {
                    _logger.LogWarning("Could not create registry key for MRU persistence");
                    return;
                }

                // Clear existing values
                foreach (var valueName in key.GetValueNames())
                {
                    key.DeleteValue(valueName, throwOnMissingValue: false);
                }

                // Save current MRU list (only existing files)
                int savedCount = 0;
                for (int i = 0; i < mruList.Count && savedCount < MaxMruSize; i++)
                {
                    if (File.Exists(mruList[i]))
                    {
                        key.SetValue($"File{savedCount}", mruList[i], RegistryValueKind.String);
                        savedCount++;
                    }
                }

                _logger.LogDebug("Saved {Count} MRU entries to registry", savedCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save MRU to registry");
                // Non-fatal: application continues even if MRU persistence fails
            }
        }

        /// <summary>
        /// Adds a file to the Most Recently Used (MRU) list.
        /// If the file already exists in the list, it is moved to the top.
        /// Older entries exceeding the maximum MRU size are automatically removed.
        /// </summary>
        public void AddToMru(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger.LogWarning("Cannot add non-existent file to MRU: {FilePath}", filePath);
                return;
            }

            try
            {
                var mruList = LoadMru();

                // Remove if already in list
                if (mruList.Contains(filePath))
                {
                    mruList.Remove(filePath);
                }

                // Insert at beginning
                mruList.Insert(0, filePath);

                // Trim if exceeded max size
                if (mruList.Count > MaxMruSize)
                {
                    mruList.RemoveRange(MaxMruSize, mruList.Count - MaxMruSize);
                }

                SaveMru(mruList);
                _logger.LogDebug("Added file to MRU: {FilePath}", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add file to MRU: {FilePath}", filePath);
                // Non-fatal: application continues even if MRU add fails
            }
        }

        /// <summary>
        /// Clears all Most Recently Used (MRU) entries from the Windows Registry.
        /// </summary>
        public void ClearMru()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(MruRegistryPath, writable: true);
                if (key == null)
                {
                    _logger.LogDebug("MRU registry key does not exist; nothing to clear");
                    return;
                }

                foreach (var valueName in key.GetValueNames())
                {
                    key.DeleteValue(valueName, throwOnMissingValue: false);
                }

                _logger.LogDebug("Cleared all MRU entries from registry");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear MRU from registry");
                // Non-fatal: application continues even if MRU clear fails
            }
        }
    }
}
