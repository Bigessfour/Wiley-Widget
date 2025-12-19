-- Insert Sewer Enterprise Fund Expenditures for FY 2026
SET IDENTITY_INSERT BudgetEntries ON;
INSERT INTO BudgetEntries (Id, AccountNumber, Description, BudgetedAmount, ActualAmount, Variance, FiscalYear, StartPeriod, EndPeriod, FundType, DepartmentId, MunicipalAccountId, CreatedAt) VALUES
(26, '200-101', 'Outside Service Lab Fees', 700.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 106, GETDATE()),
(27, '200-102', 'Budget, Audit, Legal', 18000.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 107, GETDATE()),
(28, '200-103', 'Office Supplies/Postage', 1800.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 108, GETDATE()),
(29, '200-104', 'Education', 100.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 109, GETDATE()),
(30, '200-105', 'Dues & Subs', 200.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 110, GETDATE()),
(31, '200-106', 'Lift Station - Utilities', 2500.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 111, GETDATE()),
(32, '200-107', 'Collection Fee', 6600.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 112, GETDATE()),
(33, '200-108', 'Supplies and Expenses', 2000.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 113, GETDATE()),
(34, '200-109', 'Insurance', 7500.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 114, GETDATE()),
(35, '200-110', 'Sewer Cleaning', 7600.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 115, GETDATE()),
(36, '200-111', 'Property Taxes', 7.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 116, GETDATE()),
(37, '200-112', 'Backhoe Repairs', 300.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 117, GETDATE()),
(38, '200-113', 'PU Usage Fee', 2400.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 118, GETDATE()),
(39, '200-114', 'Fuel', 2000.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 119, GETDATE()),
(40, '200-115', 'Capital Outlay', 5725427.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 120, GETDATE());
SET IDENTITY_INSERT BudgetEntries OFF;
