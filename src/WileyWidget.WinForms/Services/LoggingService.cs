using System;
using Serilog;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Simple logging service wrapper for proper disposal handling.
    /// </summary>
    public sealed class LoggingService : IDisposable
    {
        private bool _disposed;

        ~LoggingService()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return;
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // Ensure logger is properly disposed to avoid ObjectDisposedException
                try
                {
                    Log.CloseAndFlush();
                }
                catch (Exception ex)
                {
                    // Log disposal errors to console as last resort
                    Console.WriteLine($"Error disposing logger: {ex.Message}");
                }
            }
        }
    }
}
