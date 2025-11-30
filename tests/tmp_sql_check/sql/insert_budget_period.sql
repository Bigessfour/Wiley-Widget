-- Create FY 2026 Budget Period
SET IDENTITY_INSERT BudgetPeriods ON;

INSERT INTO BudgetPeriods (Id, Year, Name, CreatedDate, Status, StartDate, EndDate, IsActive) VALUES
(1, 2026, 'FY 2026 Town of Wiley Budget', GETDATE(), 2, '2026-01-01', '2026-12-31', 1);

SET IDENTITY_INSERT BudgetPeriods OFF;
