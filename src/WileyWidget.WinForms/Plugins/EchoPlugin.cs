using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// A tiny example plugin used in tests to validate plugin auto-registration.
    /// </summary>
    public sealed class EchoPlugin
    {
        [KernelFunction("echo")]
        [Description("Echoes the provided message.")]
        public string Echo([Description("Message to echo")] string message)
        {
            return message;
        }
    }
}
