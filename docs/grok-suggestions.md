# Grok Suggestions — Immediate Rhythm Boosters (from Grok)

These suggestions are copied/adapted from Grok and tuned for this repository so you get instant developer superpowers.

## You already have these interactive servers wired up

- csharp-mcp — full .NET 9 REPL inside the AI environment. Can run any `*.csx` scripts in `/scripts` and has read access to the repo.
- syncfusion-docs — live Syncfusion WinForms documentation fetcher (custom integration).
- github — the assistant has GitHub API access (list runs, download artifacts, create branches, comment, etc.).
- filesystem — the assistant can read/write files directly in this workspace.
- sequential-thinking — forces the assistant to run step-by-step planning for safe edits.

## 1) One-liner data playground (60 seconds)

A script has been added: `scripts/playground/seed-and-query.csx`.

What it does:

- Spins up an in-memory `AppDbContext` for rapid experimentation.
- Runs the built-in `DatabaseSeeder` to ensure seed data exists.
- Queries `MunicipalAccounts` (filters by department name containing "Sewer" and `AccountType.Revenue`) and prints the total and top 10 accounts.

How to use (example command for local MCP):

```
# In the csharp-mcp environment (or run manually if you have CSX support):
# run scripts/playground/seed-and-query.csx

# or use the AI assistant to execute the script and return the objects/rows for you.
```

Why this helps:

- Instant feedback loop for UI development (populate grids, validate sorting/filtering, reproduce edge cases).
- Great for debugging queries, verifying model changes, or creating reproducible test data.

## 2) Instant Syncfusion help

Ask the assistant through the Syncfusion docs server for focused API answers, e.g.:

- "How to enable server-side filtering on SfDataGrid for 50k rows"
- "Compare AllowEditing vs ReadOnly vs EditMode for SfDataGrid"

Why this helps:

- Eliminates guesswork for control behavior and performance-critical settings.
- Use the code snippets directly in small slices (e.g., add server-side paging/filtering to AccountsViewModel).

## 3) Zero-friction fix loop (superpower)

Example flow (3–5 minutes fix loop):

- You: "CI failed on the Accounts slice, run id 987654321"
- Assistant: `/ai triage 987654321` → downloads logs, finds the exception (file + line)
- Assistant: `/ai propose-fix path/to/AccountsViewModel.cs` → provides a concrete fix + new unit test
- You: copy → commit → `/ai enter-merge-queue #42`

This loop reduces lengthy debug cycles — the assistant can propose working code and validation steps instantly.

## 7-Day rhythm (suggested)

A tight, focused weekly cadence to build a strong shipping rhythm using Grok-assisted scripts & docs.

| Day     | Focus Slice                                           | Deliverable                                       | MCP help                                                                 |
| ------- | ----------------------------------------------------- | ------------------------------------------------- | ------------------------------------------------------------------------ |
| Mon–Tue | Accounts E2E (grid + filters)                         | Fully working Accounts tab + filters, no warnings | csharp-mcp seed script + syncfusion-docs for grid-binding questions      |
| Wed     | Charts slice (LiveCharts + optional Syncfusion Chart) | Revenue vs Expense line + pie                     | Ask syncfusion-docs for recommended chart for large datasets             |
| Thu     | Settings persistence (env + user secrets)             | Theme toggle + saved connection strings           | Filesystem server can scaffold config & secrets examples                 |
| Fri     | QuickBooks OAuth UI (UI + token storage)              | Login & token persistence (encrypted)             | github API can create branches/PRs and the assistant can prototype tests |
| Weekend | Polish & retrospective                                | `v1.1-winforms-stable` tag & docs update          | `/ai docs-add README.md` to update status and release notes              |

---

Files added to support these suggestions:

- `scripts/playground/seed-and-query.csx` — seed & query playground script
- `docs/grok-suggestions.md` — this document (you can link from `docs/dev-rhythm.md`)

If you want I can:

- Stage & push these changes to the current PR branch (ci/workflow-improvements) and update the PR body with a short summary.
- Add a short usage guide to the `README.md` for developers who may not use MCP frequently.
- Add a tiny helper script to run the CSX script locally with `dotnet-script` or to run it inside a container for CI demos.

Which of the follow-ups would you like me to do next? (I can push to the current PR branch now if you say go.)
