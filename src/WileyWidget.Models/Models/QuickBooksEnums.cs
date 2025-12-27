#nullable enable

namespace WileyWidget.Models
{
    /// <summary>
    /// Policies controlling how QuickBooks invoice amount conflicts are handled.
    /// </summary>
    public enum QuickBooksConflictPolicy
    {
        /// <summary>Accept QuickBooks Online (QBO) values and update local records (default).</summary>
        PreferQBO = 0,

        /// <summary>Retain local values and flag the discrepancy for review.</summary>
        KeepLocal = 1,

        /// <summary>Leave changes unresolved and prompt a user to choose resolution.</summary>
        PromptUser = 2
    }
}
