# Database Migration Audit — 2025-12-25

Summary:

- Server: localhost\SQLEXPRESS
- Database: WileyWidgetDev
- Audit inserted into `dbo.AuditEntries` with EntityType `DatabaseMigrationAudit` (Id=1)

Applied EF Migrations (from \_\_EFMigrationsHistory):

- 20251012223946_AddAppSettingsEntity
- 20251012224059_AddAdvancedSettingsToAppSettings
- 20251013152132_AddBackendEnhancements
- 20251013153211_ApplyBackendEnhancements
- 20251013153700_RemoveHasDataSeeding
- 20251013212727_PendingChangesFix
- 20251014223636_AddEnterpriseSeedData
- 20251014224939_AddUseSeedingForFY2026Budget
- 20251016234651_AddDataSeeding
- 20251018202549_SyncModelAfterSeeding
- 20251021134644_AddQboClientColumnsToAppSettings
- 20251021231427_SeedConservationAccounts
- 20251022200702_AddMunicipalAccountDescriptions
- 20251211110403_AddActivityLog
- 20251218132044_UpdateDatabaseSchema
- 20251222030700_AddMissingSchema_20251221
- 20251222030840_AddFundsTable_20251221
- 20251225175342_AddQBMappingConfiguration

Repository migration files (src/WileyWidget.Data/Migrations):

- 20251012223946_AddAppSettingsEntity.cs
- 20251012223946_AddAppSettingsEntity.Designer.cs
- 20251012224059_AddAdvancedSettingsToAppSettings.cs
- 20251012224059_AddAdvancedSettingsToAppSettings.Designer.cs
- 20251013152132_AddBackendEnhancements.cs
- 20251013152132_AddBackendEnhancements.Designer.cs
- 20251013153901_FixBudgetEntrySelfReference.cs
- 20251021231427_SeedConservationAccounts.cs
- 20251021231427_SeedConservationAccounts.Designer.cs
- 20251022200702_AddMunicipalAccountDescriptions.cs
- 20251022200702_AddMunicipalAccountDescriptions.Designer.cs
- 20251028124500_AddLookupSeeds.cs
- 20251211110403_AddActivityLog.cs
- 20251211110403_AddActivityLog.Designer.cs
- 20251218132044_UpdateDatabaseSchema.cs
- 20251218132044_UpdateDatabaseSchema.Designer.cs
- 20251225175342_AddQBMappingConfiguration.cs
- 20251225175342_AddQBMappingConfiguration.Designer.cs
- AppDbContextModelSnapshot.cs

Key table row counts (WileyWidgetDev):

- BudgetEntries: 41
- Transactions: 1
- QBMappingConfigurations: 0
- DepartmentCurrentCharges: 0
- DepartmentGoals: 0
- AuditEntries: 1 (new audit row)

Notes and recommendations:

- All migrations present in the repository up through `20251225175342_AddQBMappingConfiguration` are recorded in the database `__EFMigrationsHistory`.
- Several repository migration files (e.g., `20251028124500_AddLookupSeeds.cs`, `20251013153901_FixBudgetEntrySelfReference.cs`) appear in the repo but not as applied migrations; verify whether these were consolidated into other migration IDs or intentionally omitted.
- I inserted a single audit row summarizing this check. If you prefer the full JSON report inserted into the DB (longer `Changes` field), I can update the audit row.

Files created/updated:

- `docs/db-audit/2025-12-25-db-migration-audit.md`

If you want, I can now:

- Expand the `Changes` JSON in the audit row to include the full repo vs applied diff.
- Reconcile the few repo migration files not present in `__EFMigrationsHistory` (investigate whether they were squashed or renamed).
- Clean the migration Designer/ModelSnapshot to reflect the edited migration changes.
