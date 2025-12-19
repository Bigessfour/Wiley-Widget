-- Create FY 2025 Budget Period
SET IDENTITY_INSERT BudgetPeriods ON;

INSERT INTO BudgetPeriods (Id, Year, Name, CreatedDate, Status, StartDate, EndDate, IsActive) VALUES
(1, 2025, 'FY 2025 Town of Wiley Budget', GETDATE(), 2, '2025-01-01', '2025-12-31', 1);

SET IDENTITY_INSERT BudgetPeriods OFF;
