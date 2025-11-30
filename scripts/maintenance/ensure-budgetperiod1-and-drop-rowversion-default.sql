-- Idempotent script: ensure BudgetPeriod Id=1 exists, ensure MunicipalAccount Id=1 exists, drop default constraint on UtilityCustomers.RowVersion
SET NOCOUNT ON;
PRINT '=== Beginning idempotent maintenance script ===';

-- 1) Ensure BudgetPeriods Id=1 exists
IF OBJECT_ID(N'dbo.BudgetPeriods','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BudgetPeriods WHERE Id = 1)
    BEGIN
        PRINT 'Inserting BudgetPeriods Id=1';
        SET IDENTITY_INSERT dbo.BudgetPeriods ON;
        INSERT INTO dbo.BudgetPeriods ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
        VALUES (1, 2025, N'2025 Current', SYSUTCDATETIME(), 1, '2025-01-01', '2025-12-31 23:59:59', 1);
        SET IDENTITY_INSERT dbo.BudgetPeriods OFF;
    END
    ELSE
    BEGIN
        PRINT 'BudgetPeriods Id=1 already present - nothing to do';
    END
END
ELSE
BEGIN
    PRINT 'Table BudgetPeriods not found - skipping insertion of Id=1';
END

-- 2) Ensure MunicipalAccounts Id=1 exists
IF OBJECT_ID(N'dbo.MunicipalAccounts','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MunicipalAccounts WHERE Id = 1)
    BEGIN
        PRINT 'Inserting MunicipalAccounts Id=1';
        SET IDENTITY_INSERT dbo.MunicipalAccounts ON;
        INSERT INTO dbo.MunicipalAccounts ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription])
        VALUES (1, N'100', 0.0, 0.0, 1, 1, 1, N'General Fund', 1, NULL, N'GENERAL ACCOUNT', NULL, NULL, NULL, 1, N'Asset');
        SET IDENTITY_INSERT dbo.MunicipalAccounts OFF;
    END
    ELSE
    BEGIN
        PRINT 'MunicipalAccounts Id=1 already present - nothing to do';
    END
END
ELSE
BEGIN
    PRINT 'Table MunicipalAccounts not found - skipping insertion of Id=1';
END

-- 3) Drop any default constraint on UtilityCustomers.RowVersion (idempotent)
IF OBJECT_ID(N'dbo.UtilityCustomers','U') IS NOT NULL
BEGIN
    DECLARE @dcName sysname;
    SELECT @dcName = dc.[name]
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'UtilityCustomers' AND c.name = 'RowVersion';

    IF @dcName IS NOT NULL
    BEGIN
        PRINT CONCAT('Dropping default constraint ', @dcName, ' on UtilityCustomers.RowVersion');
        EXEC(N'ALTER TABLE dbo.UtilityCustomers DROP CONSTRAINT [' + @dcName + ']');
    END
    ELSE
    BEGIN
        PRINT 'No default constraint found on UtilityCustomers.RowVersion';
    END
END
ELSE
BEGIN
    PRINT 'Table UtilityCustomers not found - skipping drop of default constraint';
END

PRINT '=== Idempotent maintenance script finished ===';
