# Branch Naming & Branching Strategy

A consistent branch naming convention makes PRs discoverable and CI easier to manage.

- main — protected; only merged via Trunk / merge queue after CI & reviews pass.
- develop — long-lived integration branch for short term feature grouping.

Branch naming rules:
- feature/{short-description}-{taskId?} — new features and slices (e.g., feature/accounts-quick-filter-532)
- fix/{area}-{short-description} — bug fixes (e.g., fix/services-nullprovider)
- chore/{short-description} — maintenance changes (e.g., chore/upgrade-dotnet-sdk)
- spike/{short-description} — experimental branches for research (merge after PR review and cleanup)

Keep branches small and short-lived (1–3 days). For larger multi-day efforts use feature toggles or split into iterative slices.

Merge rules:
- All PRs require 1 reviewer + green CI + trunk checks.
- Small PRs (<= 500 LOC) are preferred. Use draft PRs to share early when necessary.
- Rebase on main before final merge to ensure a clean trunk run and reduce merge conflicts.

Hotfixes:
- Create hotfix/{short-description} and target main.
- Cherry-pick to develop if necessary.