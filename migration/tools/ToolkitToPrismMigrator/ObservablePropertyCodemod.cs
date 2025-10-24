using System;

namespace ToolkitToPrismMigrator
{
    /// <summary>
    /// Minimal stub for the observable property codemod used by the migration tool.
    /// The real implementation was removed to avoid Roslyn dependencies; this stub
    /// provides a safe no-op implementation so the tool can be built and run without errors.
    /// </summary>
    internal static class ObservablePropertyCodemod
    {
        public static (bool changed, string newText, object[] matches) Process(string text)
        {
            // No transformation performed by default; return original text and no matches.
            return (false, text, Array.Empty<object>());
        }
    }
}
