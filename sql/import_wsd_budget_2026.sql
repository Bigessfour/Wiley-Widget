SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.BudgetEntries')
          AND name = N'IX_BudgetEntries_AccountNumber_FiscalYear')
    BEGIN
        DROP INDEX IX_BudgetEntries_AccountNumber_FiscalYear ON dbo.BudgetEntries;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.BudgetEntries')
          AND name = N'IX_BudgetEntries_AccountNumber_FiscalYear_FundId')
    BEGIN
        CREATE UNIQUE INDEX IX_BudgetEntries_AccountNumber_FiscalYear_FundId
            ON dbo.BudgetEntries(AccountNumber, FiscalYear, FundId);
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.BudgetEntries')
          AND name = N'CK_Budget_Positive')
    BEGIN
        ALTER TABLE dbo.BudgetEntries DROP CONSTRAINT CK_Budget_Positive;
    END;

    ALTER TABLE dbo.BudgetEntries
        ADD CONSTRAINT CK_Budget_Positive CHECK ([BudgetedAmount] >= 0);

    DECLARE @FundId int = 7;
    DECLARE @DepartmentId int = 4;
    DECLARE @BudgetPeriodId int = 2;
    DECLARE @FiscalYear int = 2026;
    DECLARE @StartDate datetime2 = (SELECT StartDate FROM dbo.BudgetPeriods WHERE Id = @BudgetPeriodId);
    DECLARE @EndDate datetime2 = (SELECT EndDate FROM dbo.BudgetPeriods WHERE Id = @BudgetPeriodId);
    DECLARE @FundDescription nvarchar(100) = N'Wiley Sanitation District';

    DECLARE @Source TABLE (
        AccountNumber nvarchar(20) NOT NULL,
        Description nvarchar(200) NOT NULL,
        BudgetAmount decimal(18,2) NOT NULL,
        AccountType int NOT NULL,
        TypeDescription nvarchar(50) NOT NULL
    );

    INSERT INTO @Source (AccountNumber, Description, BudgetAmount, AccountType, TypeDescription)
    VALUES
        (N'311.00', N'SPECIFIC OWNERSHIP TAXES', 483.94, 11, N'Taxes'),
        (N'301.00', N'SEWAGE SALES', 102173.17, 15, N'Sales'),
        (N'310.10', N'DELINQUENT TAXES', 0.00, 11, N'Taxes'),
        (N'312.00', N'TAX A', 206.05, 11, N'Taxes'),
        (N'313.00', N'SENIOR HOMESTEAD EXEMP', 0.00, 11, N'Taxes'),
        (N'315.00', N'INTEREST INCOME', 14.80, 14, N'Interest'),
        (N'320.00', N'PENALTIES AND INT ON DELIQ TAXES', 0.00, 11, N'Taxes'),
        (N'321.00', N'MISC INCOME', 1810.00, 16, N'Revenue'),
        (N'315.00', N'INTEREST ON INVESTMENTS', 1984.56, 14, N'Interest'),
        (N'322.00', N'GRANT', 630996.09, 13, N'Grants'),
        (N'401.00', N'PERMITS AND ASSESSMENTS', 977.00, 25, N'PermitsAndAssessments'),
        (N'401.10', N'BANK SERVICE', 15.00, 19, N'Services'),
        (N'405.00', N'OUTSIDE SERVICE LAB FEES', 624.00, 19, N'Services'),
        (N'405.10', N'BUDGET, AUDIT, LEGAL', 870.00, 26, N'ProfessionalServices'),
        (N'410.00', N'OFFICE SUPPLIES/ POSTAGE', 1051.13, 18, N'Supplies'),
        (N'413.40', N'EDUCATION', 0.00, 24, N'Expense'),
        (N'415.00', N'CAPITAL OUTLAY', 1064683.59, 29, N'CapitalOutlay'),
        (N'416.00', N'DUES AND SUBSCRIPTIONS', 270.00, 28, N'DuesAndSubscriptions'),
        (N'418.00', N'LIFT-STATION UTILITIES', 1879.71, 20, N'Utilities'),
        (N'420.00', N'COLLECTION FEE', 12000.00, 19, N'Services'),
        (N'425.00', N'SUPPLIES AND EXPENSES', 1909.01, 18, N'Supplies'),
        (N'430.00', N'INSURANCE', 5507.55, 22, N'Insurance'),
        (N'432.53', N'SEWER CLEANING', 0.00, 21, N'Maintenance'),
        (N'445.00', N'TREASURER FEES', 153.13, 19, N'Services'),
        (N'484.00', N'PROPERTY TAXES', 6.88, 24, N'Expense'),
        (N'486.00', N'BACKHOE REPAIRS', 0.00, 21, N'Maintenance'),
        (N'489.00', N'PU USAGE FEE', 2400.00, 20, N'Utilities'),
        (N'491.00', N'FUEL', 1095.87, 24, N'Expense');

    DECLARE @Aggregated TABLE (
        AccountNumber nvarchar(20) NOT NULL PRIMARY KEY,
        Description nvarchar(200) NOT NULL,
        BudgetAmount decimal(18,2) NOT NULL,
        AccountType int NOT NULL,
        TypeDescription nvarchar(50) NOT NULL
    );

    INSERT INTO @Aggregated (AccountNumber, Description, BudgetAmount, AccountType, TypeDescription)
    SELECT
        src.AccountNumber,
        CASE
            WHEN COUNT(DISTINCT src.Description) = 1 THEN MAX(src.Description)
            ELSE STRING_AGG(src.Description, N' / ') WITHIN GROUP (ORDER BY src.Description)
        END AS Description,
        SUM(src.BudgetAmount) AS BudgetAmount,
        MAX(src.AccountType) AS AccountType,
        MAX(src.TypeDescription) AS TypeDescription
    FROM @Source src
    GROUP BY src.AccountNumber;

    MERGE dbo.MunicipalAccounts AS target
    USING @Aggregated AS src
        ON target.AccountNumber = src.AccountNumber
       AND target.DepartmentId = @DepartmentId
       AND target.FundId = @FundId
    WHEN MATCHED THEN
        UPDATE SET
            target.Name = LEFT(src.Description, 100),
            target.Type = src.AccountType,
            target.TypeDescription = src.TypeDescription,
            target.FundType = 4,
            target.FundDescription = @FundDescription,
            target.BudgetPeriodId = @BudgetPeriodId,
            target.BudgetAmount = src.BudgetAmount,
            target.Balance = COALESCE(target.Balance, 0),
            target.IsActive = 1
    WHEN NOT MATCHED THEN
        INSERT (DepartmentId, AccountNumber, BudgetPeriodId, Name, Type, TypeDescription, FundDescription, FundType, FundId, Balance, BudgetAmount, IsActive)
        VALUES (@DepartmentId, src.AccountNumber, @BudgetPeriodId, LEFT(src.Description, 100), src.AccountType, src.TypeDescription, @FundDescription, 4, @FundId, 0, src.BudgetAmount, 1);

    MERGE dbo.BudgetEntries AS target
    USING (
        SELECT
            agg.AccountNumber,
            agg.Description,
            agg.BudgetAmount,
            ma.Id AS MunicipalAccountId
        FROM @Aggregated agg
        INNER JOIN dbo.MunicipalAccounts ma
            ON ma.AccountNumber = agg.AccountNumber
           AND ma.DepartmentId = @DepartmentId
           AND ma.FundId = @FundId
    ) AS src
        ON target.AccountNumber = src.AccountNumber
       AND target.FiscalYear = @FiscalYear
       AND target.FundId = @FundId
    WHEN MATCHED THEN
        UPDATE SET
            target.Description = LEFT(src.Description, 200),
            target.BudgetedAmount = src.BudgetAmount,
            target.Variance = src.BudgetAmount - COALESCE(target.ActualAmount, 0),
            target.DepartmentId = @DepartmentId,
            target.FundId = @FundId,
            target.FundType = 2,
            target.MunicipalAccountId = src.MunicipalAccountId,
            target.StartPeriod = @StartDate,
            target.EndPeriod = @EndDate,
            target.IsGASBCompliant = 1,
            target.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (AccountNumber, Description, BudgetedAmount, ActualAmount, Variance, FiscalYear, StartPeriod, EndPeriod, FundType, DepartmentId, FundId, MunicipalAccountId, EncumbranceAmount, IsGASBCompliant, CreatedAt, UpdatedAt)
        VALUES (src.AccountNumber, LEFT(src.Description, 200), src.BudgetAmount, 0, src.BudgetAmount, @FiscalYear, @StartDate, @EndDate, 2, @DepartmentId, @FundId, src.MunicipalAccountId, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
