-- ============================================
-- Town of Wiley - Chart of Accounts Seed Data
-- Generated from Chart of Accounts PDF dated 10/9/2025
-- ============================================

-- Create table for Funds if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Funds')
BEGIN
    CREATE TABLE Funds (
        FundId INT IDENTITY(1,1) PRIMARY KEY,
        FundCode NVARCHAR(50) NOT NULL UNIQUE,
        FundName NVARCHAR(255) NOT NULL,
        Description NVARCHAR(500),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
    );
END
GO

-- Create table for Account Types if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountTypes')
BEGIN
    CREATE TABLE AccountTypes (
        AccountTypeId INT IDENTITY(1,1) PRIMARY KEY,
        TypeName NVARCHAR(50) NOT NULL UNIQUE,
        Description NVARCHAR(255),
        IsDebit BIT NOT NULL -- True for Asset/Expense, False for Liability/Equity/Income
    );
END
GO

-- Create table for Chart of Accounts if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChartOfAccounts')
BEGIN
    CREATE TABLE ChartOfAccounts (
        AccountId INT IDENTITY(1,1) PRIMARY KEY,
        AccountNumber NVARCHAR(50) NOT NULL,
        AccountName NVARCHAR(255) NOT NULL,
        FundId INT NOT NULL,
        AccountTypeId INT NOT NULL,
        ParentAccountId INT NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ChartOfAccounts_Fund FOREIGN KEY (FundId) REFERENCES Funds(FundId),
        CONSTRAINT FK_ChartOfAccounts_AccountType FOREIGN KEY (AccountTypeId) REFERENCES AccountTypes(AccountTypeId),
        CONSTRAINT FK_ChartOfAccounts_Parent FOREIGN KEY (ParentAccountId) REFERENCES ChartOfAccounts(AccountId),
        CONSTRAINT UQ_ChartOfAccounts_Number_Fund UNIQUE (AccountNumber, FundId)
    );
END
GO

-- ============================================
-- Seed Account Types
-- ============================================
PRINT 'Seeding Account Types...';

MERGE INTO AccountTypes AS target
USING (VALUES
    (0, 'Revenue and income accounts', 0),
    (1, 'Expenditure and expense accounts', 1),
    ('Asset', 'Asset accounts', 1),
    ('Liability', 'Liability accounts', 0),
    ('Equity', 'Equity and fund balance accounts', 0),
    ('Cost of Goods Sold', 'Cost of goods sold accounts', 1),
    ('Bank', 'Bank and cash accounts', 1),
    ('Accounts Receivable', 'Accounts receivable', 1),
    ('Other Current Asset', 'Other current assets', 1),
    ('Fixed Asset', 'Fixed assets', 1),
    ('Other Asset', 'Other assets', 1),
    ('Accounts Payable', 'Accounts payable', 0),
    ('Credit Card', 'Credit card accounts', 0),
    ('Other Current Liability', 'Other current liabilities', 0),
    ('Long Term Liability', 'Long term liabilities', 0)
) AS source (TypeName, Description, IsDebit)
ON target.TypeName = source.TypeName
WHEN NOT MATCHED THEN
    INSERT (TypeName, Description, IsDebit)
    VALUES (source.TypeName, source.Description, source.IsDebit);
GO

-- ============================================
-- Seed Funds
-- ============================================
PRINT 'Seeding Funds...';

MERGE INTO Funds AS target
USING (VALUES
    ('TOWN-GENERAL', 'Town General Fund', 'Town of Wiley General Operating Fund'),
    ('WILEY-REC', 'Wiley Rec', 'Wiley Recreation Fund'),
    ('CONSERV-TRUST', 'Conservation Trust Fund', 'Conservation Trust Fund'),
    ('WSD-GENERAL', 'WSD General', 'Wiley Sanitation District - General Fund'),
    ('WILEY-CC', 'Wiley Community Center', 'Wiley Community Center Fund'),
    ('WHA-BROOKSIDE', 'WHA Brookside', 'Wiley Housing Authority - Brookside')
) AS source (FundCode, FundName, Description)
ON target.FundCode = source.FundCode
WHEN NOT MATCHED THEN
    INSERT (FundCode, FundName, Description)
    VALUES (source.FundCode, source.FundName, source.Description);
GO

-- ============================================
-- Seed Chart of Accounts - Town General Fund
-- ============================================
PRINT 'Seeding Town General Fund accounts...';

DECLARE @TownGeneralFundId INT;
SELECT @TownGeneralFundId = FundId FROM Funds WHERE FundCode = 'TOWN-GENERAL';

DECLARE @IncomeTypeId INT, @ExpenseTypeId INT, @COGSTypeId INT;
SELECT @IncomeTypeId = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';
SELECT @COGSTypeId = AccountTypeId FROM AccountTypes WHERE TypeName = 'Cost of Goods Sold';

-- Town General Fund - Income Accounts
MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts from pages 11-12
    ('300', 'GENERAL REVENUES', @TownGeneralFundId, @IncomeTypeId),
    ('301', 'PROPERTY TAX', @TownGeneralFundId, @IncomeTypeId),
    ('302', 'SPECIFIC OWNERSHIP TAX', @TownGeneralFundId, @IncomeTypeId),
    ('303', 'MOTOR VEHICLE FEES', @TownGeneralFundId, @IncomeTypeId),
    ('304', 'SALES TAX', @TownGeneralFundId, @IncomeTypeId),
    ('305', 'COUNTY ROAD & BRIDGE', @TownGeneralFundId, @IncomeTypeId),
    ('306', 'SEVERANCE TAX', @TownGeneralFundId, @IncomeTypeId),
    ('307', 'FRANCHISE FEE REVENUE', @TownGeneralFundId, @IncomeTypeId),
    ('308', 'LOTTERY PROCEEDS', @TownGeneralFundId, @IncomeTypeId),
    ('309', 'CIGARETTE TAX', @TownGeneralFundId, @IncomeTypeId),
    ('310', 'TRAILER REG FEES', @TownGeneralFundId, @IncomeTypeId),
    ('311', 'MINERAL LEASING', @TownGeneralFundId, @IncomeTypeId),
    ('312', 'HWY USERS TAX', @TownGeneralFundId, @IncomeTypeId),
    ('315', 'INTEREST REVENUE', @TownGeneralFundId, @IncomeTypeId),
    ('316', 'LIBRARY INCOME', @TownGeneralFundId, @IncomeTypeId),
    ('317', 'BUILDING PERMITS', @TownGeneralFundId, @IncomeTypeId),
    ('318', 'ANIMAL IMPOUND FEES', @TownGeneralFundId, @IncomeTypeId),
    ('319', 'BUSINESS LICENSES', @TownGeneralFundId, @IncomeTypeId),
    ('320', 'MISCELLANEOUS', @TownGeneralFundId, @IncomeTypeId),
    ('321', 'FINES', @TownGeneralFundId, @IncomeTypeId),
    ('322', 'COURT COSTS', @TownGeneralFundId, @IncomeTypeId),
    ('325', 'OTHER INCOME', @TownGeneralFundId, @IncomeTypeId),
    ('326', 'DOLA GRANT INCOME', @TownGeneralFundId, @IncomeTypeId),
    ('327', 'DONATIONS', @TownGeneralFundId, @IncomeTypeId),
    ('328', 'INSURANCE INCOME', @TownGeneralFundId, @IncomeTypeId),
    ('329', 'ARPA GRANT INCOME', @TownGeneralFundId, @IncomeTypeId),
    ('330', 'TRANSFER IN SEWER', @TownGeneralFundId, @IncomeTypeId),
    ('331', 'TRANSFER IN WATER', @TownGeneralFundId, @IncomeTypeId),
    ('332', 'TRANSFER IN CC FUND', @TownGeneralFundId, @IncomeTypeId),
    ('333', 'OTHER GRANT INCOME', @TownGeneralFundId, @IncomeTypeId),
    
    -- Expense accounts from pages 12-14
    ('400', 'ADMINISTRATION', @TownGeneralFundId, @ExpenseTypeId),
    ('401', 'SALARIES EMPLOYEES', @TownGeneralFundId, @ExpenseTypeId),
    ('402', 'SALARIES OFFICIALS', @TownGeneralFundId, @ExpenseTypeId),
    ('403', 'PAYROLL TAXES', @TownGeneralFundId, @ExpenseTypeId),
    ('404', 'RETIREMENT', @TownGeneralFundId, @ExpenseTypeId),
    ('405', 'INSURANCE', @TownGeneralFundId, @ExpenseTypeId),
    ('406', 'WORKERS COMP', @TownGeneralFundId, @ExpenseTypeId),
    ('407', 'UNEMPLOYMENT EXPENSE', @TownGeneralFundId, @ExpenseTypeId),
    ('408', 'DUES SUBSCRIPTIONS', @TownGeneralFundId, @ExpenseTypeId),
    ('409', 'PROFESSIONAL SERVICES', @TownGeneralFundId, @ExpenseTypeId),
    ('410', 'REPAIRS MAINTENANCE BLDGS', @TownGeneralFundId, @ExpenseTypeId),
    ('411', 'REPAIRS MAINTENANCE EQUIPMENT', @TownGeneralFundId, @ExpenseTypeId),
    ('412', 'REPAIRS MAINTENANCE STREETS', @TownGeneralFundId, @ExpenseTypeId),
    ('413', 'REPAIRS MAINTENANCE VEHICLES', @TownGeneralFundId, @ExpenseTypeId),
    ('414', 'REPAIRS MAINTENANCE PARK', @TownGeneralFundId, @ExpenseTypeId),
    ('415', 'REPAIRS MAINTENANCE OTHER', @TownGeneralFundId, @ExpenseTypeId),
    ('420', 'OFFICE SUPPLIES', @TownGeneralFundId, @ExpenseTypeId),
    ('421', 'PRINTING PUBLICATIONS', @TownGeneralFundId, @ExpenseTypeId),
    ('422', 'UTILITIES', @TownGeneralFundId, @ExpenseTypeId),
    ('423', 'TELEPHONE', @TownGeneralFundId, @ExpenseTypeId),
    ('424', 'POSTAGE', @TownGeneralFundId, @ExpenseTypeId),
    ('425', 'FUEL', @TownGeneralFundId, @ExpenseTypeId),
    ('426', 'TRAVEL TRAINING', @TownGeneralFundId, @ExpenseTypeId),
    ('427', 'EQUIPMENT', @TownGeneralFundId, @ExpenseTypeId),
    ('428', 'COMPUTER EXPENSE', @TownGeneralFundId, @ExpenseTypeId),
    ('430', 'LEGAL FEES', @TownGeneralFundId, @ExpenseTypeId),
    ('431', 'AUDIT FEES', @TownGeneralFundId, @ExpenseTypeId),
    ('432', 'ENGINEERING FEES', @TownGeneralFundId, @ExpenseTypeId),
    ('433', 'COLLECTION FEES', @TownGeneralFundId, @ExpenseTypeId),
    ('440', 'STREET SUPPLIES', @TownGeneralFundId, @ExpenseTypeId),
    ('441', 'STREET LIGHTING', @TownGeneralFundId, @ExpenseTypeId),
    ('442', 'SNOW REMOVAL', @TownGeneralFundId, @ExpenseTypeId),
    ('443', 'TRAFFIC CONTROL', @TownGeneralFundId, @ExpenseTypeId),
    ('450', 'ANIMAL CONTROL', @TownGeneralFundId, @ExpenseTypeId),
    ('451', 'LAW ENFORCEMENT', @TownGeneralFundId, @ExpenseTypeId),
    ('452', 'FIRE PROTECTION', @TownGeneralFundId, @ExpenseTypeId),
    ('453', 'AMBULANCE SERVICE', @TownGeneralFundId, @ExpenseTypeId),
    ('460', 'LIBRARY EXPENSE', @TownGeneralFundId, @ExpenseTypeId),
    ('461', 'PUBLICATIONS/ADVERTISING', @TownGeneralFundId, @ExpenseTypeId),
    ('462', 'INT/PENALTIES W/H TAXES', @TownGeneralFundId, @ExpenseTypeId),
    ('465', 'CHRISTMAS LIGHTING CONTEST', @TownGeneralFundId, @ExpenseTypeId),
    ('480', 'DONATIONS EXPENSE', @TownGeneralFundId, @ExpenseTypeId),
    ('481', 'FEMA', @TownGeneralFundId, @ExpenseTypeId),
    ('483', 'COVID RELIEF EXPENSES', @TownGeneralFundId, @ExpenseTypeId),
    ('484', 'GOCO CONSULT EXPENSES', @TownGeneralFundId, @ExpenseTypeId),
    ('485.2', 'MISC EXPENSE', @TownGeneralFundId, @ExpenseTypeId),
    ('485.3', 'COURTESY', @TownGeneralFundId, @ExpenseTypeId),
    ('485.4', 'MEALS', @TownGeneralFundId, @ExpenseTypeId),
    ('486', 'TRANSFER TO CAPITAL PROJECTS', @TownGeneralFundId, @ExpenseTypeId),
    ('490', 'TRASFER TO UTILITY FUNDS', @TownGeneralFundId, @ExpenseTypeId),
    ('491', 'TRANSFER TO CC FUND', @TownGeneralFundId, @ExpenseTypeId),
    ('492', 'PUBLIC SAFETY (EXP)', @TownGeneralFundId, @ExpenseTypeId),
    ('495', 'CAPITAL OUTLAY', @TownGeneralFundId, @ExpenseTypeId),
    ('49900', 'Uncategorized Income', @TownGeneralFundId, @IncomeTypeId),
    ('50000', 'Cost of Goods Sold', @TownGeneralFundId, @COGSTypeId),
    ('66000', 'Payroll Expenses', @TownGeneralFundId, @ExpenseTypeId)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Seed Chart of Accounts - Wiley Rec Fund
-- ============================================
PRINT 'Seeding Wiley Rec Fund accounts...';

DECLARE @WileyRecFundId INT;
SELECT @WileyRecFundId = FundId FROM Funds WHERE FundCode = 'WILEY-REC';

DECLARE @IncomeTypeId2 INT, @ExpenseTypeId2 INT;
SELECT @IncomeTypeId2 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId2 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';

MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts
    ('300', 'GENERAL REVENUES', @WileyRecFundId, @IncomeTypeId2),
    ('315', 'INTEREST REVENUE', @WileyRecFundId, @IncomeTypeId2),
    ('320', 'MISCELLANEOUS', @WileyRecFundId, @IncomeTypeId2),
    ('325', 'OTHER INCOME', @WileyRecFundId, @IncomeTypeId2),
    ('327', 'DONATIONS', @WileyRecFundId, @IncomeTypeId2),
    ('332', 'TRANSFER IN GENERAL FUND', @WileyRecFundId, @IncomeTypeId2),
    
    -- Expense accounts
    ('400', 'ADMINISTRATION', @WileyRecFundId, @ExpenseTypeId2),
    ('410', 'REPAIRS MAINTENANCE BLDGS', @WileyRecFundId, @ExpenseTypeId2),
    ('414', 'REPAIRS MAINTENANCE PARK', @WileyRecFundId, @ExpenseTypeId2),
    ('422', 'UTILITIES', @WileyRecFundId, @ExpenseTypeId2),
    ('485.2', 'MISC EXPENSE', @WileyRecFundId, @ExpenseTypeId2),
    ('495', 'CAPITAL OUTLAY', @WileyRecFundId, @ExpenseTypeId2)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Seed Chart of Accounts - Conservation Trust Fund
-- ============================================
PRINT 'Seeding Conservation Trust Fund accounts...';

DECLARE @ConservTrustFundId INT;
SELECT @ConservTrustFundId = FundId FROM Funds WHERE FundCode = 'CONSERV-TRUST';

DECLARE @IncomeTypeId3 INT, @ExpenseTypeId3 INT;
SELECT @IncomeTypeId3 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId3 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';

MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts
    ('300', 'GENERAL REVENUES', @ConservTrustFundId, @IncomeTypeId3),
    ('308', 'LOTTERY PROCEEDS', @ConservTrustFundId, @IncomeTypeId3),
    ('315', 'INTEREST REVENUE', @ConservTrustFundId, @IncomeTypeId3),
    ('320', 'MISCELLANEOUS', @ConservTrustFundId, @IncomeTypeId3),
    
    -- Expense accounts
    ('414', 'REPAIRS MAINTENANCE PARK', @ConservTrustFundId, @ExpenseTypeId3),
    ('485.2', 'MISC EXPENSE', @ConservTrustFundId, @ExpenseTypeId3),
    ('495', 'CAPITAL OUTLAY', @ConservTrustFundId, @ExpenseTypeId3)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Seed Chart of Accounts - WSD General Fund
-- ============================================
PRINT 'Seeding WSD General Fund accounts...';

DECLARE @WSDGeneralFundId INT;
SELECT @WSDGeneralFundId = FundId FROM Funds WHERE FundCode = 'WSD-GENERAL';

DECLARE @IncomeTypeId4 INT, @ExpenseTypeId4 INT;
SELECT @IncomeTypeId4 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId4 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';

MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts
    ('300', 'GENERAL REVENUES', @WSDGeneralFundId, @IncomeTypeId4),
    ('301', 'PROPERTY TAX', @WSDGeneralFundId, @IncomeTypeId4),
    ('302', 'SPECIFIC OWNERSHIP TAX', @WSDGeneralFundId, @IncomeTypeId4),
    ('315', 'INTEREST REVENUE', @WSDGeneralFundId, @IncomeTypeId4),
    ('320', 'MISCELLANEOUS', @WSDGeneralFundId, @IncomeTypeId4),
    
    -- Expense accounts
    ('400', 'ADMINISTRATION', @WSDGeneralFundId, @ExpenseTypeId4),
    ('405', 'INSURANCE', @WSDGeneralFundId, @ExpenseTypeId4),
    ('409', 'PROFESSIONAL SERVICES', @WSDGeneralFundId, @ExpenseTypeId4),
    ('431', 'AUDIT FEES', @WSDGeneralFundId, @ExpenseTypeId4),
    ('485.2', 'MISC EXPENSE', @WSDGeneralFundId, @ExpenseTypeId4),
    ('495', 'CAPITAL OUTLAY', @WSDGeneralFundId, @ExpenseTypeId4)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Seed Chart of Accounts - Wiley Community Center
-- ============================================
PRINT 'Seeding Wiley Community Center accounts...';

DECLARE @WileyCCFundId INT;
SELECT @WileyCCFundId = FundId FROM Funds WHERE FundCode = 'WILEY-CC';

DECLARE @IncomeTypeId5 INT, @ExpenseTypeId5 INT;
SELECT @IncomeTypeId5 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId5 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';

MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts
    ('300', 'GENERAL REVENUES', @WileyCCFundId, @IncomeTypeId5),
    ('315', 'INTEREST REVENUE', @WileyCCFundId, @IncomeTypeId5),
    ('320', 'MISCELLANEOUS', @WileyCCFundId, @IncomeTypeId5),
    ('325', 'OTHER INCOME', @WileyCCFundId, @IncomeTypeId5),
    ('326', 'DOLA GRANT INCOME', @WileyCCFundId, @IncomeTypeId5),
    ('327', 'DONATIONS', @WileyCCFundId, @IncomeTypeId5),
    ('332', 'TRANSFER IN GENERAL FUND', @WileyCCFundId, @IncomeTypeId5),
    ('333', 'OTHER GRANT INCOME', @WileyCCFundId, @IncomeTypeId5),
    ('334', 'RENTAL INCOME', @WileyCCFundId, @IncomeTypeId5),
    
    -- Expense accounts
    ('400', 'ADMINISTRATION', @WileyCCFundId, @ExpenseTypeId5),
    ('401', 'SALARIES EMPLOYEES', @WileyCCFundId, @ExpenseTypeId5),
    ('403', 'PAYROLL TAXES', @WileyCCFundId, @ExpenseTypeId5),
    ('405', 'INSURANCE', @WileyCCFundId, @ExpenseTypeId5),
    ('406', 'WORKERS COMP', @WileyCCFundId, @ExpenseTypeId5),
    ('409', 'PROFESSIONAL SERVICES', @WileyCCFundId, @ExpenseTypeId5),
    ('410', 'REPAIRS MAINTENANCE BLDGS', @WileyCCFundId, @ExpenseTypeId5),
    ('411', 'REPAIRS MAINTENANCE EQUIPMENT', @WileyCCFundId, @ExpenseTypeId5),
    ('420', 'OFFICE SUPPLIES', @WileyCCFundId, @ExpenseTypeId5),
    ('422', 'UTILITIES', @WileyCCFundId, @ExpenseTypeId5),
    ('423', 'TELEPHONE', @WileyCCFundId, @ExpenseTypeId5),
    ('427', 'EQUIPMENT', @WileyCCFundId, @ExpenseTypeId5),
    ('428', 'COMPUTER EXPENSE', @WileyCCFundId, @ExpenseTypeId5),
    ('431', 'AUDIT FEES', @WileyCCFundId, @ExpenseTypeId5),
    ('485.2', 'MISC EXPENSE', @WileyCCFundId, @ExpenseTypeId5),
    ('491', 'TRANSFER TO GENERAL FUND', @WileyCCFundId, @ExpenseTypeId5),
    ('495', 'CAPITAL OUTLAY', @WileyCCFundId, @ExpenseTypeId5)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Seed Chart of Accounts - WHA Brookside
-- ============================================
PRINT 'Seeding WHA Brookside accounts...';

DECLARE @WHABrooksideFundId INT;
SELECT @WHABrooksideFundId = FundId FROM Funds WHERE FundCode = 'WHA-BROOKSIDE';

DECLARE @IncomeTypeId6 INT, @ExpenseTypeId6 INT;
SELECT @IncomeTypeId6 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Income';
SELECT @ExpenseTypeId6 = AccountTypeId FROM AccountTypes WHERE TypeName = 'Expense';

MERGE INTO ChartOfAccounts AS target
USING (VALUES
    -- Income accounts
    ('300', 'GENERAL REVENUES', @WHABrooksideFundId, @IncomeTypeId6),
    ('315', 'INTEREST REVENUE', @WHABrooksideFundId, @IncomeTypeId6),
    ('320', 'MISCELLANEOUS', @WHABrooksideFundId, @IncomeTypeId6),
    ('325', 'OTHER INCOME', @WHABrooksideFundId, @IncomeTypeId6),
    ('327', 'DONATIONS', @WHABrooksideFundId, @IncomeTypeId6),
    ('334', 'RENTAL INCOME', @WHABrooksideFundId, @IncomeTypeId6),
    ('335', 'HUD SUBSIDY', @WHABrooksideFundId, @IncomeTypeId6),
    
    -- Expense accounts
    ('400', 'ADMINISTRATION', @WHABrooksideFundId, @ExpenseTypeId6),
    ('401', 'SALARIES EMPLOYEES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('403', 'PAYROLL TAXES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('404', 'RETIREMENT', @WHABrooksideFundId, @ExpenseTypeId6),
    ('405', 'INSURANCE', @WHABrooksideFundId, @ExpenseTypeId6),
    ('406', 'WORKERS COMP', @WHABrooksideFundId, @ExpenseTypeId6),
    ('408', 'DUES SUBSCRIPTIONS', @WHABrooksideFundId, @ExpenseTypeId6),
    ('409', 'PROFESSIONAL SERVICES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('410', 'REPAIRS MAINTENANCE BLDGS', @WHABrooksideFundId, @ExpenseTypeId6),
    ('411', 'REPAIRS MAINTENANCE EQUIPMENT', @WHABrooksideFundId, @ExpenseTypeId6),
    ('415', 'REPAIRS MAINTENANCE OTHER', @WHABrooksideFundId, @ExpenseTypeId6),
    ('420', 'OFFICE SUPPLIES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('421', 'PRINTING PUBLICATIONS', @WHABrooksideFundId, @ExpenseTypeId6),
    ('422', 'UTILITIES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('423', 'TELEPHONE', @WHABrooksideFundId, @ExpenseTypeId6),
    ('424', 'POSTAGE', @WHABrooksideFundId, @ExpenseTypeId6),
    ('426', 'TRAVEL TRAINING', @WHABrooksideFundId, @ExpenseTypeId6),
    ('427', 'EQUIPMENT', @WHABrooksideFundId, @ExpenseTypeId6),
    ('428', 'COMPUTER EXPENSE', @WHABrooksideFundId, @ExpenseTypeId6),
    ('431', 'AUDIT FEES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('433', 'COLLECTION FEES', @WHABrooksideFundId, @ExpenseTypeId6),
    ('485.2', 'MISC EXPENSE', @WHABrooksideFundId, @ExpenseTypeId6),
    ('495', 'CAPITAL OUTLAY', @WHABrooksideFundId, @ExpenseTypeId6)
) AS source (AccountNumber, AccountName, FundId, AccountTypeId)
ON target.AccountNumber = source.AccountNumber AND target.FundId = source.FundId
WHEN NOT MATCHED THEN
    INSERT (AccountNumber, AccountName, FundId, AccountTypeId)
    VALUES (source.AccountNumber, source.AccountName, source.FundId, source.AccountTypeId);
GO

-- ============================================
-- Verification Query
-- ============================================
PRINT 'Chart of Accounts seed complete. Summary:';
PRINT '';

SELECT 
    f.FundName,
    COUNT(*) AS AccountCount,
    SUM(CASE WHEN at.TypeName = 'Income' THEN 1 ELSE 0 END) AS IncomeAccounts,
    SUM(CASE WHEN at.TypeName = 'Expense' THEN 1 ELSE 0 END) AS ExpenseAccounts,
    SUM(CASE WHEN at.TypeName NOT IN (0, 1) THEN 1 ELSE 0 END) AS OtherAccounts
FROM ChartOfAccounts coa
JOIN Funds f ON coa.FundId = f.FundId
JOIN AccountTypes at ON coa.AccountTypeId = at.AccountTypeId
GROUP BY f.FundName
ORDER BY f.FundName;

PRINT '';
PRINT 'Total accounts seeded:';
SELECT COUNT(*) AS TotalAccounts FROM ChartOfAccounts;
GO
