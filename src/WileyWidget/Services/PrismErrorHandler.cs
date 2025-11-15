using System;
using Prism.Logging;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Error handler for Prism framework integration
    /// </summary>
    public class PrismErrorHandler : ILogListener
    {
        public void Write(string message, Category category, Priority priority)
        {
            switch (category)
            {
                case Category.Debug:
                    Log.Debug(message);
                    break;
                case Category.Info:
                    Log.Information(message);
                    break;
                case Category.Warn:
                    Log.Warning(message);
                    break;
                case Category.Exception:
                    Log.Error(message);
                    break;
                default:
                    Log.Information(message);
                    break;
            }
        }
    }
}