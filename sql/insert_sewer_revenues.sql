-- Insert Sewer Enterprise Fund Revenues for FY 2026
SET IDENTITY_INSERT BudgetEntries ON;
INSERT INTO BudgetEntries (Id, AccountNumber, Description, BudgetedAmount, ActualAmount, Variance, FiscalYear, StartPeriod, EndPeriod, FundType, DepartmentId, MunicipalAccountId, CreatedAt) VALUES
(41, '200-201', 'Grant', 5725427.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 102, GETDATE()),
(42, '200-202', 'Sewage Sales', 150000.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 103, GETDATE()),
(43, '200-203', 'Interest on Investments', 2100.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 104, GETDATE()),
(44, '200-204', 'Misc', 2000.00, 0, 0, 2026, '2026-01-01', '2026-12-31', 1, 1, 105, GETDATE());
SET IDENTITY_INSERT BudgetEntries OFF;
