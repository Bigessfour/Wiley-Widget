using Xunit;

// Disable parallel test execution for the Services test assembly to avoid flakiness
// caused by tests that modify global environment variables or shared process state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
