SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @FundId int = 1;
    DECLARE @BudgetPeriodId int = 2;
    DECLARE @FiscalYear int = 2026;
    DECLARE @StartDate datetime2 = (SELECT StartDate FROM dbo.BudgetPeriods WHERE Id = @BudgetPeriodId);
    DECLARE @EndDate datetime2 = (SELECT EndDate FROM dbo.BudgetPeriods WHERE Id = @BudgetPeriodId);
    DECLARE @FundDescription nvarchar(100) = N'General Fund';

    DECLARE @Source TABLE (
        DepartmentId int NOT NULL,
        AccountNumber nvarchar(20) NOT NULL,
        Description nvarchar(200) NOT NULL,
        BudgetAmount decimal(18,2) NOT NULL
    );

    INSERT INTO @Source (DepartmentId, AccountNumber, Description, BudgetAmount)
    VALUES
    (1, N'310.00', N'WSD COLLECTION FEE', 6000.00),
    (1, N'311.10', N'DELINQUENT PROPERTY TAXES', 0.00),
    (1, N'311.20', N'SENIOR HOMESTEAD EXEMPTION', 1500.00),
    (1, N'312.00', N'SPECIFIC OWNERSHIP TAXES', 5100.00),
    (1, N'313.00', N'ADDITIONAL MV', 1775.00),
    (1, N'314.00', N'TAX A', 2500.00),
    (1, N'315.00', N'SB22-238 REV REIMB - PROP TAXES', 0.00),
    (1, N'318.20', N'FRANCHISE FEE', 7058.00),
    (1, N'319.00', N'PENALTIES & INTEREST ON', 35.00),
    (1, N'320.00', N'CDOT', 0.00),
    (1, N'321.10', N'LIQUOR LICENSES', 0.00),
    (1, N'321.70', N'BUSINESS LICENSES & PERMITS', 0.00),
    (1, N'322.70', N'ANIMAL LICENSES', 50.00),
    (1, N'324.00', N'WEED CONTROL', 0.00),
    (1, N'332.1', N'MINERAL LEASE', 360.00),
    (1, N'333.00', N'CIGARETTE TAXES', 240.00),
    (1, N'334.00', N'SEVERANCE', 27.00),
    (1, N'334.31', N'HIGHWAYS USERS', 18153.00),
    (1, N'336.00', N'SALES TAX', 120000.00),
    (1, N'337.17', N'COUNTY ROAD & BRIDGE', 1460.00),
    (1, N'341.40', N'COPIES, FAX ETC', 0.00),
    (1, N'350.00', N'WILEY HAY DAYS DONATIONS', 10000.00),
    (1, N'355.00', N'GOCO GRANT', 0.00),
    (1, N'361.00', N'INTEREST EARNINGS', 325.00),
    (1, N'362.00', N'RENT / DONATIONS', 2500.00),
    (1, N'363.00', N'LEASE', 1100.00),
    (1, N'364.00', N'SALE/LEASE-FIXED ASSETS', 0.00),
    (1, N'365.00', N'DIVIDENDS', 100.00),
    (1, N'368.00', N'MISC', 0.00),
    (1, N'370.00', N'HOUSING AUTHORITY MGT FEE', 12000.00),
    (1, N'372.20', N'TRANSFERS FROM OTHER FUNDS', 0.00),
    (1, N'372.30', N'HOUSING AUTHORITY GROUND MAINT', 0.00),
    (1, N'373.00', N'PICKUP USAGE FEE', 2400.00),
    (1, N'374.00', N'CODE ENFORCEMENT FINES', 0.00),
    (1, N'410.00', N'WILEY HAY DAYS EXP', 2500.00),
    (1, N'413.10', N'MAYOR EXPENSE', 0.00),
    (1, N'413.20', N'ADMIN CLERK SALARIES', 45000.00),
    (1, N'413.21', N'ADMIN DEPUTY CLERK SALARIES', 7500.00),
    (1, N'413.22', N'ADMIN CODE ENFORCEMENT', 2000.00),
    (1, N'413.30', N'PAYROLL TAXES - FICA', 10575.00),
    (1, N'413.40', N'EDUCATION', 500.00),
    (1, N'413.50', N'TRAVEL & TRANSPORTATION', 150.00),
    (1, N'413.60', N'PAYROLL TAXES - UNEMPLOYMENT', 360.00),
    (1, N'413.70', N'FAMLI', 900.00),
    (1, N'413.80', N'SIMPLE IRA', 5000.00),
    (1, N'415.11', N'AUDIT & BUDGET EXPENSE', 1000.00),
    (1, N'415.12', N'PROF SERVICES', 0.00),
    (1, N'415.17', N'OFFICE SUPPLIES', 5000.00),
    (1, N'415.18', N'COUNTY TAX ASSESSMENT / POSTAGE', 82.00),
    (1, N'415.19', N'OFFICE EQUIPMENT', 1500.00),
    (1, N'415.20', N'LEGAL', 300.00),
    (1, N'415.21', N'ADMINISTRATION EXPENSES', 1100.00),
    (1, N'415.50', N'INSURANCE BLDG', 4020.00),
    (1, N'415.51', N'INSURANCE PUBLIC LIAB', 4356.00),
    (1, N'415.52', N'INSURANCE WORKERS COMP', 2000.00),
    (1, N'415.53', N'EMPLOYEE HEALTH INSURANCE', 28000.00),
    (1, N'415.54', N'BANK CHARGES', 90.00),
    (1, N'415.75', N'PET CLINIC', 100.00),
    (1, N'415.76', N'RECYCLING', 1700.00),
    (1, N'416.00', N'DUES & SUBS', 5600.00),
    (1, N'417.00', N'TREASURER''S FEES', 1700.00),
    (1, N'418.00', N'SERVICE CONTRACT', 0.00),
    (1, N'419.42', N'BUILDING MAINTENANCE', 1000.00),
    (1, N'419.44', N'BUILDING SUPPLIES/MAINT', 200.00),
    (1, N'419.50', N'BUILDING UTILITIES', 8000.00),
    (1, N'419.70', N'COMMUNITY CENTER SALARIES', 0.00),
    (1, N'420.00', N'CONTRACT LABOR', 250.00),
    (1, N'441.00', N'HEALTH FAIR', 0.00),
    (1, N'453.00', N'ELECTIONS: JUDGES', 150.00),
    (1, N'454.00', N'ELECTIONS: PUBLICATIONS', 60.00),
    (1, N'455.00', N'ELECTIONS: SUPPLIES', 600.00),
    (1, N'456.00', N'MOSQUITO SPRAYING', 200.00),
    (1, N'459.00', N'FIRE DEPT PMT', 250.00),
    (1, N'460.10', N'COMMUNITY SERVICE', 0.00),
    (1, N'461.00', N'PUBLICATIONS/ADV', 100.00),
    (1, N'462.00', N'INTEREST & PENALTIES', 0.00),
    (1, N'465.00', N'CHRISTMAS LIGHTING CONTEST', 150.00),
    (1, N'480.00', N'DONATIONS', 500.00),
    (1, N'481.00', N'FEMA FLOOD MAPPING', 0.00),
    (1, N'483.00', N'COVID RELIEF FUNDS', 0.00),
    (1, N'485.20', N'MISCELLANEOUS', 50.00),
    (1, N'485.30', N'GOCO CONSULT EXP', 0.00),
    (1, N'485.40', N'MEALS', 75.00),
    (1, N'492.00', N'PUBLIC SAFETY', 0.00),
    (2, N'412.50', N'BROOKSIDE DRIVE DEVELOPMENT', 0.00),
    (2, N'431.00', N'SUPPLIES/REPAIRS', 1500.00),
    (2, N'431.10', N'SNOW AND ICE REMOVAL', 0.00),
    (2, N'431.40', N'SALARIES', 45000.00),
    (2, N'431.41', N'SALARIES PARTTIME', 48000.00),
    (2, N'431.50', N'GRAVEL', 6000.00),
    (2, N'431.60', N'SEAL COATING', 2000.00),
    (2, N'431.61', N'STREET SIGNS', 1000.00),
    (2, N'432.20', N'REPAIRS/MAINT: EQUIP', 4200.00),
    (2, N'432.30', N'STREET LIGHTING', 12000.00),
    (2, N'432.40', N'FUEL', 2800.00),
    (2, N'432.51', N'EQUIPMENT', 20000.00),
    (2, N'432.99', N'STREETS (PLACEHOLDER FROM UNCODED SOURCE LINE)', 40000.00),
    (2, N'433.00', N'SHOP SUPPLIES', 12000.00),
    (3, N'451.10', N'SALARIES', 4000.00),
    (3, N'451.20', N'SALARIES PART TIME', 0.00),
    (3, N'452.00', N'SUPPLIES/REPAIRS', 2500.00),
    (3, N'452.10', N'UTILITES', 550.00);

    DECLARE @Prepared TABLE (
        DepartmentId int NOT NULL,
        AccountNumber nvarchar(20) NOT NULL,
        Description nvarchar(200) NOT NULL,
        BudgetAmount decimal(18,2) NOT NULL,
        AccountType int NOT NULL,
        TypeDescription nvarchar(50) NOT NULL
    );

    INSERT INTO @Prepared (DepartmentId, AccountNumber, Description, BudgetAmount, AccountType, TypeDescription)
    SELECT
        s.DepartmentId,
        s.AccountNumber,
        s.Description,
        s.BudgetAmount,
        CASE
            WHEN s.AccountNumber = N'372.20' THEN 30
            WHEN s.DepartmentId = 1 AND (s.AccountNumber LIKE N'31%' OR s.AccountNumber IN (N'334.00', N'336.00')) THEN 11
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'321.10', N'321.70') THEN 25
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'318.20', N'322.70', N'310.00', N'341.40', N'373.00', N'324.00') THEN 12
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'361.00', N'365.00') THEN 14
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'320.00', N'332.1', N'334.31', N'337.17', N'355.00') THEN 13
            WHEN s.DepartmentId = 1 AND s.AccountNumber < N'400' THEN 16
            WHEN s.AccountNumber IN (N'412.50', N'432.51', N'432.99') THEN 29
            WHEN s.Description LIKE N'%SALAR%' THEN 17
            WHEN s.Description LIKE N'%AUDIT%' OR s.Description LIKE N'%LEGAL%' OR s.Description LIKE N'%PROF%' OR s.Description LIKE N'%FLOOD MAPPING%' OR s.Description LIKE N'%CONSULT%' THEN 26
            WHEN s.Description LIKE N'%CONTRACT LABOR%' THEN 27
            WHEN s.Description LIKE N'%DUES%' OR s.Description LIKE N'%SUBS%' THEN 28
            WHEN s.Description LIKE N'%INSURANCE%' THEN 22
            WHEN s.Description LIKE N'%UTILIT%' OR s.Description LIKE N'%LIGHTING%' THEN 20
            WHEN s.Description LIKE N'%REPAIR%' OR s.Description LIKE N'%MAINT%' OR s.Description LIKE N'%SNOW%' OR s.Description LIKE N'%SEAL%' THEN 21
            WHEN s.AccountNumber = N'415.19' OR s.Description LIKE N'%SUPPLIES%' OR s.Description LIKE N'%GRAVEL%' OR s.Description LIKE N'%SIGNS%' OR s.Description LIKE N'%POSTAGE%' THEN 18
            WHEN s.Description LIKE N'%BANK CHARGES%' OR s.Description LIKE N'%TREASURER%' OR s.Description LIKE N'%PUBLICATIONS%' OR s.Description LIKE N'%RECYCLING%' OR s.Description LIKE N'%PET CLINIC%' OR s.Description LIKE N'%SERVICE CONTRACT%' OR s.Description LIKE N'%MOSQUITO%' THEN 19
            ELSE 24
        END AS AccountType,
        CASE
            WHEN s.AccountNumber = N'372.20' THEN N'Transfers'
            WHEN s.DepartmentId = 1 AND (s.AccountNumber LIKE N'31%' OR s.AccountNumber IN (N'334.00', N'336.00')) THEN N'Taxes'
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'321.10', N'321.70') THEN N'PermitsAndAssessments'
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'318.20', N'322.70', N'310.00', N'341.40', N'373.00', N'324.00') THEN N'Fees'
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'361.00', N'365.00') THEN N'Interest'
            WHEN s.DepartmentId = 1 AND s.AccountNumber IN (N'320.00', N'332.1', N'334.31', N'337.17', N'355.00') THEN N'Grants'
            WHEN s.DepartmentId = 1 AND s.AccountNumber < N'400' THEN N'Revenue'
            WHEN s.AccountNumber IN (N'412.50', N'432.51', N'432.99') THEN N'CapitalOutlay'
            WHEN s.Description LIKE N'%SALAR%' THEN N'Salaries'
            WHEN s.Description LIKE N'%AUDIT%' OR s.Description LIKE N'%LEGAL%' OR s.Description LIKE N'%PROF%' OR s.Description LIKE N'%FLOOD MAPPING%' OR s.Description LIKE N'%CONSULT%' THEN N'ProfessionalServices'
            WHEN s.Description LIKE N'%CONTRACT LABOR%' THEN N'ContractLabor'
            WHEN s.Description LIKE N'%DUES%' OR s.Description LIKE N'%SUBS%' THEN N'DuesAndSubscriptions'
            WHEN s.Description LIKE N'%INSURANCE%' THEN N'Insurance'
            WHEN s.Description LIKE N'%UTILIT%' OR s.Description LIKE N'%LIGHTING%' THEN N'Utilities'
            WHEN s.Description LIKE N'%REPAIR%' OR s.Description LIKE N'%MAINT%' OR s.Description LIKE N'%SNOW%' OR s.Description LIKE N'%SEAL%' THEN N'Maintenance'
            WHEN s.AccountNumber = N'415.19' OR s.Description LIKE N'%SUPPLIES%' OR s.Description LIKE N'%GRAVEL%' OR s.Description LIKE N'%SIGNS%' OR s.Description LIKE N'%POSTAGE%' THEN N'Supplies'
            WHEN s.Description LIKE N'%BANK CHARGES%' OR s.Description LIKE N'%TREASURER%' OR s.Description LIKE N'%PUBLICATIONS%' OR s.Description LIKE N'%RECYCLING%' OR s.Description LIKE N'%PET CLINIC%' OR s.Description LIKE N'%SERVICE CONTRACT%' OR s.Description LIKE N'%MOSQUITO%' THEN N'Services'
            ELSE N'Expense'
        END AS TypeDescription
    FROM @Source s;

    MERGE dbo.MunicipalAccounts AS target
    USING @Prepared AS src
        ON target.AccountNumber = src.AccountNumber
       AND target.DepartmentId = src.DepartmentId
       AND target.FundId = @FundId
    WHEN MATCHED THEN
        UPDATE SET
            target.Name = LEFT(src.Description, 100),
            target.Type = src.AccountType,
            target.TypeDescription = src.TypeDescription,
            target.FundType = 0,
            target.FundDescription = @FundDescription,
            target.BudgetPeriodId = @BudgetPeriodId,
            target.BudgetAmount = src.BudgetAmount,
            target.Balance = COALESCE(target.Balance, 0),
            target.IsActive = 1
    WHEN NOT MATCHED THEN
        INSERT (DepartmentId, AccountNumber, BudgetPeriodId, Name, Type, TypeDescription, FundDescription, FundType, FundId, Balance, BudgetAmount, IsActive)
        VALUES (src.DepartmentId, src.AccountNumber, @BudgetPeriodId, LEFT(src.Description, 100), src.AccountType, src.TypeDescription, @FundDescription, 0, @FundId, 0, src.BudgetAmount, 1);

    MERGE dbo.BudgetEntries AS target
    USING (
        SELECT
            p.DepartmentId,
            p.AccountNumber,
            p.Description,
            p.BudgetAmount,
            ma.Id AS MunicipalAccountId
        FROM @Prepared p
        INNER JOIN dbo.MunicipalAccounts ma
            ON ma.AccountNumber = p.AccountNumber
           AND ma.DepartmentId = p.DepartmentId
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
            target.DepartmentId = src.DepartmentId,
            target.FundId = @FundId,
            target.FundType = 1,
            target.MunicipalAccountId = src.MunicipalAccountId,
            target.StartPeriod = @StartDate,
            target.EndPeriod = @EndDate,
            target.IsGASBCompliant = 1,
            target.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (AccountNumber, Description, BudgetedAmount, ActualAmount, Variance, FiscalYear, StartPeriod, EndPeriod, FundType, DepartmentId, FundId, MunicipalAccountId, EncumbranceAmount, IsGASBCompliant, CreatedAt, UpdatedAt)
        VALUES (src.AccountNumber, LEFT(src.Description, 200), src.BudgetAmount, 0, src.BudgetAmount, @FiscalYear, @StartDate, @EndDate, 1, src.DepartmentId, @FundId, src.MunicipalAccountId, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
