using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Disables xUnit parallel test execution for this assembly because
// some startup registration APIs (Prism.ViewModelLocationProvider) are
// not thread-safe and mutate global static collections during DI
// registration. Running tests serially prevents concurrent updates.
