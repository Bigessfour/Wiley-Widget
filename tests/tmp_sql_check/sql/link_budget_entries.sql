-- Link BudgetEntries to MunicipalAccounts based on AccountNumber
UPDATE be
SET be.MunicipalAccountId = ma.Id
FROM BudgetEntries be
-- MunicipalAccount uses an owned AccountNumber mapped to backing/computed column AccountNumber_Value.
-- Match against the persisted backing column when linking via SQL scripts.
INNER JOIN MunicipalAccounts ma ON be.AccountNumber = ma.AccountNumber_Value
WHERE be.MunicipalAccountId IS NULL;
