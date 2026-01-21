using System.Collections.Generic;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Services.Abstractions
{
    /// <summary>
    /// Provides abstraction for managing window state persistence (position, size, maximized/minimized).
    /// Also manages Most Recently Used (MRU) file list persistence.
    /// </summary>
    public interface IWindowStateService
    {
        /// <summary>
        /// Restores the window's position, size, and state from persistent storage.
        /// If no state is found, the window maintains its current state.
        /// </summary>
        /// <param name="form">The form to restore state for.</param>
        void RestoreWindowState(Form form);

        /// <summary>
        /// Saves the window's current position, size, and state to persistent storage.
        /// Called during form closing to preserve user's layout preferences.
        /// </summary>
        /// <param name="form">The form to save state for.</param>
        void SaveWindowState(Form form);

        /// <summary>
        /// Loads the Most Recently Used (MRU) file list from persistent storage.
        /// Returns an empty list if no MRU data exists.
        /// </summary>
        /// <returns>List of recently used file paths, ordered from most to least recent.</returns>
        List<string> LoadMru();

        /// <summary>
        /// Saves the Most Recently Used (MRU) file list to persistent storage.
        /// Overwrites any existing MRU data.
        /// </summary>
        /// <param name="mruList">List of file paths to save as MRU.</param>
        void SaveMru(List<string> mruList);

        /// <summary>
        /// Adds a file to the Most Recently Used (MRU) list.
        /// If the file already exists in the list, it is moved to the top.
        /// Older entries exceeding the maximum MRU size are automatically removed.
        /// </summary>
        /// <param name="filePath">The file path to add to MRU.</param>
        void AddToMru(string filePath);

        /// <summary>
        /// Clears the Most Recently Used (MRU) file list from persistent storage.
        /// </summary>
        void ClearMru();
    }
}
