using WileyWidget.WinForms.E2ETests.Helpers;
using Xunit;

namespace WileyWidget.WinForms.E2ETests;

/// <summary>
/// Test collection definitions for organizing E2E tests into logical groups.
/// Collections ensure tests within the same collection run sequentially, not in parallel.
/// </summary>

/// <summary>
/// Standard UI tests collection - runs with panels for stable tests.
/// Used by AllViewsUITests, AccountsFormE2ETests, CustomersFormE2ETests, etc.
/// </summary>
[CollectionDefinition("UI Tests", DisableParallelization = true)]
public class UiTestsCollection : ICollectionFixture<UiTestSessionFixture>
{
    // This class has no code, and is never instantiated.
    // It serves as a marker for Xunit to group tests and shares UiTestSessionFixture cleanup.
}

/// <summary>
/// Panel-specific tests collection - runs with panels enabled.
/// Used by PanelTests to validate docking and panel scenarios.
/// Isolated from standard UI tests to avoid test environment conflicts.
/// </summary>
[CollectionDefinition("Panel Tests", DisableParallelization = true)]
public class PanelTestsCollection : ICollectionFixture<UiTestSessionFixture>
{
    // This class has no code, and is never instantiated.
    // It serves as a marker for Xunit to group tests and shares UiTestSessionFixture cleanup.
}
