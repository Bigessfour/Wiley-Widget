using System.Threading.Tasks;

namespace WileyWidget.Services
{
    public class AILoggingService
    {
        public Task LogAsync(string message)
        {
            // Dummy implementation
            System.Diagnostics.Debug.WriteLine($"AI Log: {message}");
            return Task.CompletedTask;
        }
    }
}