-- Seed Sewer Enterprise Fund MunicipalAccounts
-- This script creates municipal accounts for the sewer enterprise fund

SET IDENTITY_INSERT MunicipalAccounts ON;

-- Sewer Enterprise Fund Revenue Accounts
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(102, 1, 1, '200-201', 'Proprietary', 'Grant', 4, 2, 0.00, 0.00, 1),
(103, 1, 1, '200-202', 'Proprietary', 'Sewage Sales', 4, 2, 0.00, 0.00, 1),
(104, 1, 1, '200-203', 'Proprietary', 'Interest on Investments', 4, 2, 0.00, 0.00, 1),
(105, 1, 1, '200-204', 'Proprietary', 'Misc', 4, 2, 0.00, 0.00, 1);

-- Sewer Enterprise Fund Expenditure Accounts
INSERT INTO MunicipalAccounts (Id, DepartmentId, BudgetPeriodId, AccountNumber, FundClass, Name, Type, Fund, Balance, BudgetAmount, IsActive) VALUES
(106, 1, 1, '200-101', 'Proprietary', 'Outside Service Lab Fees', 5, 2, 0.00, 0.00, 1),
(107, 1, 1, '200-102', 'Proprietary', 'Budget, Audit, Legal', 5, 2, 0.00, 0.00, 1),
(108, 1, 1, '200-103', 'Proprietary', 'Office Supplies/Postage', 5, 2, 0.00, 0.00, 1),
(109, 1, 1, '200-104', 'Proprietary', 'Education', 5, 2, 0.00, 0.00, 1),
(110, 1, 1, '200-105', 'Proprietary', 'Dues & Subs', 5, 2, 0.00, 0.00, 1),
(111, 1, 1, '200-106', 'Proprietary', 'Lift Station - Utilities', 5, 2, 0.00, 0.00, 1),
(112, 1, 1, '200-107', 'Proprietary', 'Collection Fee', 5, 2, 0.00, 0.00, 1),
(113, 1, 1, '200-108', 'Proprietary', 'Supplies and Expenses', 5, 2, 0.00, 0.00, 1),
(114, 1, 1, '200-109', 'Proprietary', 'Insurance', 5, 2, 0.00, 0.00, 1),
(115, 1, 1, '200-110', 'Proprietary', 'Sewer Cleaning', 5, 2, 0.00, 0.00, 1),
(116, 1, 1, '200-111', 'Proprietary', 'Property Taxes', 5, 2, 0.00, 0.00, 1),
(117, 1, 1, '200-112', 'Proprietary', 'Backhoe Repairs', 5, 2, 0.00, 0.00, 1),
(118, 1, 1, '200-113', 'Proprietary', 'PU Usage Fee', 5, 2, 0.00, 0.00, 1),
(119, 1, 1, '200-114', 'Proprietary', 'Fuel', 5, 2, 0.00, 0.00, 1),
(120, 1, 1, '200-115', 'Proprietary', 'Capital Outlay', 5, 2, 0.00, 0.00, 1);

SET IDENTITY_INSERT MunicipalAccounts OFF;
