-- Script to fix NULL values in MunicipalAccounts string columns
-- Run this before or after applying the migration to ensure data consistency
-- Date: 2024-12-01

-- Check for NULL values first (diagnostic)
SELECT 
    'Records with NULL FundDescription' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE FundDescription IS NULL

UNION ALL

SELECT 
    'Records with NULL TypeDescription' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE TypeDescription IS NULL

UNION ALL

SELECT 
    'Records with NULL Name' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE Name IS NULL

UNION ALL

SELECT 
    'Records with NULL AccountNumber' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE AccountNumber IS NULL;

-- Fix NULL values with sensible defaults
-- FundDescription: Default to the Fund enum value or 'General Fund'
UPDATE MunicipalAccounts 
SET FundDescription = CASE Fund
    WHEN 0 THEN 'General Fund'
    WHEN 1 THEN 'Special Revenue'
    WHEN 2 THEN 'Debt Service'
    WHEN 3 THEN 'Capital Projects'
    WHEN 4 THEN 'Enterprise'
    WHEN 5 THEN 'Internal Service'
    WHEN 6 THEN 'Trust'
    WHEN 7 THEN 'Agency'
    ELSE 'General Fund'
END
WHERE FundDescription IS NULL;

-- TypeDescription: Default to the Type enum value or 'Asset'
UPDATE MunicipalAccounts 
SET TypeDescription = CASE Type
    WHEN 0 THEN 'Cash'
    WHEN 1 THEN 'Investments'
    WHEN 2 THEN 'Receivables'
    WHEN 3 THEN 'Inventory'
    WHEN 4 THEN 'Prepaid'
    WHEN 5 THEN 'FixedAssets'
    WHEN 6 THEN 'Payables'
    WHEN 7 THEN 'AccruedLiabilities'
    WHEN 8 THEN 'LongTermDebt'
    WHEN 9 THEN 'DeferredRevenue'
    WHEN 10 THEN 'FundBalance'
    WHEN 11 THEN 'Revenue'
    WHEN 12 THEN 'Expenditure'
    WHEN 13 THEN 'Transfer'
    ELSE 'Asset'
END
WHERE TypeDescription IS NULL;

-- Name: Default to 'Unnamed Account'
UPDATE MunicipalAccounts 
SET Name = 'Unnamed Account'
WHERE Name IS NULL;

-- AccountNumber: Default to a placeholder (this should be rare)
UPDATE MunicipalAccounts 
SET AccountNumber = '000'
WHERE AccountNumber IS NULL;

-- Verify fixes
SELECT 
    'After Fix - NULL FundDescription' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE FundDescription IS NULL

UNION ALL

SELECT 
    'After Fix - NULL TypeDescription' AS Issue,
    COUNT(*) AS Count
FROM MunicipalAccounts 
WHERE TypeDescription IS NULL;

PRINT 'NULL value fixes applied successfully.';
