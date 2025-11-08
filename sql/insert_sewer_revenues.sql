-- Insert Sewer Enterprise Fund Revenues for FY 2026
SET IDENTITY_INSERT BudgetEntries ON;
INSERT INTO BudgetEntries (Id, MunicipalAccountId, BudgetPeriodId, YearType, EntryType, Amount, CreatedDate, Notes) VALUES
(41, 102, 1, 0, 0, 5725427.00, GETDATE(), 'Grant'),
(42, 103, 1, 0, 0, 150000.00, GETDATE(), 'Sewage Sales'),
(43, 104, 1, 0, 0, 2100.00, GETDATE(), 'Interest on Investments'),
(44, 105, 1, 0, 0, 2000.00, GETDATE(), 'Misc');
SET IDENTITY_INSERT BudgetEntries OFF;
