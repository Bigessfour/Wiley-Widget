// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress CA1305 for StringBuilder.AppendLine() - output formatting doesn't require culture-specific formatting
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Output formatting for reports doesn't require culture-specific formatting", Scope = "module")]

// Suppress CA1816 for GC.SuppressFinalize in helper methods - valid usage for cleanup suppression
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Helper method legitimately suppresses finalization for cleanup", Scope = "member", Target = "~M:WileyWidget.McpServer.Helpers.FormInstantiationHelper.SafeDispose(System.Windows.Forms.Form,WileyWidget.McpServer.Helpers.MockMainForm)")]

// Suppress CA1062 for outputFormat parameters - validated by MCP framework before invocation
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Parameters validated by MCP framework", Scope = "member", Target = "~M:WileyWidget.McpServer.Tools.ValidateFormThemeTool.ValidateFormTheme(System.String,System.String,System.String)~System.String")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Parameters validated by MCP framework", Scope = "member", Target = "~M:WileyWidget.McpServer.Tools.BatchValidateFormsTool.BatchValidateForms(System.String[],System.String,System.Boolean,System.String)~System.String")]
