-- Link BudgetEntries to MunicipalAccounts based on AccountNumber
UPDATE be
SET be.MunicipalAccountId = ma.Id
FROM BudgetEntries be
INNER JOIN MunicipalAccounts ma ON be.AccountNumber = ma.AccountNumber
WHERE be.MunicipalAccountId IS NULL;