# Trunk Merge Queue — setup & guidance

This repository uses Trunk for CI validation. Trunk also offers a hosted "Merge Queue" service that performs predictive testing and reliably merges PRs in high-velocity repositories.

This document explains what Trunk Merge Queue does, prerequisites, and step-by-step instructions to enable it for this repo.

## What it solves

- Prevents `main` from breaking due to racing merges
- Tests PRs against the predicted future state of `main` (predictive testing)
- Reduces rebase/retest loops and increases merge throughput with batching and parallel lanes

## Prerequisites

1. A Trunk account with the Trunk GitHub App installed for the organization or repository — Trunk Merge Queue is a hosted / paid feature and requires access through the Trunk dashboard (see [Trunk Merge Queue dashboard](https://app.trunk.io/wiley-widget/merge-queue)).
2. Admin or repository settings access on GitHub (to configure branch protections and optionally accept Trunk App installation prompts).
3. CI (GitHub Actions) configured and working for the repository; Trunk can orchestrate your existing CI (we use the `CI/CD Dashboard Enhanced (95% Success Target)` workflow).

## Quick checklist

- [ ] Trunk GitHub App installed and authorized for this repository
- [ ] Branch protection rules for `main` updated to require the Trunk merge-check(s) (see note below)
- [ ] Trunk Merge Queue configured in Trunk web UI for the repo (enable predictive testing + batching if desired)
- [ ] Confirm Trunk has permission to trigger and read current CI workflows (GitHub Actions)

## Suggested branch protection settings

- Require pull-request reviews: 1 required approval (dismiss stale reviews)
- Require status checks to pass before merging: include the CI workflow _job_ named `build` (see the `build-winforms.yml` workflow) and any Trunk-provided checks
- Enforce administrators: true (recommended)
- Disallow force pushes and deletions
- Require linear history and conversation resolution

## Example flow to enable Merge Queue — expanded steps

1. Sign in to Trunk ([Trunk login](https://app.trunk.io/login)) and open the org/repo page (see the Merge Queue dashboard link: [Trunk Merge Queue dashboard](https://app.trunk.io/wiley-widget/merge-queue)).
2. Keep the Trunk-provided status checks in required checks so GitHub blocks direct merge unless Trunk signals the PR is validated to merge.
3. Sign in to Trunk: <https://app.trunk.io/login> and open the org/repo page (your link: <https://app.trunk.io/wiley-widget/merge-queue>)
4. If the Trunk GitHub App isn't installed, follow prompts to install it for the GitHub org or repository (admins only).
5. In the Trunk dashboard, opt into Merge Queue for `main`. Choose whether to require approvals, configure batching/parallel lanes, and set anti-flake options.
6. Choose which checks Trunk should validate before allowing a PR to merge (your GitHub Actions workflows). Add those check names to the repo's branch protection rules if required by your policy.
7. Run a quick test: open a small PR and select "Enter merge queue" (the Trunk UI will show the PR status while predictive testing runs). Confirm the PR can be merged when Trunk approves.

## Caveats & repo-specific notes

- This repo’s CI has some Windows-targeted projects (some build steps run on `windows-latest`). Trunk operates across any CI providers, but predictive tests require the CI to be able to run the combined change. If you expect a PR to land only on Windows runners, ensure Trunk is configured to queue up the appropriate workflows or lanes.
- Avoid enabling both GitHub "require up-to-date branch" AND Trunk Merge Queue at the same time — trunk’s predictive testing removes the need for up-to-date rebase loops.
- If you see flaky tests, enable Trunk's anti-flake or Automatic Bisection features to reduce false negatives.

---

## Machine-readable merge-queue schema (new)

We've added a repository-level machine-readable schema in `.github/merge-queue-schema.yaml` so automation, CI and reviewers can read the queue policy programmatically. Keep this file in sync with this guide.

Example: `.github/merge-queue-schema.yaml`

```yaml
name: Wiley-Widget main
repo: Bigessfour/Wiley-Widget
target_branch: main
testing_mode: draft_pr # or push
required_status_checks:
  - build
required_approving_review_count: 1
enforce_admins: true
allow_force_pushes: false
allow_deletions: false
required_linear_history: true
required_conversation_resolution: true
required_signatures: true
```

Why we have a machine-readable file

- Single source of truth: the schema describes the queue target branch, required status check names, and general protection options that should be applied in branch protection.
- Automation & validation: CI checks (see `.github/workflows/validate-merge-queue-schema.yml`) validate the schema format and prevent accidental misconfiguration.
- Auditability: repository maintainers and automation can detect drift between the documented schema, Trunk dashboard configuration, and branch protection rules.

## Small automation we added

- `.github/validate-merge-queue-schema.yml` — a lightweight action that validates the schema syntax and required keys on PR and push.
- Use the schema to drive these automated steps (examples):
  - Repo-level check to ensure branch-protection has required contexts listed
  - A maintenance cron that verifies the Trunk queue exists and targets the configured branch

## How to use / update the schema

1. Edit `.github/merge-queue-schema.yaml` and open a PR against `main` (the validation workflow will check the file).
2. If you add a new required CI status in the schema, update GitHub branch protection to include the same status (or update `.trunk/trunk.yaml` to include required statuses if you prefer the central config there).
3. When adding or changing statuses, consider whether to apply changes immediately or coordinate a short maintenance window — adding a new required CI check will block merges until it runs successfully on PRs.

## Recommended CI / automation additions (next work items)

- Add a validation job that checks `.trunk/trunk.yaml` vs `.github/merge-queue-schema.yaml` for consistency.
- Add a light monitoring job or scheduled workflow that checks the Trunk dashboard via API (or `trunk` CLI) to ensure the queue is healthy and targeting the configured branch.
- Add a PR template checklist reminding contributors about the merge queue (e.g., "Is this PR enqueued? Add `/trunk merge` comment or use `trunk merge PR_NUMBER`).

---

## Want help implementing the schema end-to-end?

I can:

- Add the monitoring/validation job mentioned above
- Keep the schema validated in CI and add a post-merge job that re-checks live dashboard config
- Update `.vscode/copilot-instructions.md` and `.vscode/mcp.json` with slash commands and guidance so Copilot/GH-MCP users can work with the queue easily

Tell me which automation you want to start next (validate-trunk-config, monitor-queue, update-copilot-instructions) and I’ll implement it and add tests/docs.
