using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Diagnostics.Health;

/// <summary>
/// Minimal optional named pipe health endpoint returning a static JSON payload.
/// Enabled via --diag-startup or env var WILEYWIDGET_ENABLE_HEALTHZ=1.
/// Pipe name: wileywidget-healthz
/// </summary>
internal sealed class HealthServer : IDisposable
{
    private const string PIPE_NAME = "wileywidget-healthz";
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;

    public static HealthServer StartIfEnabled(bool diagFlag)
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("WILEYWIDGET_ENABLE_HEALTHZ");
            if (!diagFlag && !string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
                return null;
            return new HealthServer();
        }
        catch { return null; }
    }

    private HealthServer()
    {
        _loopTask = Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                if (server.IsConnected)
                {
                    var payload = Encoding.UTF8.GetBytes("{\"status\":\"Healthy\"}\n");
                    await server.WriteAsync(payload, 0, payload.Length, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loopTask.Wait(200); } catch { }
        _cts.Dispose();
    }
}
