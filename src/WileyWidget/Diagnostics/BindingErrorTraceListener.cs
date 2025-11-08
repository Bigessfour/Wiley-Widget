using System.Diagnostics;
using System.Text;
using System.Threading;
using Serilog;

namespace WileyWidget.Diagnostics;

/// <summary>
/// Captures WPF binding trace output and forwards it to Serilog so binding warnings and errors
/// are visible in the standard application logs instead of being hidden in the debug console.
/// </summary>
internal sealed class BindingErrorTraceListener : TraceListener
{
    private static readonly ThreadLocal<StringBuilder> MessageBuilder = new(() => new StringBuilder());

    public override void Write(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var builder = GetBuilder();
        builder.Append(message);
    }

    public override void WriteLine(string? message)
    {
        var builder = GetBuilder();
        if (!string.IsNullOrEmpty(message))
        {
            builder.Append(message);
        }

        var finalMessage = builder.ToString();
        builder.Clear();

        if (string.IsNullOrWhiteSpace(finalMessage))
        {
            return;
        }

        Log.Warning("WPF Binding Trace: {BindingMessage}", finalMessage);
    }

    private static StringBuilder GetBuilder()
    {
        var builder = MessageBuilder.Value;
        if (builder is null)
        {
            builder = new StringBuilder();
            MessageBuilder.Value = builder;
        }

        return builder;
    }
}
