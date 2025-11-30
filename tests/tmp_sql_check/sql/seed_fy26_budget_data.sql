-- ============================================
-- Town of Wiley & WSD - FY 2026 Budget Seed Data
-- Generated from Chart of Accounts PDF (10/9/2025)
-- and Budget Excel files (TOW & WSD 2026 BUDGET.xls)
-- ============================================
-- Database: WileyWidget
-- Generated: November 2025
-- ============================================

USE WileyWidget;
GO

-- ============================================
-- SEED BUDGET PERIOD
-- ============================================
SET IDENTITY_INSERT BudgetPeriods ON;

MERGE INTO BudgetPeriods AS target
USING (VALUES
    (1, 'FY2026', '2026-01-01', '2026-12-31', 1)
) AS source (Id, Name, StartDate, EndDate, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, StartDate, EndDate, IsActive)
    VALUES (source.Id, source.Name, source.StartDate, source.EndDate, source.IsActive)
WHEN MATCHED THEN
    UPDATE SET Name = source.Name, StartDate = source.StartDate, EndDate = source.EndDate, IsActive = source.IsActive;

SET IDENTITY_INSERT BudgetPeriods OFF;
GO

-- ============================================
-- SEED DEPARTMENTS
-- ============================================
SET IDENTITY_INSERT Departments ON;

MERGE INTO Departments AS target
USING (VALUES
    (1, 12, 'Sewer Enterprise', 4, NULL),
    (2, 11, 'Water Enterprise', 4, NULL),
    (3, 'GENGOV', 'General Government', 0, NULL),
    (4, 'HWYST', 'Highways & Streets', 0, NULL),
    (5, 9, 'Culture & Recreation', 0, NULL),
    (6, 10, 'Utility Fund', 4, NULL),
    (7, 'COMCENTER', 'Community Center', 0, NULL),
    (8, 'RECFUND', 'Recreation Fund', 0, NULL),
    (9, 'CONSERV', 'Conservation Trust', 0, NULL)
) AS source (Id, Code, Name, Fund, ParentDepartmentId)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, Fund, ParentDepartmentId)
    VALUES (source.Id, source.Code, source.Name, source.Fund, source.ParentDepartmentId)
WHEN MATCHED THEN
    UPDATE SET Code = source.Code, Name = source.Name, Fund = source.Fund;

SET IDENTITY_INSERT Departments OFF;
GO

-- ============================================
-- SEED ENTERPRISES
-- ============================================
SET IDENTITY_INSERT Enterprises ON;

MERGE INTO Enterprises AS target
USING (VALUES
    (1, 'Sewer Enterprise', 'Town of Wiley Sewer Services - proprietary fund for sewage collection and treatment', 42.50, 8500.00, 455, 5778145.00, 5778145.00, 4, 'FY 2026 sewer enterprise fund', 1),
    (2, 'Water Enterprise', 'Town of Wiley Water Services - proprietary fund for water supply and distribution', 38.00, 7200.00, 455, 79365.00, 79365.00, 4, 'FY 2026 water enterprise fund', 1)
) AS source (Id, Name, Description, CurrentRate, MonthlyExpenses, CitizenCount, TotalBudget, BudgetAmount, Type, Notes, Status)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Description, CurrentRate, MonthlyExpenses, CitizenCount, TotalBudget, BudgetAmount, LastModified, Type, Notes, Status, MeterReading, MeterReadDate, PreviousMeterReading, PreviousMeterReadDate, CreatedDate, ModifiedDate, CreatedBy, ModifiedBy, IsDeleted)
    VALUES (source.Id, source.Name, source.Description, source.CurrentRate, source.MonthlyExpenses, source.CitizenCount, source.TotalBudget, source.BudgetAmount, GETDATE(), source.Type, source.Notes, source.Status, 0, GETDATE(), 0, GETDATE(), GETDATE(), GETDATE(), 'system', 'system', 0);

SET IDENTITY_INSERT Enterprises OFF;
GO

-- ============================================
-- SEED VENDORS
-- ============================================
SET IDENTITY_INSERT Vendors ON;

MERGE INTO Vendors AS target
USING (VALUES
    (1, 'Prowers County Landfill', 'Lamar, CO 81052', 1),
    (2, 'Colorado Correctional Industries', 'Canon City, CO', 1),
    (3, 'Xcel Energy', 'Denver, CO - Utility Provider', 1),
    (4, 'Black Hills Energy', 'Pueblo, CO - Gas Provider', 1),
    (5, 'Office Depot', 'Office Supplies', 1),
    (6, 'Lamar Fire Department', 'Lamar, CO - Fire Services Contract', 1),
    (7, 'Wiley Volunteer Fire Department', 'Wiley, CO', 1),
    (8, 'CIRSA', 'Denver, CO - Insurance', 1),
    (9, 'CML - Colorado Municipal League', 'Denver, CO - Dues', 1),
    (10, 'Southeast Colorado Mosquito District', 'Lamar, CO', 1),
    (11, 'Martin Marietta', 'Gravel & Materials', 1),
    (12, 'Yellowstone Paving', 'Seal Coating Services', 1),
    (13, 'Lamar Light & Power', 'Lamar, CO - Street Lighting', 1),
    (14, 'Wiley Equipment', 'Local Equipment Supplier', 1),
    (15, 'Sewer Solutions Inc', 'Sewer Cleaning Services', 1)
) AS source (Id, Name, ContactInfo, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ContactInfo, IsActive)
    VALUES (source.Id, source.Name, source.ContactInfo, source.IsActive);

SET IDENTITY_INSERT Vendors OFF;
GO

-- ============================================
-- SEED MUNICIPAL ACCOUNTS - FY 2026 BUDGET
-- ============================================
-- This section contains the complete Chart of Accounts
-- with FY2026 Budget amounts from the Excel budget files.

SET IDENTITY_INSERT MunicipalAccounts ON;

-- ============================================
-- GENERAL GOVERNMENT - ADMINISTRATION EXPENDITURES
-- Total Budget: $142,618
-- ============================================
PRINT 'Seeding General Government Administration accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    -- Admin Salaries & Benefits
    (201, 3, 1, '413.2', 'Governmental', 'Admin Clerk Salaries', 1, 0, 0.00, 45000.00, 1),
    (202, 3, 1, '413.21', 'Governmental', 'Admin Deputy Clerk Salaries', 1, 0, 0.00, 7500.00, 1),
    (203, 3, 1, '413.22', 'Governmental', 'Admin Code Enforcement', 1, 0, 0.00, 2000.00, 1),
    (204, 3, 1, '413.3', 'Governmental', 'Payroll Taxes - FICA', 1, 0, 0.00, 10575.00, 1),
    (205, 3, 1, '413.4', 'Governmental', 'Education', 1, 0, 0.00, 500.00, 1),
    (206, 3, 1, '413.5', 'Governmental', 'Travel & Transportation', 1, 0, 0.00, 150.00, 1),
    (207, 3, 1, '413.6', 'Governmental', 'Payroll Taxes - Unemployment', 1, 0, 0.00, 360.00, 1),
    (208, 3, 1, '413.7', 'Governmental', 'FAMLI', 1, 0, 0.00, 900.00, 1),
    (209, 3, 1, '413.8', 'Governmental', 'Simple IRA', 1, 0, 0.00, 5000.00, 1),
    -- Professional Services & Office
    (210, 3, 1, '415.11', 'Governmental', 'Audit & Budget Expense', 1, 0, 0.00, 1000.00, 1),
    (211, 3, 1, '415.17', 'Governmental', 'Office Supplies', 1, 0, 0.00, 5000.00, 1),
    (212, 3, 1, '415.175', 'Governmental', 'County Tax Assessment', 1, 0, 0.00, 7.00, 1),
    (213, 3, 1, '415.18', 'Governmental', 'Postage', 1, 0, 0.00, 75.00, 1),
    (214, 3, 1, '415.19', 'Governmental', 'Office Equipment', 1, 0, 0.00, 1500.00, 1),
    (215, 3, 1, '415.2', 'Governmental', 'Legal', 1, 0, 0.00, 300.00, 1),
    (216, 3, 1, '415.21', 'Governmental', 'Administration Expenses', 1, 0, 0.00, 1100.00, 1),
    -- Insurance
    (217, 3, 1, '415.5', 'Governmental', 'Insurance Bldg', 1, 0, 0.00, 4020.00, 1),
    (218, 3, 1, '415.51', 'Governmental', 'Insurance Public Liab', 1, 0, 0.00, 4356.00, 1),
    (219, 3, 1, '415.52', 'Governmental', 'Insurance Workers Comp', 1, 0, 0.00, 2000.00, 1),
    (220, 3, 1, '415.53', 'Governmental', 'Employee Health Insurance', 1, 0, 0.00, 28000.00, 1),
    (221, 3, 1, '415.54', 'Governmental', 'Bank Charges', 1, 0, 0.00, 90.00, 1),
    -- Other Admin
    (222, 3, 1, '415.75', 'Governmental', 'Pet Clinic', 1, 0, 0.00, 100.00, 1),
    (223, 3, 1, '415.76', 'Governmental', 'Recycling', 1, 0, 0.00, 1700.00, 1),
    (224, 3, 1, '416', 'Governmental', 'Dues & Subs', 1, 0, 0.00, 5600.00, 1),
    (225, 3, 1, '417', 'Governmental', 'Treasurer''s Fees', 1, 0, 0.00, 1700.00, 1),
    (226, 3, 1, '419.42', 'Governmental', 'Building Maintenance', 1, 0, 0.00, 1000.00, 1),
    (227, 3, 1, '419.44', 'Governmental', 'Building Supplies/Maint', 1, 0, 0.00, 200.00, 1),
    (228, 3, 1, '419.5', 'Governmental', 'Building Utilities', 1, 0, 0.00, 8000.00, 1),
    (229, 3, 1, '420', 'Governmental', 'Contract Labor', 1, 0, 0.00, 250.00, 1),
    -- Elections
    (230, 3, 1, '453', 'Governmental', 'Elections: Judges', 1, 0, 0.00, 150.00, 1),
    (231, 3, 1, '454', 'Governmental', 'Elections: Publications', 1, 0, 0.00, 60.00, 1),
    (232, 3, 1, '455', 'Governmental', 'Elections: Supplies', 1, 0, 0.00, 600.00, 1),
    -- Other General Admin
    (233, 3, 1, '456', 'Governmental', 'Mosquito Spraying', 1, 0, 0.00, 200.00, 1),
    (234, 3, 1, '459', 'Governmental', 'Fire Dept Pmt', 1, 0, 0.00, 250.00, 1),
    (235, 3, 1, '461', 'Governmental', 'Publications/Adv', 1, 0, 0.00, 100.00, 1),
    (236, 3, 1, '465', 'Governmental', 'Christmas Lighting Contest', 1, 0, 0.00, 150.00, 1),
    (237, 3, 1, '480', 'Governmental', 'Donations', 1, 0, 0.00, 500.00, 1),
    (238, 3, 1, '485.2', 'Governmental', 'Miscellaneous', 1, 0, 0.00, 50.00, 1),
    (239, 3, 1, '485.3', 'Governmental', 'GOCO Consult Exp', 1, 0, 0.00, 0.00, 1),
    (240, 3, 1, '485.4', 'Governmental', 'Meals', 1, 0, 0.00, 75.00, 1),
    (241, 3, 1, '410', 'Governmental', 'Wiley Hay Days Exp', 1, 0, 0.00, 2500.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

-- ============================================
-- HIGHWAYS & STREETS - EXPENDITURES
-- Total Budget: $194,500
-- ============================================
PRINT 'Seeding Highways & Streets accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    (250, 4, 1, '431', 'Governmental', 'Supplies/Repairs', 1, 1, 0.00, 1500.00, 1),
    (251, 4, 1, '431.4', 'Governmental', 'Salaries', 1, 1, 0.00, 45000.00, 1),
    (252, 4, 1, '431.41', 'Governmental', 'Salaries Parttime', 1, 1, 0.00, 48000.00, 1),
    (253, 4, 1, '431.5', 'Governmental', 'Gravel', 1, 1, 0.00, 6000.00, 1),
    (254, 4, 1, '431.6', 'Governmental', 'Seal Coating', 1, 1, 0.00, 2000.00, 1),
    (255, 4, 1, '431.61', 'Governmental', 'Street Signs', 1, 1, 0.00, 1000.00, 1),
    (256, 4, 1, '432.2', 'Governmental', 'Repairs/Maint: Equip', 1, 1, 0.00, 4200.00, 1),
    (257, 4, 1, '432.3', 'Governmental', 'Street Lighting', 1, 1, 0.00, 12000.00, 1),
    (258, 4, 1, '432.4', 'Governmental', 'Fuel', 1, 1, 0.00, 2800.00, 1),
    (259, 4, 1, '433', 'Governmental', 'Shop Supplies', 1, 1, 0.00, 12000.00, 1),
    (260, 4, 1, '432.51', 'Governmental', 'Equipment', 1, 1, 0.00, 20000.00, 1),
    (261, 4, 1, 'STREETS', 'Governmental', 'Streets - Prowers County', 1, 1, 0.00, 40000.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

-- ============================================
-- CULTURE & RECREATION - EXPENDITURES
-- Total Budget: $7,050
-- ============================================
PRINT 'Seeding Culture & Recreation accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    (270, 5, 1, '451.1', 'Governmental', 'Salaries', 1, 9, 0.00, 4000.00, 1),
    (271, 5, 1, '452', 'Governmental', 'Supplies/Repairs', 1, 9, 0.00, 2500.00, 1),
    (272, 5, 1, '452.1', 'Governmental', 'Utilities', 1, 9, 0.00, 550.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

-- ============================================
-- UTILITY FUND - REVENUES & EXPENDITURES
-- Total Revenue Budget: $285,755
-- Total Expenditure Budget: $224,465
-- ============================================
PRINT 'Seeding Utility Fund accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    -- Utility Fund Revenues
    (300, 6, 1, 'WATER-SALES', 4, 'Water Sales', 0, 10, 0.00, 150000.00, 1),
    (301, 6, 1, 'WSD-COLLECT', 4, 'WSD Collection Fee', 0, 10, 0.00, 6000.00, 1),
    (302, 6, 1, 'TAP-FEES', 4, 'Tap Fees', 0, 10, 0.00, 1675.00, 1),
    (303, 6, 1, 'PENALTY', 4, 'Penalty', 0, 10, 0.00, 3000.00, 1),
    (304, 6, 1, 'INTEREST', 4, 'Interest', 0, 10, 0.00, 95.00, 1),
    (305, 6, 1, 'LEASE', 4, 'Lease', 0, 10, 0.00, 5485.00, 1),
    (306, 6, 1, 'TRASH-SALES', 4, 'Trash Sales', 0, 10, 0.00, 107500.00, 1),
    (307, 6, 1, 'BROOKSIDE-MGT', 4, 'Brookside Mgt Fee', 0, 10, 0.00, 12000.00, 1),
    -- Utility Fund Water Expenditures
    (310, 6, 1, '411', 4, 'Pumping Exp', 1, 10, 0.00, 15000.00, 1),
    (311, 6, 1, '412', 4, 'Supplies and Repairs', 1, 10, 0.00, 25000.00, 1),
    (312, 6, 1, '413', 4, 'Backhoe Repair/Maint', 1, 10, 0.00, 1200.00, 1),
    (313, 6, 1, '413.4', 4, 'Education/Travel', 1, 10, 0.00, 250.00, 1),
    (314, 6, 1, '415.11', 4, 'Audit & Budget Expense', 1, 10, 0.00, 1000.00, 1),
    (315, 6, 1, '416', 4, 'Dues and Subs', 1, 10, 0.00, 8500.00, 1),
    (316, 6, 1, '419.4', 4, 'Fuel', 1, 10, 0.00, 7500.00, 1),
    (317, 6, 1, '430', 4, 'Water Treatment', 1, 10, 0.00, 9000.00, 1),
    (318, 6, 1, '442', 4, 'Bldg Maint', 1, 10, 0.00, 100.00, 1),
    (319, 6, 1, '445', 4, 'Credit Card Fees', 1, 10, 0.00, 816.00, 1),
    (320, 6, 1, '487', 4, 'Interest USDA RD', 1, 10, 0.00, 10999.00, 1),
    -- Utility Fund Trash Expenditures
    (330, 6, 1, '453', 4, 'Trash: Supplies Repairs', 1, 10, 0.00, 25000.00, 1),
    (331, 6, 1, '454', 4, 'Trash: Disposals/Dump Fee', 1, 10, 0.00, 31500.00, 1),
    (332, 6, 1, '480.2', 4, 'Insurance-Trash Truck & Dumpster', 1, 10, 0.00, 4000.00, 1),
    -- Utility Fund Administration Expenditures
    (340, 6, 1, '460', 4, 'Supt Salaries', 1, 10, 0.00, 15000.00, 1),
    (341, 6, 1, '460.1', 4, 'Clerk Salaries', 1, 10, 0.00, 15000.00, 1),
    (342, 6, 1, '460.12', 4, 'Part Time Salaries', 1, 10, 0.00, 16000.00, 1),
    (343, 6, 1, '460.2', 4, 'Admin Deputy Clerk', 1, 10, 0.00, 2500.00, 1),
    (344, 6, 1, '465', 4, 'Office Supplies/Postage', 1, 10, 0.00, 3500.00, 1),
    (345, 6, 1, '470', 4, 'Outside Service-Lab', 1, 10, 0.00, 3500.00, 1),
    (346, 6, 1, '480', 4, 'Insurance: Bldg/Vhcl', 1, 10, 0.00, 14475.00, 1),
    (347, 6, 1, '480.1', 4, 'Insurance: Work Comp', 1, 10, 0.00, 2000.00, 1),
    (348, 6, 1, '483', 4, 'Payroll Taxes', 1, 10, 0.00, 3525.00, 1),
    (349, 6, 1, '491', 4, 'Employee Benefits-Health Ins', 1, 10, 0.00, 9100.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

-- ============================================
-- WSD (WILEY SANITATION DISTRICT) ENTERPRISE FUND
-- Total Revenue Budget: $5,879,527 (includes $5.7M grant)
-- Total Expenditure Budget: $5,778,145 (includes $5.7M capital outlay)
-- ============================================
PRINT 'Seeding WSD Enterprise Fund accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    -- WSD Revenues
    (400, 1, 1, 'WSD-SEWAGE', 4, 'Sewage Sales', 0, 12, 0.00, 150000.00, 1),
    (401, 1, 1, 'WSD-GRANT', 4, 'Grant (Ponds Project)', 0, 12, 0.00, 5725427.00, 1),
    (402, 1, 1, 'WSD-INTEREST', 4, 'Interest on Investments', 0, 12, 0.00, 2100.00, 1),
    (403, 1, 1, 'WSD-MISC', 4, 'Misc', 0, 12, 0.00, 2000.00, 1),
    -- WSD Expenditures
    (410, 1, 1, 'WSD-401', 4, 'Permits and Assessments', 1, 12, 0.00, 976.00, 1),
    (411, 1, 1, 'WSD-401.1', 4, 'Bank Service & Interest', 1, 12, 0.00, 35.00, 1),
    (412, 1, 1, 'WSD-405', 4, 'Outside Service Lab Fees', 1, 12, 0.00, 700.00, 1),
    (413, 1, 1, 'WSD-405.1', 4, 'Budget, Audit, Legal', 1, 12, 0.00, 18000.00, 1),
    (414, 1, 1, 'WSD-410', 4, 'Office Supplies/Postage', 1, 12, 0.00, 1800.00, 1),
    (415, 1, 1, 'WSD-413.4', 4, 'Education', 1, 12, 0.00, 100.00, 1),
    (416, 1, 1, 'WSD-416', 4, 'Dues & Subs', 1, 12, 0.00, 200.00, 1),
    (417, 1, 1, 'WSD-418', 4, 'Lift Station - Utilities', 1, 12, 0.00, 2500.00, 1),
    (418, 1, 1, 'WSD-420', 4, 'Collection Fee', 1, 12, 0.00, 6600.00, 1),
    (419, 1, 1, 'WSD-425', 4, 'Supplies and Expenses', 1, 12, 0.00, 2000.00, 1),
    (420, 1, 1, 'WSD-430', 4, 'Insurance', 1, 12, 0.00, 7500.00, 1),
    (421, 1, 1, 'WSD-432.53', 4, 'Sewer Cleaning', 1, 12, 0.00, 7600.00, 1),
    (422, 1, 1, 'WSD-484', 4, 'Property Taxes', 1, 12, 0.00, 7.00, 1),
    (423, 1, 1, 'WSD-486', 4, 'Backhoe Repairs', 1, 12, 0.00, 300.00, 1),
    (424, 1, 1, 'WSD-489', 4, 'PU Usage Fee', 1, 12, 0.00, 2400.00, 1),
    (425, 1, 1, 'WSD-491', 4, 'Fuel', 1, 12, 0.00, 2000.00, 1),
    (426, 1, 1, 'WSD-415', 4, 'Capital Outlay (Ponds Project)', 1, 12, 0.00, 5725427.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

-- ============================================
-- GENERAL FUND - REVENUES
-- Total Budget: $192,683 (excluding property taxes)
-- ============================================
PRINT 'Seeding General Fund Revenue accounts...';

MERGE INTO MunicipalAccounts AS target
USING (VALUES
    -- Intergovernmental Revenue
    (500, 3, 1, '332.1', 'Governmental', 'Federal: Mineral Lease', 0, 0, 0.00, 360.00, 1),
    (501, 3, 1, '333', 'Governmental', 'Cigarette Taxes', 0, 0, 0.00, 240.00, 1),
    (502, 3, 1, '334.31', 'Governmental', 'Highways Users', 0, 0, 0.00, 18153.00, 1),
    (503, 3, 1, '313', 'Governmental', 'Additional MV', 0, 0, 0.00, 1775.00, 1),
    (504, 3, 1, '334', 'Governmental', 'Severance', 0, 0, 0.00, 27.00, 1),
    (505, 3, 1, '337.17', 'Governmental', 'County Road & Bridge', 0, 0, 0.00, 1460.00, 1),
    -- Tax Revenues
    (510, 3, 1, '311.2', 'Governmental', 'Senior Homestead Exemption', 0, 0, 0.00, 1500.00, 1),
    (511, 3, 1, '312', 'Governmental', 'Specific Ownership Taxes', 0, 0, 0.00, 5100.00, 1),
    (512, 3, 1, '314', 'Governmental', 'Tax A', 0, 0, 0.00, 2500.00, 1),
    (513, 3, 1, '319', 'Governmental', 'Penalties & Interest on Delinquent Taxes', 0, 0, 0.00, 35.00, 1),
    (514, 3, 1, '336', 'Governmental', 'Sales Tax', 0, 0, 0.00, 120000.00, 1),
    (515, 3, 1, '318.2', 'Governmental', 'Franchise Fee', 0, 0, 0.00, 7058.00, 1),
    -- Licenses & Permits
    (520, 3, 1, '322.7', 'Governmental', 'Animal Licenses', 0, 0, 0.00, 50.00, 1),
    -- Charges for Services
    (530, 3, 1, '310', 'Governmental', 'WSD Collection Fee', 0, 0, 0.00, 6000.00, 1),
    (531, 3, 1, '370', 'Governmental', 'Housing Authority Mgt Fee', 0, 0, 0.00, 12000.00, 1),
    (532, 3, 1, '373', 'Governmental', 'Pickup Usage Fee', 0, 0, 0.00, 2400.00, 1),
    -- Miscellaneous Receipts
    (540, 3, 1, '361', 'Governmental', 'Interest Earnings', 0, 0, 0.00, 325.00, 1),
    (541, 3, 1, '365', 'Governmental', 'Dividends', 0, 0, 0.00, 100.00, 1),
    (542, 3, 1, '363', 'Governmental', 'Lease', 0, 0, 0.00, 1100.00, 1),
    (543, 3, 1, '350', 'Governmental', 'Wiley Hay Days Donations', 0, 0, 0.00, 10000.00, 1),
    (544, 3, 1, '362', 'Governmental', 'Donations', 0, 0, 0.00, 2500.00, 1)
) AS source (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive)
    VALUES (source.Id, source.DepartmentId, source.BudgetPeriodId, source.AccountNumber, source.FundClass, source.Name, source.Type, source.Fund, source.Balance, source.BudgetAmount, source.IsActive);

SET IDENTITY_INSERT MunicipalAccounts OFF;
GO

-- ============================================
-- VERIFICATION QUERY
-- ============================================
PRINT '';
PRINT '=== FY 2026 BUDGET SEED SUMMARY ===';
PRINT '';

SELECT 'Table Counts' AS Category,
    (SELECT COUNT(*) FROM BudgetPeriods) AS BudgetPeriods,
    (SELECT COUNT(*) FROM Departments) AS Departments,
    (SELECT COUNT(*) FROM MunicipalAccounts) AS Accounts,
    (SELECT COUNT(*) FROM Enterprises) AS Enterprises,
    (SELECT COUNT(*) FROM Vendors) AS Vendors;

SELECT
    d.Name AS Department,
    ma.Type,
    COUNT(*) AS AccountCount,
    FORMAT(SUM(ma.BudgetAmount), 'C') AS TotalBudget
FROM MunicipalAccounts ma
JOIN Departments d ON ma.DepartmentId = d.Id
GROUP BY d.Name, ma.Type
ORDER BY d.Name, ma.Type;

SELECT
    'GRAND TOTALS' AS Summary,
    COUNT(*) AS TotalAccounts,
    FORMAT(SUM(CASE WHEN Type = 0 THEN BudgetAmount ELSE 0 END), 'C') AS TotalRevenueBudget,
    FORMAT(SUM(CASE WHEN Type IN (1) THEN BudgetAmount ELSE 0 END), 'C') AS TotalExpenditureBudget
FROM MunicipalAccounts;

PRINT '';
PRINT 'FY 2026 Budget Seed Complete!';
GO
