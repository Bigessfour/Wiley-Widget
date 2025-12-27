-- Idempotent SQL for FixBudgetEntrySelfReference
-- Drop existing FK if exists, then create with ON DELETE NO ACTION (Restrict)
IF OBJECT_ID('dbo.BudgetEntries','U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BudgetEntries_BudgetEntries_ParentId' AND parent_object_id = OBJECT_ID('dbo.BudgetEntries'))
    BEGIN
        ALTER TABLE dbo.BudgetEntries DROP CONSTRAINT FK_BudgetEntries_BudgetEntries_ParentId;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BudgetEntries_BudgetEntries_ParentId' AND parent_object_id = OBJECT_ID('dbo.BudgetEntries'))
    BEGIN
        ALTER TABLE dbo.BudgetEntries ADD CONSTRAINT FK_BudgetEntries_BudgetEntries_ParentId FOREIGN KEY (ParentId) REFERENCES dbo.BudgetEntries (Id) ON DELETE NO ACTION;
    END
END
GO
