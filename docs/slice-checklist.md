# Slice Checklist — Pre-PR Requirements ✅

Use this checklist for each slice prior to opening a PR. Keep PRs focused, descriptive, and fast to review.

- [ ] Branch name follows the naming convention (see docs/branching.md)
- [ ] Opening Issue exists describing acceptance criteria + tests
- [ ] Code compiles locally (dotnet build --no-restore)
- [ ] Unit tests for changed/added files pass locally
- [ ] Integration tests updated or documented if required for the slice
- [ ] Code follows MVVM pattern (no business logic in code-behind)
- [ ] Dependency injection used for services (registered with Prism modules)
- [ ] Added/updated tests achieve ≥80% coverage for changed files
- [ ] Added or updated relevant docs/README/CHANGELOG entries
- [ ] Linters / formatters run — code is whitespace/syntax-clean
- [ ] Trunk pre-checks pass locally (trunk check) where available
- [ ] PR description includes screenshots, test steps, and rollback plan where applicable

Notes:

- If any step is blocked (e.g., requires QuickBooks secrets), add a clear note in the Issue and PR and mark integration tests as "skipped in PRs" with rationale.
- For large changes, break up into smaller slices and use feature flags if necessary.
