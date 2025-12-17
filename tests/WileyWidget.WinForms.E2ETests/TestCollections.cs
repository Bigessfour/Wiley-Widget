using WileyWidget.WinForms.E2ETests.Helpers;
using Xunit;

namespace WileyWidget.WinForms.E2ETests;

/// <summary>
/// Test collection definitions for organizing E2E tests into logical groups.
/// Collections ensure tests within the same collection run sequentially, not in parallel.
/// </summary>

/// <summary>
/// Standard UI tests collection - runs with MDI mode disabled for faster, more stable tests.
/// Used by AllViewsUITests, AccountsFormE2ETests, CustomersFormE2ETests, etc.
/// </summary>
[CollectionDefinition("UI Tests")]
public class UiTestsCollection : ICollectionFixture<UiTestSessionFixture>
{
    // This class has no code, and is never instantiated.
    // It serves as a marker for Xunit to group tests and shares UiTestSessionFixture cleanup.
}

/// <summary>
/// MDI-specific tests collection - runs with MDI mode enabled.
/// Used by MdiTests to validate tabbed MDI, docking, and MDI child form scenarios.
/// Isolated from standard UI tests to avoid test environment conflicts.
/// </summary>
[CollectionDefinition("MDI Tests")]
public class MdiTestsCollection : ICollectionFixture<UiTestSessionFixture>
{
    // This class has no code, and is never instantiated.
    // It serves as a marker for Xunit to group tests and shares UiTestSessionFixture cleanup.
}
