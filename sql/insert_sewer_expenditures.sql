-- Insert Sewer Enterprise Fund Expenditures for FY 2026
SET IDENTITY_INSERT BudgetEntries ON;
INSERT INTO BudgetEntries (Id, MunicipalAccountId, BudgetPeriodId, YearType, EntryType, Amount, CreatedDate, Notes) VALUES
(26, 106, 1, 0, 1, 700.00, GETDATE(), 'Outside Service Lab Fees'),
(27, 107, 1, 0, 1, 18000.00, GETDATE(), 'Budget, Audit, Legal'),
(28, 108, 1, 0, 1, 1800.00, GETDATE(), 'Office Supplies/Postage'),
(29, 109, 1, 0, 1, 100.00, GETDATE(), 'Education'),
(30, 110, 1, 0, 1, 200.00, GETDATE(), 'Dues & Subs'),
(31, 111, 1, 0, 1, 2500.00, GETDATE(), 'Lift Station - Utilities'),
(32, 112, 1, 0, 1, 6600.00, GETDATE(), 'Collection Fee'),
(33, 113, 1, 0, 1, 2000.00, GETDATE(), 'Supplies and Expenses'),
(34, 114, 1, 0, 1, 7500.00, GETDATE(), 'Insurance'),
(35, 115, 1, 0, 1, 7600.00, GETDATE(), 'Sewer Cleaning'),
(36, 116, 1, 0, 1, 7.00, GETDATE(), 'Property Taxes'),
(37, 117, 1, 0, 1, 300.00, GETDATE(), 'Backhoe Repairs'),
(38, 118, 1, 0, 1, 2400.00, GETDATE(), 'PU Usage Fee'),
(39, 119, 1, 0, 1, 2000.00, GETDATE(), 'Fuel'),
(40, 120, 1, 0, 1, 5725427.00, GETDATE(), 'Capital Outlay');
SET IDENTITY_INSERT BudgetEntries OFF;
