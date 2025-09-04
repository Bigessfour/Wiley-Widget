using System;
using System.Windows;
using Serilog;
using WileyWidget.Configuration;

namespace WileyWidget.Services
{
    /// <summary>
    /// Isolated window state persistence (size, position, maximized) to flatten MainWindow complexity.
    /// </summary>
    public interface IWindowStateService
    {
        void Restore(Window window);
        void Persist(Window window);
    }

    public sealed class WindowStateService : IWindowStateService
    {
        private readonly SettingsService _settingsService;
        public WindowStateService(SettingsService settingsService) => _settingsService = settingsService;

        public void Restore(Window window)
        {
            if (window == null) return;
            var s = _settingsService.Current;
            try
            {
                if (s.WindowWidth.HasValue) window.Width = s.WindowWidth.Value;
                if (s.WindowHeight.HasValue) window.Height = s.WindowHeight.Value;
                if (s.WindowLeft.HasValue) window.Left = s.WindowLeft.Value;
                if (s.WindowTop.HasValue) window.Top = s.WindowTop.Value;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WindowStateService restore failed");
            }
        }

        public void Persist(Window window)
        {
            if (window == null) return;
            var s = _settingsService.Current;
            try
            {
                s.WindowMaximized = window.WindowState == WindowState.Maximized;
                if (window.WindowState == WindowState.Normal)
                {
                    s.WindowWidth = window.Width;
                    s.WindowHeight = window.Height;
                    s.WindowLeft = window.Left;
                    s.WindowTop = window.Top;
                }
                _settingsService.Save();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WindowStateService persist failed");
            }
        }
    }
}
