using WileyWidget.WinForms.E2ETests.Helpers;
using Xunit;

namespace WileyWidget.WinForms.E2ETests;

/// <summary>
/// Test collection definitions for organizing E2E tests into logical groups.
/// Collections ensure tests within the same collection run sequentially, not in parallel.
/// </summary>

/// <summary>
/// Standard UI tests collection - runs with docking panels for stable tests.
/// Used by AllViewsUITests, AccountsFormE2ETests, CustomersFormE2ETests, etc.
/// </summary>
[CollectionDefinition("UI Tests")]
public class UiTestsCollection : ICollectionFixture<UiTestSessionFixture>
{
    // This class has no code, and is never instantiated.
    // It serves as a marker for Xunit to group tests and shares UiTestSessionFixture cleanup.
}
