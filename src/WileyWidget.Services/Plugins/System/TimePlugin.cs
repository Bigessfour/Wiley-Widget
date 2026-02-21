using System.ComponentModel;
using Microsoft.SemanticKernel;
using System;

namespace WileyWidget.Services.Plugins.System
{
    public class TimePlugin
    {
        [KernelFunction]
        [Description("Gets the current UTC date and time.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("O");

        [KernelFunction]
        [Description("Gets the current local date and time.")]
        public string GetCurrentLocalTime() => DateTime.Now.ToString("F");
    }
}
