using System;

namespace WileyWidget.Tests.TestHelpers
{
    /// <summary>
    /// Marker attribute to indicate tests that require MCP integration or special environment.
    /// The test runner can filter or skip these when MCP isn't available.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class McpIntegrationAttribute : Attribute
    {
        public string Reason { get; }
        public McpIntegrationAttribute(string reason = "Requires MCP integration") => Reason = reason;
    }
}
