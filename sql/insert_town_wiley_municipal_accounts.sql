-- Seed MunicipalAccounts for Town of Wiley FY 2026 Budget
-- This script creates all the municipal accounts referenced in the budget data

SET IDENTITY_INSERT MunicipalAccounts ON;

-- General Fund Revenue Accounts
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(1, 3, 1, '332.1', 'Federal: Mineral Lease', 4, 1, 0.00, 0.00, 1),
(2, 3, 1, '333.00', 'State: Cigarette Taxes', 4, 1, 0.00, 0.00, 1),
(3, 3, 1, '334.31', 'Highways Users', 4, 1, 0.00, 0.00, 1),
(4, 3, 1, '313.00', 'Additional MV', 4, 1, 0.00, 0.00, 1),
(5, 3, 1, '334.00', 'Severance', 4, 1, 0.00, 0.00, 1),
(6, 3, 1, '320.00', 'CDOT', 4, 1, 0.00, 0.00, 1),
(7, 3, 1, '337.17', 'County Road & Bridge', 4, 1, 0.00, 0.00, 1),
(8, 3, 1, '335.00', 'Covid Relief Funds', 4, 1, 0.00, 0.00, 1),
(9, 3, 1, '355.00', 'GOCO Grant', 4, 1, 0.00, 0.00, 1),
(10, 3, 1, '315.00', 'Taxes: SB22-238 Rev Reimb - Prop Taxes', 4, 1, 0.00, 0.00, 1),
(11, 3, 1, '311.10', 'Delinquent Property Taxes', 4, 1, 0.00, 0.00, 1),
(12, 3, 1, '311.20', 'Senior Homestead Exemption', 4, 1, 0.00, 0.00, 1),
(13, 3, 1, '312.00', 'Specific Ownership Taxes', 4, 1, 0.00, 0.00, 1),
(14, 3, 1, '314.00', 'Tax A', 4, 1, 0.00, 0.00, 1),
(15, 3, 1, '319.00', 'Penalties & Interest on Delinquent Taxes', 4, 1, 0.00, 0.00, 1),
(16, 3, 1, '336.00', 'Sales Tax', 4, 1, 0.00, 0.00, 1),
(17, 3, 1, '318.20', 'Franchise Fee', 4, 1, 0.00, 0.00, 1),
(18, 3, 1, '321.70', 'Licenses & Permits: Business Licenses & Permits', 4, 1, 0.00, 0.00, 1),
(19, 3, 1, '321.10', 'Liquor Licenses', 4, 1, 0.00, 0.00, 1),
(20, 3, 1, '322.70', 'Animal Licenses', 4, 1, 0.00, 0.00, 1),
(21, 3, 1, '374.00', 'Fines: Code Enforcement Fines', 4, 1, 0.00, 0.00, 1),
(22, 3, 1, '310.00', 'Charges for Services: WSD Collection Fee', 4, 1, 0.00, 0.00, 1),
(23, 3, 1, '341.40', 'Copies, Fax Etc', 4, 1, 0.00, 0.00, 1),
(24, 3, 1, '370.00', 'Housing Authority Mgt Fee', 4, 1, 0.00, 0.00, 1),
(25, 3, 1, '372.30', 'Housing Authority Ground Maint', 4, 1, 0.00, 0.00, 1),
(26, 3, 1, '373.00', 'Pickup Usage Fee', 4, 1, 0.00, 0.00, 1),
(27, 3, 1, '324.00', 'Weed Control', 4, 1, 0.00, 0.00, 1),
(28, 3, 1, '361.00', 'Miscellaneous Receipts: Interest Earnings', 4, 1, 0.00, 0.00, 1),
(29, 3, 1, '365.00', 'Dividends', 4, 1, 0.00, 0.00, 1),
(30, 3, 1, '362.00', 'Rent', 4, 1, 0.00, 0.00, 1),
(31, 3, 1, '363.00', 'Lease', 4, 1, 0.00, 0.00, 1),
(32, 3, 1, '350.00', 'Wiley Hay Days Donations', 4, 1, 0.00, 0.00, 1),
(33, 3, 1, '368.00', 'Misc', 4, 1, 0.00, 0.00, 1),
(34, 3, 1, '364.00', 'Sales & Comp for Fixed Assets: Sale/Lease-Fixed Assets', 4, 1, 0.00, 0.00, 1),
(35, 3, 1, '372.20', 'Transfers From Other Funds', 4, 1, 0.00, 0.00, 1);

-- General Fund Expenditure Accounts (General Government)
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(36, 3, 1, '413.10', 'Mayor Expense', 5, 1, 0.00, 0.00, 1),
(37, 3, 1, '413.20', 'Admin Clerk Salaries', 5, 1, 0.00, 0.00, 1),
(38, 3, 1, '413.21', 'Admin Deputy Clerk Salaries', 5, 1, 0.00, 0.00, 1),
(39, 3, 1, '413.22', 'Admin Code Enforcement', 5, 1, 0.00, 0.00, 1),
(40, 3, 1, '413.30', 'Payroll Taxes - FICA', 5, 1, 0.00, 0.00, 1),
(41, 3, 1, '413.40', 'Education', 5, 1, 0.00, 0.00, 1),
(42, 3, 1, '413.50', 'Travel & Transportation', 5, 1, 0.00, 0.00, 1),
(43, 3, 1, '413.60', 'Payroll Taxes - Unemployment', 5, 1, 0.00, 0.00, 1),
(44, 3, 1, '413.70', 'FAMLI', 5, 1, 0.00, 0.00, 1),
(45, 3, 1, '413.80', 'Simple IRA', 5, 1, 0.00, 0.00, 1),
(46, 3, 1, '415.11', 'Audit & Budget Expense', 5, 1, 0.00, 0.00, 1),
(47, 3, 1, '415.12', 'Prof Services', 5, 1, 0.00, 0.00, 1),
(48, 3, 1, '415.17', 'Office Supplies', 5, 1, 0.00, 0.00, 1),
(49, 3, 1, '415.18', 'County Tax Assessment', 5, 1, 0.00, 0.00, 1),
(50, 3, 1, '415.19', 'Office Equipment', 5, 1, 0.00, 0.00, 1),
(51, 3, 1, '415.20', 'Legal', 5, 1, 0.00, 0.00, 1),
(52, 3, 1, '415.21', 'Administration Expenses', 5, 1, 0.00, 0.00, 1),
(53, 3, 1, '415.50', 'Insurance Bldg', 5, 1, 0.00, 0.00, 1),
(54, 3, 1, '415.51', 'Insurance Public Liab', 5, 1, 0.00, 0.00, 1),
(55, 3, 1, '415.52', 'Insurance Workers Comp', 5, 1, 0.00, 0.00, 1),
(56, 3, 1, '415.53', 'Employee Health Insurance', 5, 1, 0.00, 0.00, 1),
(57, 3, 1, '415.54', 'Bank Charges', 5, 1, 0.00, 0.00, 1),
(58, 3, 1, '415.75', 'Pet Clinic', 5, 1, 0.00, 0.00, 1),
(59, 3, 1, '415.76', 'Recycling', 5, 1, 0.00, 0.00, 1),
(60, 3, 1, '416.00', 'Dues & Subs', 5, 1, 0.00, 0.00, 1),
(61, 3, 1, '417.00', 'Treasurer''s Fees', 5, 1, 0.00, 0.00, 1),
(62, 3, 1, '418.00', 'Service Contract', 5, 1, 0.00, 0.00, 1),
(63, 3, 1, '419.42', 'Building Maintenance', 5, 1, 0.00, 0.00, 1),
(64, 3, 1, '419.44', 'Building Supplies/Maint', 5, 1, 0.00, 0.00, 1),
(65, 3, 1, '419.50', 'Building Utilities', 5, 1, 0.00, 0.00, 1),
(66, 3, 1, '419.70', 'Community Center Salaries', 5, 1, 0.00, 0.00, 1),
(67, 3, 1, '420.00', 'Contract Labor', 5, 1, 0.00, 0.00, 1),
(68, 3, 1, '441.00', 'Health Fair', 5, 1, 0.00, 0.00, 1),
(69, 3, 1, '453.00', 'Elections: Judges', 5, 1, 0.00, 0.00, 1),
(70, 3, 1, '454.00', 'Elections: Publications', 5, 1, 0.00, 0.00, 1),
(71, 3, 1, '455.00', 'Elections: Supplies', 5, 1, 0.00, 0.00, 1),
(72, 3, 1, '456.00', 'Mosquito Spraying', 5, 1, 0.00, 0.00, 1),
(73, 3, 1, '459.00', 'Fire Dept Pmt', 5, 1, 0.00, 0.00, 1),
(74, 3, 1, '460.10', 'Community Service', 5, 1, 0.00, 0.00, 1),
(75, 3, 1, '461.00', 'Publications/Adv', 5, 1, 0.00, 0.00, 1),
(76, 3, 1, '462.00', 'Interest & Penalties', 5, 1, 0.00, 0.00, 1),
(77, 3, 1, '465.00', 'Christmas Lighting Contest', 5, 1, 0.00, 0.00, 1),
(78, 3, 1, '480.00', 'Donations', 5, 1, 0.00, 0.00, 1),
(79, 3, 1, '485.20', 'Miscellaneous', 5, 1, 0.00, 0.00, 1),
(80, 3, 1, '485.30', 'GOCO Consult Exp', 5, 1, 0.00, 0.00, 1),
(81, 3, 1, '485.40', 'Meals', 5, 1, 0.00, 0.00, 1),
(82, 3, 1, '492.00', 'Public Safety', 5, 1, 0.00, 0.00, 1),
(83, 3, 1, '481.00', 'Fema Flood Mapping', 5, 1, 0.00, 0.00, 1),
(84, 3, 1, '410.00', 'Wiley Hay Days Exp', 5, 1, 0.00, 0.00, 1);

-- Highways & Streets Fund Expenditure Accounts
SET IDENTITY_INSERT MunicipalAccounts ON;
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(85, 4, 1, '431.00', 'Supplies/Repairs', 5, 2, 0.00, 0.00, 1),
(86, 4, 1, '431.10', 'Snow and Ice Removal', 5, 2, 0.00, 0.00, 1),
(87, 4, 1, '431.40', 'Salaries', 5, 2, 0.00, 0.00, 1),
(88, 4, 1, '431.41', 'Salaries Parttime', 5, 2, 0.00, 0.00, 1),
(89, 4, 1, '431.50', 'Gravel', 5, 2, 0.00, 0.00, 1),
(90, 4, 1, '431.60', 'Seal Coating', 5, 2, 0.00, 0.00, 1),
(91, 4, 1, '431.61', 'Street Signs', 5, 2, 0.00, 0.00, 1),
(92, 4, 1, '432.20', 'Repairs/Maint: Equip', 5, 2, 0.00, 0.00, 1),
(93, 4, 1, '432.30', 'Street Lighting', 5, 2, 0.00, 0.00, 1),
(94, 4, 1, '432.40', 'Fuel', 5, 2, 0.00, 0.00, 1),
(95, 4, 1, '433.00', 'Shop Supplies', 5, 2, 0.00, 0.00, 1),
(96, 4, 1, '432.51', 'Equipment', 5, 2, 0.00, 0.00, 1),
(97, 4, 1, '412.50', 'Brookside Drive Development', 5, 2, 0.00, 0.00, 1);

-- Culture and Recreation Fund Expenditure Accounts
SET IDENTITY_INSERT MunicipalAccounts ON;
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(98, 5, 1, '451.10', 'Salaries', 5, 3, 0.00, 0.00, 1),
(99, 5, 1, '451.20', 'Salaries Part Time', 5, 3, 0.00, 0.00, 1),
(100, 5, 1, '452.00', 'Supplies/Repairs', 5, 3, 0.00, 0.00, 1),
(101, 5, 1, '452.10', 'Utilites', 5, 3, 0.00, 0.00, 1);

SET IDENTITY_INSERT MunicipalAccounts OFF;
