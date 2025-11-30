# Production rollout runbook — enum fixes

Target: apply a limited set of verified enum fixes to production with minimal risk.

## Staging Validation Summary (2025-11-29)

| Table | Column | Rows Fixed | Mapping |
|-------|--------|------------|---------|
| Departments | Fund | 5 | General→0 |
| Departments | Fund | 3 | Proprietary→4 |
| MunicipalAccounts | FundClass | 178 | Governmental→0 |
| MunicipalAccounts | FundClass | 72 | Proprietary→1 |

**Result**: All string enum values successfully converted to integers. 258 backup rows preserved in `enum_fix_backup`.

## Pre-checks (must complete before production window)

- ✅ Confirm `scripts/migrations/enum-fix-staging.sql` was executed on staging and verified.
- ✅ Confirm staging audit shows zero remaining string values for Fund/FundClass.
- ✅ Confirm domain review completed for all ambiguous mappings and `scripts/tools/sql_enum_mappings.json` updated appropriately.
- Schedule a maintenance window if change affects running services.

## Backup plan

- Take a full database backup or snapshot and verify integrity. Do not proceed without a tested restore plan.
- The migration script stores pre-update rows in `dbo.enum_fix_backup`. Confirm this is available post-migration.

## Production steps (operator)

1. Put application read/write modes depending on risk (if needed).
2. Run the `enum-fix-production.sql` script inside a single transaction (or the Apply section manually). Validate counts on updated tables.
3. Run smoke tests and the `sql_enum_audit_v2.py` tool against a taken-down export or test copy to verify results.

## Rollback

- If the immediate post-migration checks fail, roll back the DB backup or use the `dbo.enum_fix_backup` table to restore original values with the included helper SQL.

## Post-migration validation

- Re-run the auditor, app-level smoke tests, and sample data verification.
- Monitor error/telemetry for a period after change.
