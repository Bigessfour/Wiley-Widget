# QuickBooks API Validation Matrix

Use this document to validate Wiley Widget against the QuickBooks Online Accounting API incrementally without pretending the entire Intuit surface is one testable unit.

This is not a copy of the Intuit API reference. It is the Wiley Widget control document that answers four questions for every QuickBooks capability:

- Is the capability implemented in this repo?
- Which Intuit documentation governs it?
- How is it currently validated?
- What is still missing before the capability is considered proven?

## Source Documentation

- Get started: https://developer.intuit.com/app/developer/qbo/docs/get-started
- Create requests and base URL rules: https://developer.intuit.com/app/developer/qbo/docs/get-started/create-a-request
- REST behavior, query limits, throttling, minor versions: https://developer.intuit.com/app/developer/qbo/docs/learn/rest-api-features
- Accounting API Explorer root: https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account

## Validation States

- `Implemented`: code exists, but there is no meaningful proof yet.
- `Unit validated`: request building, parsing, or local behavior is covered by unit tests.
- `Sandbox contract validated`: the capability has been exercised successfully against a real QuickBooks sandbox company.
- `Workflow validated`: a real Wiley Widget user flow has proven the capability end to end.
- `Out of scope`: intentionally not supported by Wiley Widget today.

## How To Cover The Entire API Without Getting Lost

Do not validate the Intuit API by reading the docs top to bottom and writing random tests. Break it into layers and require each in-scope entity to pass the same validation ladder.

### Layer 0. Platform Rules

Validate these once and reuse them across every entity:

- OAuth environment selection
- sandbox vs production base URLs
- request headers and JSON content type
- rate limiting and 429 handling
- query paging limits and max result handling
- minor version usage
- realm ID handling

### Layer 1. Entity Families

Validate entity families in this order:

1. Auth and connection lifecycle
2. Read-only foundational entities: CompanyInfo, Account, Customer, Vendor
3. Transactional read entities: Invoice, Purchase, JournalEntry, Budget
4. Write and sync flows actually used by Wiley Widget
5. Batch, CDC, report entities, and niche entities only if they become product scope

### Layer 2. Per-Entity Definition Of Done

An entity is only considered covered when all relevant boxes are complete:

- Doc row exists in this matrix
- Repo implementation is mapped to the doc row
- Request shape is statically validated where possible
- Parser and local behavior have unit coverage
- Sandbox contract test exists for the supported operation set
- At least one real Wiley Widget workflow proves the business path

## Current Wiley Widget Surface

The table below inventories the QuickBooks surface currently visible in the repo. It does not claim complete Intuit API coverage. It shows what Wiley Widget actually uses or exposes today.

| Domain                                                              | Intuit doc anchor                                            | Repo surface                                                                                                                                                         | Current proof                                                                                                                                                                              | State          | Main divergence risk                                                                          | Next required step                                                                                |
| ------------------------------------------------------------------- | ------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| OAuth and token lifecycle                                           | Get started, create requests, scopes, REST rules             | `IQuickBooksAuthService`, `QuickBooksAuthService`                                                                                                                    | `QuickBooksAuthServiceTests` covers invalid refresh token cleanup and reauthorize behavior                                                                                                 | Unit validated | Redirect URI drift, realm mismatch, token refresh edge cases                                  | Add sandbox contract tests for authorize, refresh, revoke, and realm capture                      |
| Connection, diagnostics, callback readiness                         | Get started, create requests, sandbox/prod environment rules | `QuickBooksService.TestConnectionAsync`, `CheckUrlAclAsync`, `RunDiagnosticsAsync`, `ConnectAsync`, `DisconnectAsync`, `GetConnectionStatusAsync`, `QuickBooksPanel` | `QuickBooksViewModelTests` cover reauthorize guidance; sandbox/prod guides exist                                                                                                           | Implemented    | Local callback prerequisites, auth success but workflow failure, redundant probes, throttling | Add contract test suite for connect, test connection, disconnect, and diagnostics against sandbox |
| Company information                                                 | API Explorer `CompanyInfo` entity                            | `IQuickBooksCompanyInfoService`, `QuickBooksCompanyInfoService`                                                                                                      | `QuickBooksCompanyInfoServiceTests` validate object and nested-field parsing                                                                                                               | Unit validated | Response-shape drift and cache invalidation behavior                                          | Add sandbox contract test for `CompanyInfo` query and cache refresh                               |
| Chart of accounts read/cache                                        | API Explorer `Account` entity                                | `IQuickBooksChartOfAccountsService`, `QuickBooksChartOfAccountsService`, `QuickBooksService.GetChartOfAccountsAsync`                                                 | `QuickBooksServiceTests` cover paginated account retrieval; account explorer docs define required/writable fields                                                                          | Unit validated | Unsupported fields, paging edge cases, locale-specific account constraints                    | Add sandbox contract tests for query/read account flows and cache assertions                      |
| Account read/filter/balance wrapper                                 | API Explorer `Account` entity                                | `IQuickBooksAccountService`, `QuickBooksAccountService`                                                                                                              | `QuickBooksAccountServiceTests` cover classification filtering and account balance lookup through the real response parser                                                                 | Unit validated | Filter mismatch vs documented fields, stale cache assumptions                                 | Add sandbox contract tests for account balance retrieval and query/read parity                    |
| Customer and vendor reads                                           | API Explorer `Customer` and `Vendor` entities                | `QuickBooksService.GetCustomersAsync`, `GetVendorsAsync`, consumer usage in view models and sync paths                                                               | `QuickBooksServiceDataAccessTests` cover injected-data-service customer and vendor retrieval seams                                                                                         | Unit validated | Assumed SDK defaults, paging, sparse/null fields                                              | Add sandbox contract tests for read/query operations                                              |
| Invoice reads                                                       | API Explorer `Invoice` entity                                | `QuickBooksService.GetInvoicesAsync`                                                                                                                                 | `QuickBooksServiceDataAccessTests` cover enterprise invoice query generation; `QuickBooksQueryValidationTests` keep `Invoice` queries inside the documented entity surface                 | Unit validated | Query semantics, enterprise filtering assumptions, custom-field mismatch                      | Add sandbox contract tests for invoice query/read                                                 |
| Purchase-based department expense queries                           | API Explorer `Purchase` entity, REST query rules             | `QuickBooksService.QueryExpensesByDepartmentAsync`, `DepartmentExpenseService`                                                                                       | `QuickBooksServiceTests` cover date-range query shape and local department matching; `QuickBooksQueryValidationTests` reject `Ref.Name` query predicates and undocumented queried entities | Unit validated | Unsupported IDS query fields, per-department fan-out throttling, data-model mismatch          | Add sandbox contract tests for purchase query behavior and an EVS workflow proof run              |
| Journal entry reads                                                 | API Explorer `JournalEntry` entity                           | `QuickBooksService.GetJournalEntriesAsync`, `QuickBooksBudgetSyncService`                                                                                            | `QuickBooksServiceTests` cover injected-data-service journal retrieval                                                                                                                     | Unit validated | Date-range query behavior and high-volume paging                                              | Add sandbox contract tests for journal entry date-window reads                                    |
| Budget reads and budget import into app                             | API Explorer `Budget` entity                                 | `QuickBooksService.GetBudgetsAsync`, `SyncBudgetsToAppAsync`, `QuickBooksBudgetSyncService`                                                                          | `QuickBooksServiceTests` cover budget retrieval through API client seam                                                                                                                    | Unit validated | SDK/entity support gaps and mapping drift into local budget model                             | Add sandbox contract tests for budget reads and a workflow test for sync into app models          |
| Vendor sync between app and QBO                                     | API Explorer `Vendor` entity                                 | `QuickBooksService.SyncLocalVendorsToQboAsync`, `SyncVendorsToAppAsync`                                                                                              | No active tests found                                                                                                                                                                      | Implemented    | Upsert identity rules and duplicate handling                                                  | Add unit tests for mapping and sandbox contract tests for create/update/read-back                 |
| Sandbox account seeding and COA prep                                | API Explorer `Account` entity, create/update semantics       | `IQuickBooksSandboxSeederService`, `QuickBooksSandboxSeederService`, `scripts/quickbooks/*`                                                                          | Process docs exist in `QUICKBOOKS_SANDBOX_CONNECT_AND_SEED_GUIDE.md`                                                                                                                       | Implemented    | Create payload divergence from Intuit required fields and account-type rules                  | Add contract proof for seeded accounts and mark exact supported account templates                 |
| Reports, CDC, batch, payments, inventory, niche accounting entities | Multiple API Explorer sections                               | No active Wiley Widget implementation found                                                                                                                          | None                                                                                                                                                                                       | Out of scope   | Accidental scope creep and false confidence                                                   | Keep explicitly out of scope until product requirements pull them in                              |

## Recommended Incremental Validation Program

### Wave 1. Platform And Auth

Finish the shared plumbing before expanding entity coverage:

- OAuth authorize/refresh/revoke contract tests
- sandbox vs production base URL assertions
- diagnostics contract proof
- 429 retry and request-throttling verification against documented limits

### Wave 2. Core Read-Only Accounting Entities

Validate the entities that other features depend on:

- CompanyInfo
- Account
- Customer
- Vendor

### Wave 3. Transactional Reads Used By Wiley Widget

These are the first high-value business flows:

- Invoice
- Purchase
- JournalEntry
- Budget

### Wave 4. Writes And Synchronization

Only validate write semantics for operations Wiley Widget really performs:

- vendor sync
- sandbox account seeding
- budget sync into app models
- chart-of-accounts import path

### Wave 5. Workflow Proofs

For release readiness, prove real user flows instead of isolated API calls:

- QuickBooks panel connect, diagnostics, disconnect
- chart-of-accounts import
- actuals sync into budget data
- EVS or related department-expense workflow with real sandbox data

## Static Validation Rules To Add Next

These checks catch divergence earlier than runtime. The first guardrail now exists in `QuickBooksQueryValidationTests` and should be expanded rather than replaced.

- Expand the existing query validator from `Ref.Name` predicate rejection into field-level allowlists for each queried entity.
- Require every QBO entity wrapper to declare the canonical Intuit doc URL.
- Require every supported entity operation to have a row in this matrix.
- Fail tests if a supported operation has no unit test and no sandbox contract test.
- Log `intuit_tid` for every non-success API response.

## Release Gate Recommendation

Before any candidate that claims QuickBooks support is considered ready:

- every in-scope row above must be marked at least `Unit validated`
- auth, connection, company info, account, and purchase flows must be `Sandbox contract validated`
- the exact user workflows included in the release must be `Workflow validated`

## Related Wiley Widget Docs

- [docs/QUICKBOOKS_SANDBOX_CONNECT_AND_SEED_GUIDE.md](docs/QUICKBOOKS_SANDBOX_CONNECT_AND_SEED_GUIDE.md)
- [docs/QUICKBOOKS_PRODUCTION_CUTOVER_GUIDE.md](docs/QUICKBOOKS_PRODUCTION_CUTOVER_GUIDE.md)
- [docs/TOWN_RELEASE_PLAYBOOK.md](docs/TOWN_RELEASE_PLAYBOOK.md)
- [docs/PRE_RELEASE_CHECKLIST.md](docs/PRE_RELEASE_CHECKLIST.md)
