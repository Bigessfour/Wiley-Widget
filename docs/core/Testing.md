# Testing Strategy

Wiley Widget uses unit tests, integration tests, and end-to-end UI automation to guard regressions.

## Test Types

| Suite              | Location                                                    | Command                                                         |
| ------------------ | ----------------------------------------------------------- | --------------------------------------------------------------- |
| Unit & Integration | `tests/WileyWidget.Tests`                                   | `dotnet test tests/WileyWidget.Tests/WileyWidget.Tests.csproj`  |
| CSX End-to-End     | `scripts/testing`                                           | `pwsh ./scripts/testing/run-e2e.ps1`                            |
| Coverage           | Generated via `dotnet test --collect:"XPlat Code Coverage"` | See [Coverage Registry](../reference/TEST_COVERAGE_REGISTRY.md) |

## Guidelines

- Maintain ≥70% branch coverage on critical modules.
- Keep tests deterministic; isolate external services via fakes located in `tests/WileyWidget.Tests/Fakes`.
- Prefer `DelegateCommand` interactions over code-behind to improve testability.
- For navigation scenarios, use Prism `IRegionManager` with mocked regions; see examples in `ViewModels/Navigation` tests.

## Extended References

- [Testing Checklist](../reference/testing-checklist.md)
- [StaFact Testing Guide](../reference/StaFact_Testing_Guide.md)
- [UI Testing README](../reference/UI_TESTING_README.md)
