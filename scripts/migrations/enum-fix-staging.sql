-- enum-fix-staging.sql
-- Safe, reversible migration to apply a small set of audited enum fixes in a staging database.
-- This script is intended to be reviewed and run in a staging environment only.

-- Run the following steps manually in staging:
-- 1) Ensure you are on a staging database and you have a full backup/snapshot.
-- 2) Review the `logs/audit_after_fix/sql_enum_fix_suggestions.sql` for context.
-- 3) Run the PREP, DRY-RUN and BACKUP sections below.
-- 4) Execute the UPDATEs inside a transaction and validate counts before committing.

-- ===== PREP: create a deterministic backup table (idempotent) =====
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

-- ===== DRY-RUN: counts for rows we'd change =====
SELECT 'Departments.Fund=Recreation' AS check, COUNT(*) AS rows
FROM Departments
WHERE Fund = 'Recreation';

SELECT 'Departments.Fund=General' AS check, COUNT(*) AS rows
FROM Departments
WHERE Fund = 'General';

SELECT 'MunicipalAccounts.FundClass=Governmental' AS check, COUNT(*) AS rows
FROM MunicipalAccounts
WHERE FundClass = 'Governmental';
GO

-- ===== BACKUP: copy the rows that will be changed into backup table =====
INSERT INTO dbo.enum_fix_backup (table_name, pk_name, pk_value, column_name, old_value)
SELECT 'Departments', 'Id', CAST(Id AS NVARCHAR(4000)), 'Fund', CAST(Fund AS NVARCHAR(MAX))
FROM Departments
WHERE Fund IN ('Recreation', 'General', 'Proprietary');

INSERT INTO dbo.enum_fix_backup (table_name, pk_name, pk_value, column_name, old_value)
SELECT 'MunicipalAccounts', 'Id', CAST(Id AS NVARCHAR(4000)), 'FundClass', CAST(FundClass AS NVARCHAR(MAX))
FROM MunicipalAccounts
WHERE FundClass IN ('Governmental', 'Proprietary');
GO

-- ===== APPLY: make the changes inside an explicit transaction =====
-- Mapping reference:
--   Fund: General→0, Recreation→9, Proprietary→4 (Enterprise)
--   FundClass: Governmental→0, Proprietary→1
BEGIN TRANSACTION;

UPDATE Departments SET Fund = '9' WHERE Fund = 'Recreation';
UPDATE Departments SET Fund = '0' WHERE Fund = 'General';
UPDATE Departments SET Fund = '4' WHERE Fund = 'Proprietary';
UPDATE MunicipalAccounts SET FundClass = '0' WHERE FundClass = 'Governmental';
UPDATE MunicipalAccounts SET FundClass = '1' WHERE FundClass = 'Proprietary';

-- Verify counts before committing. If anything looks wrong, ROLLBACK.
SELECT 'Departments updated to 9 (Recreation)' AS label, COUNT(*) FROM Departments WHERE Fund = '9';
SELECT 'Departments updated to 0 (General)' AS label, COUNT(*) FROM Departments WHERE Fund = '0';
SELECT 'Departments updated to 4 (Proprietary)' AS label, COUNT(*) FROM Departments WHERE Fund = '4';
SELECT 'MunicipalAccounts FundClass updated to 0 (Governmental)' AS label, COUNT(*) FROM MunicipalAccounts WHERE FundClass = '0';
SELECT 'MunicipalAccounts FundClass updated to 1 (Proprietary)' AS label, COUNT(*) FROM MunicipalAccounts WHERE FundClass = '1';
SELECT 'Remaining string Fund values' AS label, COUNT(*) FROM Departments WHERE TRY_CAST(Fund AS INT) IS NULL;
SELECT 'Remaining string FundClass values' AS label, COUNT(*) FROM MunicipalAccounts WHERE TRY_CAST(FundClass AS INT) IS NULL;

-- COMMIT when you're satisfied, otherwise ROLLBACK.
-- COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;

GO

-- ===== ROLLBACK helper (example) =====
-- Use this to roll back changes from the enum_fix_backup table after a committed transaction.
-- NOTE: Only run if you committed and need to restore.
-- UPDATE d
-- SET d.Fund = b.old_value
-- FROM Departments d
-- JOIN dbo.enum_fix_backup b
--   ON b.table_name = 'Departments' AND b.column_name = 'Fund' AND TRY_CAST(b.pk_value AS INT) = d.Id;

-- UPDATE m
-- SET m.FundClass = b.old_value
-- FROM MunicipalAccounts m
-- JOIN dbo.enum_fix_backup b
--   ON b.table_name = 'MunicipalAccounts' AND b.column_name = 'FundClass' AND TRY_CAST(b.pk_value AS INT) = m.Id;
