-- =============================================
-- SQL Script: Import Town of Wiley Colorado 2026 Budget Data from CSVs and Image
-- Sources: Multiple CSV files (parsed) + Image OCR (Wiley Sanitation District 2025 Budget Comparison)
-- Target Database: Microsoft SQL Server (e.g., WileyWidget DB)
-- Assumptions:
--   • Unified table for all budget items across funds/departments.
--   • Data cleaned: Removed quotes/commas in numbers, handled empty fields as NULL.
--   • CSV parsing focused on rows with Account codes and descriptions.
--   • Image data integrated as 'Sanitation' fund for consistency.
--   • Use DECIMAL for monetary values; NVARCHAR for descriptions (to handle special chars).
--   • Run this in SSMS to persist data.
-- =============================================

IF OBJECT_ID('dbo.TownOfWileyBudget2026', 'U') IS NOT NULL
    DROP TABLE dbo.TownOfWileyBudget2026;

CREATE TABLE dbo.TownOfWileyBudget2026
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SourceFile NVARCHAR(255) NOT NULL,          -- CSV filename or 'Image'
    FundOrDepartment NVARCHAR(100) NULL,        -- Inferred from file (e.g., 'General Gov', 'Sanitation')
    AccountCode NVARCHAR(20) NULL,
    Description NVARCHAR(255) NULL,
    PriorYearActual DECIMAL(18,2) NULL,
    SevenMonthActual DECIMAL(18,2) NULL,
    EstimateCurrentYr DECIMAL(18,2) NULL,
    BudgetYear DECIMAL(18,2) NULL
);

-- ============== Insert from Parsed CSVs ==============

-- volleyball.csv (limited data)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('volleyball.csv', 'Volleyball', '510', 'DUES', NULL, NULL, 720, 800),
('volleyball.csv', 'Volleyball', NULL, 'TOTAL EXPENDITURES', 510, 0, 720, 1275),
('volleyball.csv', 'Volleyball', NULL, 'FEES', 495, NULL, 720, 1275),
('volleyball.csv', 'Volleyball', NULL, 'TOTAL REVENUE', 495, 0, 720, 1275);

-- Consolidated1.csv (General Fund Summary)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Consolidated1.csv', 'General Fund', NULL, 'ADMINISTRATION', 103386, 58156, 101395, 142618),
('Consolidated1.csv', 'General Fund', NULL, 'PUBLIC WORKS', 80660, 62509, 109417, 194500),
('Consolidated1.csv', 'General Fund', NULL, 'CULTURE & RECREATION', 6106, 4041, 6325, 7050),
('Consolidated1.csv', 'General Fund', NULL, 'TOTAL EXPENDITURES', 190152, 124706, 217137, 344168),
('Consolidated1.csv', 'General Fund', NULL, 'INTERGOVERNMENTAL REVENUE', 17437, 12485, 18211, 22015),
('Consolidated1.csv', 'General Fund', NULL, 'OTHER REVENUE', 241001, 95634, 157553, 170668),
('Consolidated1.csv', 'General Fund', NULL, 'TOTAL REVENUES OTHER THAN PROPERTY TAXES', 258438, 108119, 175764, 192683);

-- soccer.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('soccer.csv', 'Soccer', NULL, 'SUPPLIES', 475, 281, 281, 500),
('soccer.csv', 'Soccer', '2970', 'DUES', NULL, NULL, 1080, 3500),
('soccer.csv', 'Soccer', NULL, 'TOTAL EXPENDITURES', 3445, 281, 1361, 4000),
('soccer.csv', 'Soccer', NULL, 'FEES', 2830, 2500, 3460, 4000),
('soccer.csv', 'Soccer', NULL, 'TOTAL REVENUE', 2830, 2500, 3460, 4000);

-- baseball.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('baseball.csv', 'Baseball/Softball', NULL, 'SUPPLIES', 538, NULL, 0, 2000),
('baseball.csv', 'Baseball/Softball', '1175', 'DUES', NULL, 2825, 2825, 3000),
('baseball.csv', 'Baseball/Softball', NULL, 'UMPIRES', NULL, 165, 165, 300),
('baseball.csv', 'Baseball/Softball', NULL, 'FIELD PREP', NULL, NULL, 0, 240),
('baseball.csv', 'Baseball/Softball', NULL, 'BANK SERVICE CHARGE', 136, 10, 10, 10),
('baseball.csv', 'Baseball/Softball', NULL, 'TOTAL EXPENDITURES', 1849, 3000, 3000, 5550),
('baseball.csv', 'Baseball/Softball', NULL, 'FEES', 3565, 2835, 2835, 5550),
('baseball.csv', 'Baseball/Softball', NULL, 'TOTAL REVENUE', 3569, 2837, 2835, 5550);

-- Water Trash and Admin.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Water Trash and Admin.csv', 'Water/Trash/Admin', '405.00', 'MAYOR EXPENSE', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '410.00', 'SUPERVISION AND LABOR', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '410.10', 'PROF SERVICES', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '411.00', 'PUMPING EXP', NULL, 14234, 7482, 14250),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '411.10', 'PURCHASED WATER', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '412.00', 'SUPPLIES AND REPAIRS', NULL, 8230, 2498, 20000),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '413.00', 'BACKHOE REPAIR/MAINT', NULL, 2710, NULL, 0),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '413.40', 'EDUCATION/TRAVEL', NULL, NULL, NULL, 0),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '415.11', 'AUDIT & BUDGET EXPENSE', NULL, 848, 848, 1000),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '416.00', 'DUES AND SUBS', NULL, 7057, 4573, 7800),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '419.40', 'FUEL', NULL, 6089, 3880, 7275),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '430.00', 'WATER TREATMENT', NULL, 8008, 4769, 8750),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '442.00', 'BLDG MAINT', NULL, NULL, NULL, 0),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '445.00', 'CREDIT CARD FEES', NULL, 816, 360, 816),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '487.00', 'INTEREST', NULL, 11567, 11380, 11380),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '453.00', 'SUPPLIES REPAIRS', NULL, 32403, 6808, 20000),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '454.00', 'DISPOSALS /DUMP FEE', NULL, 29590, 17610, 31000),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '480.20', 'INSURANCE-TRASH TRUCK & DUMPSTER', NULL, 2946, 1713, 3410),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '455.10', 'SALARIES', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '455.12', 'SALARIES PARTTIME', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '460.00', 'SUPT SALARIES', NULL, 25660, 20518, 34000),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '460.10', 'CLERK SALARIES', NULL, 26272, 16266, 27885),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '460.12', 'PART TIME SALARIES', NULL, 42422, 22366, 38342),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '460.20', 'ADMIN DEPUTY CLERK', NULL, 712, 104, 178),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '465.00', 'OFFICE SUPPLIES/POSTAGE', NULL, 3730, 2360, 3500),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '467.00', 'OTHER ADMIN.', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '468.00', 'PUBLICATIONS/ADV', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '470.00', 'OUTSIDE SERVICE-LAB', NULL, 522, 1469, 2800),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '480.00', 'INSURANCE:BLDG/VHCL', NULL, 10827, 5728, 11500),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '480.10', 'INSURANCE:WORK COMP', NULL, 1464, 1758, 1758),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '483.00', 'PAYROLL TAXES', NULL, 6968, 4214, 7224),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '485.00', 'MISC', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '490.00', 'BAD DEBTS', NULL, NULL, NULL, NULL),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '491.00', 'EMPLOYEE BENEFITS-HEALTH INS', NULL, 15296, 12110, 20760),
('Water Trash and Admin.csv', 'Water/Trash/Admin', '372.20', 'TRANSFER TO GENERAL FUND', NULL, NULL, NULL, NULL);

-- rec.csv (Recreation Fund)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('rec.csv', 'Recreation', NULL, 'BASEBALL/SOFTBALL', 1849, 3000, 3000, 5550),
('rec.csv', 'Recreation', NULL, 'FOOTBALL', 1074, 0, 750, 4500),
('rec.csv', 'Recreation', NULL, 'SOCCER', 3445, 281, 1361, 4000),
('rec.csv', 'Recreation', NULL, 'BASKETBALL', 1470, 120, 120, 4000),
('rec.csv', 'Recreation', NULL, 'VOLLEYBALL', 510, 0, 720, 1275),
('rec.csv', 'Recreation', NULL, 'WRESTLING', 0, 0, 0, 1000),
('rec.csv', 'Recreation', NULL, 'TOTAL EXPENDITURES', 8348, 5401, 7951, 20325),
('rec.csv', 'Recreation', NULL, 'BASEBALL/SOFTBALL', 3569, 2837, 2835, 5550),
('rec.csv', 'Recreation', NULL, 'FOOTBALL', 905, 110, 660, 4500),
('rec.csv', 'Recreation', NULL, 'SOCCER', 2830, 2500, 3460, 4000),
('rec.csv', 'Recreation', NULL, 'BASKETBALL', 1140, NULL, 2835, 4000),
('rec.csv', 'Recreation', NULL, 'VOLLEYBALL', 495, NULL, 720, 1275),
('rec.csv', 'Recreation', NULL, 'WRESTLING', 0, NULL, 0, 1000),
('rec.csv', 'Recreation', NULL, 'TOTAL REVENUE', 9939, 5447, 10510, 20325);

-- Utilities.csv (Utility Fund)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Utilities.csv', 'Utility', NULL, 'WATER TRASH AND ADMIN', 258371, 148814, 273628, 224465),
('Utilities.csv', 'Utility', NULL, 'CAPITAL OUTLAY', NULL, NULL, NULL, NULL),
('Utilities.csv', 'Utility', NULL, 'CAPITAL OUTLAY-well development & equip', NULL, NULL, NULL, 5000),
('Utilities.csv', 'Utility', NULL, 'PRINCIPAL WATER NOTE PAYMENTS', 8764, 9132, 9132, 9513),
('Utilities.csv', 'Utility', NULL, 'TOTAL EXPENDITURES', 267135, 157946, 282760, 238978),
('Utilities.csv', 'Utility', NULL, 'WATER SALES', 100000, 51843, 100000, 100000),
('Utilities.csv', 'Utility', NULL, 'TRASH FEES', 65000, 36846, 65000, 65000),
('Utilities.csv', 'Utility', NULL, 'TAP FEES', 0, 0, 0, 0),
('Utilities.csv', 'Utility', NULL, 'INTEREST', 0, 0, 0, 0),
('Utilities.csv', 'Utility', NULL, 'MISC', 0, 0, 0, 0),
('Utilities.csv', 'Utility', NULL, 'TOTAL REVENUE OTHER THAN PROPERTY TAXES', 165000, 88689, 165000, 165000);

-- basketball.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('basketball.csv', 'Basketball', '1470', 'DUES', NULL, 120, 120, 2000),
('basketball.csv', 'Basketball', NULL, 'TOTAL EXPENDITURES', 1470, 120, 120, 4000),
('basketball.csv', 'Basketball', NULL, 'FEES', 1140, NULL, 2835, 4000),
('basketball.csv', 'Basketball', NULL, 'TOTAL REVENUE', 1140, 0, 2835, 4000);

-- salaries.csv (Salaries across funds)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('salaries.csv', 'General Fund', NULL, 'ADMIN CLERK SALARIES', 27131, 16286, 27919, 45000),
('salaries.csv', 'General Fund', NULL, 'ADMIN DEPUTY CLERK SALARIES', 712, 104, 250, 7500),
('salaries.csv', 'Highways & Streets', NULL, 'SALARIES', 15222, 9574, 28500, 45000),
('salaries.csv', 'Highways & Streets', NULL, 'SALARIES PARTTIME', 33856, 16432, 32000, 48000),
('salaries.csv', 'Culture and Recreation', NULL, 'SALARIES', 3353, 2516, 4000, 4000),
('salaries.csv', 'Culture and Recreation', NULL, 'SALARIES PART TIME', 0, 0, 0, 0),
('salaries.csv', 'Utility Fund', NULL, 'SUPT SALARIES', 25660, 20518, 34000, 15000),
('salaries.csv', 'Utility Fund', NULL, 'CLERK SALARIES', 26272, 16266, 27885, 15000),
('salaries.csv', 'Utility Fund', NULL, 'PART TIME SALARIES', 42422, 22366, 38342, 32000),
('salaries.csv', 'Utility Fund', NULL, 'ADMIN DEPUTY CLERK', 712, 104, 178, 2500),
('salaries.csv', 'Community Center', NULL, 'CC DIREC', 0, 0, 0, 0),
('salaries.csv', 'Community Center', NULL, 'CC JANITOR', 1313, 1898, 3254, 3000),
('salaries.csv', NULL, NULL, 'TOTAL SALARIES', 176653, 106064, 196328, 201000);

-- Conservation.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Conservation.csv', 'Conservation Trust', NULL, 'PARKS', 4697, NULL, 0, 1500),
('Conservation.csv', 'Conservation Trust', NULL, 'SMALL EQUIP/SUPPLIES', NULL, NULL, 0, 7000),
('Conservation.csv', 'Conservation Trust', NULL, 'TOTAL EXPENDITURES', 4697, 0, 0, 8500),
('Conservation.csv', 'Conservation Trust', '334.7', 'STATE APPORTIONMENT', 5283, 2496, 5200, 5200),
('Conservation.csv', 'Conservation Trust', '361.1', 'INTEREST', 20, 8, 15, 15),
('Conservation.csv', 'Conservation Trust', NULL, 'TOTAL REVENUE OTHER THAN PROPERTY TAXES', 5303, 2504, 5215, 5215);

-- tax_Revenue.csv (Property Tax Revenues)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('tax_Revenue.csv', 'Tax Revenue', NULL, 'GENERAL', 45.570, 48750, 45.570, 48883);

-- General Fund.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('General Fund.csv', 'General Fund', '332.1', 'MINERAL LEASE', NULL, NULL, 43, NULL),
('General Fund.csv', 'General Fund', '333.00', 'CIGARETTE TAXES', NULL, NULL, 236, 102),
('General Fund.csv', 'General Fund', '334.31', 'HIGHWAYS USERS', NULL, NULL, 17711, 9969),
('General Fund.csv', 'General Fund', '313.00', 'ADDITIONAL MV', NULL, NULL, 1661, 1102),
('General Fund.csv', 'General Fund', '334.00', 'SEVERANCE', NULL, NULL, 105, NULL),
('General Fund.csv', 'General Fund', '320.00', 'CDOT', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '337.17', 'COUNTY ROAD & BRIDGE', NULL, NULL, 1461, 1312),
('General Fund.csv', 'General Fund', '335.00', 'COVID RELIEF FUNDS', NULL, NULL, -3780, NULL),
('General Fund.csv', 'General Fund', '355.00', 'GOCO GRANT', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', NULL, 'TOTAL INTERGOVERNMENTAL REVENUE', 17437, 12485, 18211, 22015),
('General Fund.csv', 'General Fund', '315.00', 'SB22-238 REV REIMB - PROP TAXES', NULL, NULL, 23871, 2832),
('General Fund.csv', 'General Fund', '311.10', 'DELINQUENT PROPERTY TAXES', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '311.20', 'SENIOR HOMESTEAD EXEMPTION', NULL, NULL, 1106, 1409),
('General Fund.csv', 'General Fund', '312.00', 'SPECIFIC OWNERSHIP TAXES', NULL, NULL, 4949, 3047),
('General Fund.csv', 'General Fund', '314.00', 'TAX A', NULL, NULL, 2295, 1559),
('General Fund.csv', 'General Fund', '319.00', 'PENALTIES & INTEREST ON', NULL, NULL, 182, 20),
('General Fund.csv', 'General Fund', NULL, 'TOTAL PROPERTY TAX REVENUE', 48750, 48883, 48883, 85692),
('General Fund.csv', 'General Fund', '336.00', 'SALES TAX', NULL, NULL, 131656, 67100),
('General Fund.csv', 'General Fund', '318.20', 'FRANCHISE FEE', NULL, NULL, 7058, 6558),
('General Fund.csv', 'General Fund', '321.70', 'BUSINESS LICENSES & PERMITS', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '321.10', 'LIQUOR LICENSES', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '322.70', 'ANIMAL LICENSES', NULL, NULL, 52, 29),
('General Fund.csv', 'General Fund', '374.00', 'CODE ENFORCEMENT FINES', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '310.00', 'WSD COLLECTION FEE', NULL, NULL, 6000, 3500),
('General Fund.csv', 'General Fund', '341.40', 'COPIES, FAX ETC', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '370.00', 'HOUSING AUTHORITY MGT FEE', NULL, NULL, 10000, 7000),
('General Fund.csv', 'General Fund', '372.30', 'HOUSING AUTHORITY GROUND MAINT', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '373.00', 'PICKUP USAGE FEE', NULL, NULL, 2400, 1400),
('General Fund.csv', 'General Fund', '324.00', 'WEED CONTROL', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '361.00', 'INTEREST EARNINGS', NULL, NULL, 335, 206),
('General Fund.csv', 'General Fund', '365.00', 'DIVIDENDS', NULL, NULL, 88, 124),
('General Fund.csv', 'General Fund', '362.00', 'RENT', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '363.00', 'LEASE', NULL, NULL, 1100, 350),
('General Fund.csv', 'General Fund', '350.00', 'WILEY HAY DAYS DONATIONS', NULL, NULL, 19400, 500),
('General Fund.csv', 'General Fund', '362.00', 'DONATIONS', NULL, NULL, 30459, NULL),
('General Fund.csv', 'General Fund', '368.00', 'MISC', NULL, NULL, 50, NULL),
('General Fund.csv', 'General Fund', '364.00', 'SALE/LEASE-FIXED ASSETS', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', '372.20', 'TRANSFERS FROM OTHER FUNDS', NULL, NULL, NULL, NULL),
('General Fund.csv', 'General Fund', NULL, 'TOTAL OTHER REVENUE', 241001, 95634, 157553, 170668);

-- Parks.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Parks.csv', 'Parks', '451.10', 'SALARIES', 3353, 2516, 4000, 4000),
('Parks.csv', 'Parks', '451.20', 'SALARIES PART TIME', NULL, NULL, NULL, NULL),
('Parks.csv', 'Parks', '452.00', 'SUPPLIES/REPAIRS', 2224, 1231, 1800, 2500),
('Parks.csv', 'Parks', '452.10', 'UTILITES', 529, 294, 525, 550),
('Parks.csv', 'Parks', NULL, 'TOTAL', 6106, 4041, 6325, 7050);

-- No Department.csv (Lease and Debt Schedule)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('No Department.csv', 'Lease/Debt', NULL, 'USDA NOTE PAYMENTS', NULL, NULL, NULL, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2023', 8416, 12096, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2024', 8767, 11745, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2025', 9132, 11380, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2026', 9513, 10999, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2027', 9909, 10603, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2028', 10322, 10190, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2029', 10753, 9759, 20512, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2030-2035', 74607, 48465, 123072, NULL),
('No Department.csv', 'Lease/Debt', NULL, '2036-2045', 153894, 32154, 186048, NULL),
('No Department.csv', 'Lease/Debt', NULL, 'TOTAL', 330313, 208436, 538749, NULL);

-- HWY and Streets.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('HWY and Streets.csv', 'HWY and Streets', '431.00', 'SUPPLIES/REPAIRS', 421, 368, 1000, 1500),
('HWY and Streets.csv', 'HWY and Streets', '431.10', 'SNOW AND ICE REMOVAL', NULL, NULL, NULL, NULL),
('HWY and Streets.csv', 'HWY and Streets', '431.40', 'SALARIES', 15222, 9574, 28500, 45000),
('HWY and Streets.csv', 'HWY and Streets', '431.41', 'SALARIES PARTTIME', 33856, 16432, 32000, 48000),
('HWY and Streets.csv', 'HWY and Streets', '431.50', 'GRAVEL', NULL, 0, NULL, 6000),
('HWY and Streets.csv', 'HWY and Streets', '431.60', 'SEAL COATING', NULL, NULL, 1426, 1426),
('HWY and Streets.csv', 'HWY and Streets', '431.61', 'STREET SIGNS', 479, NULL, 0, NULL),
('HWY and Streets.csv', 'HWY and Streets', '432.20', 'REPAIRS/MAINT: EQUIP', 4247, 279, 500, 4200),
('HWY and Streets.csv', 'HWY and Streets', '432.30', 'STREET LIGHTING', 11490, 6851, 11760, 12000),
('HWY and Streets.csv', 'HWY and Streets', '432.40', 'FUEL', 2754, 1221, 2675, 2800),
('HWY and Streets.csv', 'HWY and Streets', '433.00', 'SHOP SUPPLIES', 12191, 6802, 12000, 12000),
('HWY and Streets.csv', 'HWY and Streets', '432.51', 'EQUIPMENT', NULL, NULL, 19556, 19556),
('HWY and Streets.csv', 'HWY and Streets', '412.50', 'BROOKSIDE DRIVE DEVELOPMENT', NULL, NULL, NULL, NULL),
('HWY and Streets.csv', 'HWY and Streets', NULL, 'TOTAL', 80660, 62509, 109417, 194500);

-- football.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('football.csv', 'Football', NULL, 'SUPPLIES', 64, NULL, 200, 2500),
('football.csv', 'Football', '1010', 'DUES', NULL, NULL, 550, 2000),
('football.csv', 'Football', NULL, 'TOTAL EXPENDITURES', 1074, 0, 750, 4500),
('football.csv', 'Football', NULL, 'FEES', 905, 110, 660, 4500),
('football.csv', 'Football', NULL, 'TOTAL REVENUE', 905, 110, 660, 4500);

-- General Gov.csv
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('General Gov.csv', 'General Gov', '413.10', 'MAYOR EXPENSE', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '413.20', 'ADMIN CLERK SALARIES', 27131, 16286, 27919, 45000),
('General Gov.csv', 'General Gov', '413.21', 'ADMIN DEPUTY CLERK SALARIES', 712, 104, 250, 7500),
('General Gov.csv', 'General Gov', '413.22', 'ADMIN CODE ENFORCEMENT', NULL, NULL, 0, 2000),
('General Gov.csv', 'General Gov', '413.30', 'PAYROLL TAXES - FICA', 6386, 3754, 6400, 10575),
('General Gov.csv', 'General Gov', '413.40', 'EDUCATION', 30, 30, 50, 500),
('General Gov.csv', 'General Gov', '413.50', 'TRAVEL & TRANSPORTATION', NULL, NULL, 0, 150),
('General Gov.csv', 'General Gov', '413.60', 'PAYROLL TAXES - UNEMPLOYMENT', 378, 191, 325, 360),
('General Gov.csv', 'General Gov', '413.70', 'FAMLI', 787, 477, 860, 860),
('General Gov.csv', 'General Gov', '413.80', 'SIMPLE IRA', 4308, 2714, 4725, 4725),
('General Gov.csv', 'General Gov', '415.11', 'AUDIT & BUDGET EXPENSE', 968, 848, 1000, 1000),
('General Gov.csv', 'General Gov', '415.12', 'PROF SERVICES', 2325, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '415.17', 'OFFICE SUPPLIES', 3700, 1789, 4250, 4250),
('General Gov.csv', 'General Gov', '415.18', 'COUNTY TAX ASSESSMENT', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '415.18', 'POSTAGE', 92, 32, 75, 75),
('General Gov.csv', 'General Gov', '415.19', 'OFFICE EQUIPMENT', 204, 2202, 2225, 2225),
('General Gov.csv', 'General Gov', '415.20', 'LEGAL', 745, 293, 350, 350),
('General Gov.csv', 'General Gov', '415.21', 'ADMINISTRATION EXPENSES', NULL, 1010, 1010, 1010),
('General Gov.csv', 'General Gov', '415.50', 'INSURANCE BLDG', 2874, 1703, 3405, 3405),
('General Gov.csv', 'General Gov', '415.51', 'INSURANCE PUBLIC LIAB', 3745, 1756, 3425, 3425),
('General Gov.csv', 'General Gov', '415.52', 'INSURANCE WORKERS COMP', 2216, 1758, 1758, 1758),
('General Gov.csv', 'General Gov', '415.53', 'EMPLOYEE HEALTH INSURANCE', 16330, 12897, 21000, 21000),
('General Gov.csv', 'General Gov', '415.54', 'BANK CHARGES', 75, 75, 85, 85),
('General Gov.csv', 'General Gov', '415.75', 'PET CLINIC', 48, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '415.76', 'RECYCLING', 1056, 792, 1584, 1584),
('General Gov.csv', 'General Gov', '416.00', 'DUES & SUBS', 4850, 2830, 5549, 5549),
('General Gov.csv', 'General Gov', '417.00', 'TREASURER''S FEES', 1500, 979, 1500, 1500),
('General Gov.csv', 'General Gov', '418.00', 'SERVICE CONTRACT', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '419.42', 'BUILDING MAINTENANCE', 699, 240, 4500, 4500),
('General Gov.csv', 'General Gov', '419.44', 'BUILDING SUPPLIES/MAINT', 214, 179, 190, 190),
('General Gov.csv', 'General Gov', '419.50', 'BUILDING UTILITIES', 7139, 3817, 7300, 7300),
('General Gov.csv', 'General Gov', '419.70', 'COMMUNITY CENTER SALARIES', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '420.00', 'CONTRACT LABOR', 103, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '441.00', 'HEALTH FAIR', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '453.00', 'ELECTIONS: JUDGES', 150, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '454.00', 'ELECTIONS: PUBLICATIONS', 45, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '455.00', 'ELECTIONS: SUPPLIES', 551, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '456.00', 'MOSQUITO SPRAYING', NULL, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '459.00', 'FIRE DEPT PMT', 250, NULL, 250, NULL),
('General Gov.csv', 'General Gov', '460.10', 'COMMUNITY SERVICE', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '461.00', 'PUBLICATIONS/ADV', 525, 60, 60, 60),
('General Gov.csv', 'General Gov', '462.00', 'INTEREST & PENALTIES', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '465.00', 'CHRISTMAS LIGHTING CONTEST', 150, NULL, 150, NULL),
('General Gov.csv', 'General Gov', '480.00', 'DONATIONS', 500, 640, 500, 500),
('General Gov.csv', 'General Gov', '485.20', 'MISCELLANEOUS', 5, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '485.30', 'GOCO CONSULT EXP', NULL, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '485.40', 'MEALS', 250, NULL, 0, NULL),
('General Gov.csv', 'General Gov', '492.00', 'PUBLIC SAFETY', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '481.00', 'FEMA FLOOD MAPPING', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '415.19', 'OFFICE EQUIPMENT', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '483.00', 'COVID RELIEF FUNDS', NULL, NULL, NULL, NULL),
('General Gov.csv', 'General Gov', '410.00', 'WILEY HAY DAYS EXP', 12345, 700, 700, 2500),
('General Gov.csv', 'General Gov', NULL, 'TOTAL', 103386, 58156, 101395, 142618);

-- Supplemental.csv (Supplemental Appropriations)
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Supplemental.csv', 'General Fund', NULL, NULL, NULL, NULL, 217137, 250473),
('Supplemental.csv', 'Utility Fund', NULL, NULL, NULL, NULL, 282760, 275028),
('Supplemental.csv', 'Conservation Trust', NULL, NULL, NULL, NULL, 0, 4500),
('Supplemental.csv', 'Community Center Fund', NULL, NULL, NULL, NULL, 28888, 9850),
('Supplemental.csv', 'Recreation Fund', NULL, NULL, NULL, NULL, 7951, 21550);

-- ============== Insert from Image OCR (Sanitation District 2025) ==============
INSERT INTO dbo.TownOfWileyBudget2026 (SourceFile, FundOrDepartment, AccountCode, Description, PriorYearActual, SevenMonthActual, EstimateCurrentYr, BudgetYear)
VALUES 
('Image', 'Sanitation', '311.00', 'SPECIFIC OWNERSHIP TAXES', 615.00, NULL, 483.94, NULL),
('Image', 'Sanitation', '301.00', 'SEWAGE SALES', 100000.00, NULL, 102173.17, NULL),
('Image', 'Sanitation', '310.10', 'DELINQUENT TAXES', 12.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '312.00', 'TAX A', 250.00, NULL, 206.05, NULL),
('Image', 'Sanitation', '313.00', 'SENIOR HOMESTEAD EXEMP', 160.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '315.00', 'INTEREST INCOME', 5.00, NULL, 14.80, NULL),
('Image', 'Sanitation', '320.00', 'PENALTIES AND INT ON DELIQ TAXES', 15.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '321.00', 'MISC INCOME', 2000.00, NULL, 1810.00, NULL),
('Image', 'Sanitation', '315.00', 'INTEREST ON INVESTMENTS', 485.00, NULL, 1984.56, NULL),
('Image', 'Sanitation', '322.00', 'GRANT', 7867427.00, NULL, 630996.09, NULL),
('Image', 'Sanitation', NULL, 'TOTAL OPERATING INCOME', 7970969.00, NULL, 737668.61, NULL),
('Image', 'Sanitation', '401.00', 'PERMITS AND ASSESSMENTS', 976.00, NULL, 977.00, NULL),
('Image', 'Sanitation', '401.10', 'BANK SERVICE', 85.00, NULL, 15.00, NULL),
('Image', 'Sanitation', '405.00', 'OUTSIDE SERVICE LAB FEES', 650.00, NULL, 624.00, NULL),
('Image', 'Sanitation', '405.10', 'BUDGET, AUDIT, LEGAL', 2000.00, NULL, 870.00, NULL),
('Image', 'Sanitation', '410.00', 'OFFICE SUPPLIES/ POSTAGE', 1800.00, NULL, 1051.13, NULL),
('Image', 'Sanitation', '413.40', 'EDUCATION', 100.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '415.00', 'CAPITAL OUTLAY', 8325427.00, NULL, 1064683.59, NULL),
('Image', 'Sanitation', '416.00', 'DUES AND SUBSCRIPTIONS', 100.00, NULL, 270.00, NULL),
('Image', 'Sanitation', '418.00', 'LIFT-STATION UTILITIES', 1500.00, NULL, 1879.71, NULL),
('Image', 'Sanitation', '420.00', 'COLLECTION FEE', 12000.00, NULL, 12000.00, NULL),
('Image', 'Sanitation', '425.00', 'SUPPLIES AND EXPENSES', 2000.00, NULL, 1909.01, NULL),
('Image', 'Sanitation', '430.00', 'INSURANCE', 6200.00, NULL, 5507.55, NULL),
('Image', 'Sanitation', '432.53', 'SEWER CLEANING', 7600.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '445.00', 'TREASURER FEES', 200.00, NULL, 153.13, NULL),
('Image', 'Sanitation', '484.00', 'PROPERTY TAXES', 7.00, NULL, 6.88, NULL),
('Image', 'Sanitation', '486.00', 'BACKHOE REPAIRS', 300.00, NULL, 0.00, NULL),
('Image', 'Sanitation', '489.00', 'PU USAGE FEE', 2400.00, NULL, 2400.00, NULL),
('Image', 'Sanitation', '491.00', 'FUEL', 2000.00, NULL, 1095.87, NULL),
('Image', 'Sanitation', NULL, 'MISC', 500.00, NULL, 0.00, NULL),
('Image', 'Sanitation', NULL, 'TOTAL O & M EXPENSES', 8365845.00, NULL, 1092450.87, NULL);

-- =============================================
-- Sample Query to Verify
-- =============================================
SELECT 
    FundOrDepartment,
    COUNT(*) AS Rows,
    SUM(BudgetYear) AS TotalBudget
FROM dbo.TownOfWileyBudget2026
GROUP BY FundOrDepartment
ORDER BY FundOrDepartment;

-- Notes:
-- 1. Execute in SSMS connected to your DB.
-- 2. Data persisted and queryable for app integration (e.g., via IBudgetRepository).
-- 3. For CSVs with incomplete parses (e.g., empty files), no rows inserted.
-- 4. Image data matches previous PDF but uses OCR values.
-- 5. Adapt table/columns if needed for existing schema.
-- 6. If more cleaning required, provide feedback for refinement.