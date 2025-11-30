-- enum-fix-production.sql
-- Production migration skeleton for the small set of safe enum updates.
-- DO NOT RUN IN PRODUCTION UNTIL STAGING VALIDATION IS COMPLETE.

-- 0) PREREQUISITES
--  * Confirm logs/audit_after_fix/unresolved_summary.md reviewed and remaining ambiguous values resolved
--  * Ensure staging migration ran successfully and any hotfixes have been applied to mappings
--  * Take a full DB backup / ensure a restore point

-- 1) backup rows (idempotent backup table)
IF OBJECT_ID('dbo.enum_fix_backup', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.enum_fix_backup (
        backup_id INT IDENTITY(1,1) PRIMARY KEY,
        change_ts DATETIME2 DEFAULT SYSUTCDATETIME(),
        table_name SYSNAME NOT NULL,
        pk_name SYSNAME NULL,
        pk_value NVARCHAR(4000) NULL,
        column_name SYSNAME NOT NULL,
        old_value NVARCHAR(MAX) NULL
    );
END
GO

-- 2) capture rows we'll change (all string values that need conversion)
INSERT INTO dbo.enum_fix_backup (table_name, pk_name, pk_value, column_name, old_value)
SELECT 'Departments', 'Id', CAST(Id AS NVARCHAR(4000)), 'Fund', CAST(Fund AS NVARCHAR(MAX))
FROM Departments
WHERE Fund IN ('Recreation', 'General', 'Proprietary');

INSERT INTO dbo.enum_fix_backup (table_name, pk_name, pk_value, column_name, old_value)
SELECT 'MunicipalAccounts', 'Id', CAST(Id AS NVARCHAR(4000)), 'FundClass', CAST(FundClass AS NVARCHAR(MAX))
FROM MunicipalAccounts
WHERE FundClass IN ('Governmental', 'Proprietary');
GO

-- 3) FINAL: run under operations guidance inside transaction
-- Mapping reference:
--   Fund: General→0, Recreation→9, Proprietary→4 (Enterprise)
--   FundClass: Governmental→0, Proprietary→1
BEGIN TRANSACTION;

UPDATE Departments SET Fund = '9' WHERE Fund = 'Recreation';
UPDATE Departments SET Fund = '0' WHERE Fund = 'General';
UPDATE Departments SET Fund = '4' WHERE Fund = 'Proprietary';
UPDATE MunicipalAccounts SET FundClass = '0' WHERE FundClass = 'Governmental';
UPDATE MunicipalAccounts SET FundClass = '1' WHERE FundClass = 'Proprietary';

-- Validation queries (run before committing)
SELECT 'Remaining string Fund values' AS check_item, COUNT(*) AS cnt
FROM Departments WHERE TRY_CAST(Fund AS INT) IS NULL;
SELECT 'Remaining string FundClass values' AS check_item, COUNT(*) AS cnt
FROM MunicipalAccounts WHERE TRY_CAST(FundClass AS INT) IS NULL;

-- Validate / smoke checks then COMMIT
-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
