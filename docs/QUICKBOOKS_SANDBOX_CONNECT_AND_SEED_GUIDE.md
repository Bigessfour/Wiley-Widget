# QuickBooks Sandbox Connect And Seed Guide

Use this guide before the first real attempt to connect Wiley Widget to a QuickBooks sandbox company and before any effort to seed that sandbox with representative Wiley data.

## First Clarify What "Seed It" Means

There are two different operations that often get mixed together:

- Connect Wiley Widget to a QuickBooks sandbox company through OAuth.
- Populate the sandbox company with representative data.

Those are not the same thing.

In the current Wiley Widget codebase, the QuickBooks surface already supports diagnostics, connect, test connection, chart-of-accounts import, and sync flows from QuickBooks into Wiley Widget.

What I do not see in the repo today is a dedicated, explicit sandbox-company seeding tool that creates or bulk-loads entities into QuickBooks Online itself.

What the repo does have today is a usable preparation path for sandbox seeding:

- `scripts/quickbooks/prepare-sandbox-seed.ps1` for preflight validation
- `scripts/quickbooks/export-qbo-session.ps1` for exporting the current app session token to the shell
- `scripts/quickbooks/upsert-coa-from-csv.ps1` for chart-of-accounts upsert into QBO sandbox
- `scripts/quickbooks/templates/town-of-wiley-coa.seed.csv` as the canonical Town-of-Wiley COA seed source
- `scripts/quickbooks/sandbox-seed-manifest.template.json` as the canonical prep manifest

That means the first preflight step is to decide whether seeding will happen:

- inside Wiley Widget through new code,
- through Intuit tooling or manual imports,
- or through a separate one-off script.

Do not start with OAuth until that distinction is clear.

## Sandbox Connect Preconditions

Before trying Connect, all of these need to be true.

### 1. Intuit Developer App Exists

- The Intuit Developer account exists.
- The QuickBooks Online app exists in the Intuit Developer Portal.
- The app is using Development credentials, not Production credentials.
- The required scope is enabled: `com.intuit.quickbooks.accounting`.

### 2. Sandbox Company Exists And You Can Sign In

- A QuickBooks sandbox company exists.
- You know which sandbox company will be used for Wiley testing.
- You have an administrator login for that sandbox company.
- You are not using the real Wiley production company for this phase.

### 3. Development Redirect URI Is Registered Correctly

- In Intuit, go to `Settings` -> `Redirect URIs` -> `Development`, then use `Add URI`.
- In Intuit, under Development redirect URIs, register `http://localhost:5000/callback/`.
- Do not rely on editing the prefilled OAuth Playground value inline and navigating away. The Wiley Widget callback must appear as its own registered URI entry after `Add URI`.
- The redirect URI must match exactly, including scheme, host, port, path, casing, and trailing slash.
- Do not use the OAuth Playground redirect URI for Wiley Widget desktop auth.

### 4. Wiley Widget Local Secrets Are In Place

Set these values through user-secrets or the supported environment/secret path:

- `Services:QuickBooks:OAuth:ClientId`
- `Services:QuickBooks:OAuth:ClientSecret`
- `Services:QuickBooks:OAuth:RedirectUri`
- `Services:QuickBooks:OAuth:Environment`

For sandbox, `Environment` must be `sandbox`.

### 5. Local Callback Listener Prerequisites Are Ready

For the default sandbox callback `http://localhost:5000/callback/`:

- HTTP.SYS URL ACL must be registered.
- The port must be available.
- No other local process should already own that callback address.

Typical URL ACL command:

```powershell
netsh http add urlacl url=http://localhost:5000/callback/ user=%USERNAME%
```

### 6. Wiley Widget Diagnostics Must Pass

Before clicking Connect, run QuickBooks diagnostics in the app and confirm:

- `Environment` is `sandbox`
- `Redirect URI` is `http://localhost:5000/callback/`
- `Redirect OK` is `YES`
- Client ID is present
- Client Secret is present
- URL ACL is registered

If diagnostics fail, stop there and fix configuration first.

## Sandbox Seeding Preconditions

Before trying to seed the sandbox company, all of these need to be true.

### 7. Define The Seed Source

Decide where the seed data comes from:

- a scrubbed export from Wiley production-like data,
- hand-built representative sample data,
- or Intuit sandbox sample data plus Wiley-specific additions.

No seed run should start until the data source is explicitly chosen.

### 8. Confirm The Seed Target Is Only Sandbox

The seed process must be hard-guarded so it cannot hit production by mistake.

At minimum, verify:

- environment is `sandbox`
- credentials are Development credentials
- connected realm ID belongs to the sandbox company
- no production Client ID or production redirect URI is present in the runtime path

### 9. Define What Entities Must Be Seeded

Be specific about the minimum useful dataset. For example:

- chart of accounts
- vendors
- customers
- departments or class-like mappings if used
- budgets
- journal entries or expense history needed for actuals

Do not start with "seed everything." Start with the minimum set Wiley Widget actually needs.

### 10. Decide The Seed Mechanism

This is still the main design checkpoint for seeding beyond chart-of-accounts data.

You need to decide whether seeding will happen through:

- the current COA script path,
- a new Wiley Widget seed command for vendors and budgets,
- a one-off API/script path for vendors and budgets,
- CSV/manual import into the sandbox company,
- or some Intuit-supported tooling flow.

Today, the prepared and reviewable path is chart-of-accounts seeding. Vendor and budget seeding still need an explicit execution path before they should be attempted.

### 11. Make The Seed Idempotent

Before running any seed, define how repeat runs behave.

You need answers for:

- can the same seed be run twice safely,
- how duplicates are avoided,
- how records are identified,
- and how the sandbox is reset if a partial seed fails.

### 12. Define A Post-Seed Proof Set

Before starting the first seed, decide what success looks like.

At minimum, prove:

- sandbox connect succeeds
- realm ID is captured
- connection test succeeds
- expected chart of accounts can be read back
- expected budgets or actuals can be read back
- Wiley Widget screens show believable sandbox data

## Recommended Order

This is the safest sequence.

1. Set up the Intuit Development app and sandbox company.
2. Register the exact sandbox redirect URI.
3. Configure Wiley Widget local secrets for sandbox.
4. Register the local URL ACL.
5. Run Wiley Widget diagnostics and fix every mismatch.
6. Connect to the sandbox and confirm realm ID capture.
7. Run a lightweight read-only proof such as connection test or chart-of-accounts retrieval.
8. Review and update `scripts/quickbooks/sandbox-seed-manifest.template.json` for the current sandbox target.
9. Run `scripts/quickbooks/prepare-sandbox-seed.ps1` and clear every blocking item.
10. Export the active sandbox session with `scripts/quickbooks/export-qbo-session.ps1` after a successful connect.
11. Seed the minimum useful dataset, starting with the COA CSV path through `scripts/quickbooks/upsert-coa-from-csv.ps1`.
12. Re-run Wiley Widget proof flows against the seeded sandbox.

## Current Wiley Widget Reality

Today, Wiley Widget appears ready for the connect-and-diagnose part and for a controlled chart-of-accounts seed path.

What still needs explicit definition before broader seeding is the write path for vendors, budgets, customers, and transactions into the QuickBooks sandbox company.

So the honest answer is:

- sandbox connect can be attempted after the OAuth and diagnostics preflight pass,
- chart-of-accounts seeding can be prepared now with the manifest, preflight script, token export, and CSV template,
- but broader sandbox seeding should not be attempted until the entity-specific write path is defined.
