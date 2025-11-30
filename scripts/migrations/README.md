## Enum-fix migration playbook (staging)

This folder contains a safe, reviewable T-SQL migration to apply small, audited enum fixes to a database instance.

Recommended workflow

1. Run the `sql_enum_audit_v2.py` script to generate `logs/audit_after_fix/` and inspect `sql_enum_fix_suggestions.sql`.
2. Create a staging DB snapshot or backup before making any changes.
3. Open and review `enum-fix-staging.sql`; it contains PREP, DRY-RUN, BACKUP and APPLY sections.
4. Run the DRY-RUN queries to confirm the counts in staging match expectations.
5. Run the BACKUP section to store rows to `dbo.enum_fix_backup`.
6. Execute the APPLY portion inside a transaction. Validate counts and application behavior.
7. If anything goes wrong, either ROLLBACK or use the ROLLBACK helper at the bottom of the script.

Notes

- Only apply the upserts in production **after** staging verification. Some unresolved audit entries require manual mapping.
- The `sql_enum_fix_todo.json` file contains findings that need domain review (e.g. non-enum columns or ambiguous values).
