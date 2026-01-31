-- Migration: Add MappedDepartment column to TownOfWileyBudget2026
-- Purpose: Enables mapping of budget line items to logical departments based on description
-- Created: 2026-01-22
-- Dependencies: TownOfWileyBudget2026 table must exist (from TownOfWileyBudget2026_Import.sql)

-- ============================================================================
-- STEP 1: Add MappedDepartment column (if not already present)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'TownOfWileyBudget2026' 
    AND COLUMN_NAME = 'MappedDepartment'
)
BEGIN
    ALTER TABLE dbo.TownOfWileyBudget2026
    ADD MappedDepartment NVARCHAR(100) NULL;
    
    PRINT 'Column MappedDepartment added to TownOfWileyBudget2026';
END
ELSE
BEGIN
    PRINT 'Column MappedDepartment already exists in TownOfWileyBudget2026';
END
GO

-- ============================================================================
-- STEP 2: Populate MappedDepartment with intelligent department mapping
-- ============================================================================
-- This mapping uses the Description field to infer department assignment.
-- Rules are based on common keywords in budget line descriptions:
--   - SEWER: Sewer Department
--   - WATER: Water Department
--   - TRASH: Trash/Sanitation Department
--   - CAPITAL OUTLAY: Capital Projects
--   - INSURANCE/AUDIT/LEGAL/OFFICE: Administration
--   - Default: Unmapped (for manual review)
-- ============================================================================

UPDATE dbo.TownOfWileyBudget2026
SET MappedDepartment = 
    CASE 
        -- Sewer-related
        WHEN Description LIKE '%SEWER%' 
          OR Description LIKE '%LIFT-STATION%' 
          OR Description LIKE '%SEWAGE%' 
          OR Description LIKE '%SEWER CLEANING%' 
          THEN 'Sewer'

        -- Water-related (most lines appear to be shared, but we catch the obvious ones)
        WHEN Description LIKE '%WATER%' 
          OR Description LIKE '%PUMP%' 
          OR Description LIKE '%WELL%' 
          THEN 'Water'

        -- Trash / Solid Waste
        WHEN Description LIKE '%TRASH%' 
          OR Description LIKE '%DUMP%' 
          OR Description LIKE '%PU USAGE%' 
          OR Description LIKE '%DISPOSAL%' 
          THEN 'Trash'

        -- Capital / Infrastructure (very large line – often separate)
        WHEN Description LIKE '%CAPITAL OUTLAY%' 
          THEN 'Capital Projects'

        -- Admin / Overhead (common municipal catch-all)
        WHEN Description LIKE '%BANK%' 
          OR Description LIKE '%AUDIT%' 
          OR Description LIKE '%LEGAL%' 
          OR Description LIKE '%OFFICE%' 
          OR Description LIKE '%INSURANCE%' 
          OR Description LIKE '%DUES%' 
          OR Description LIKE '%EDUCATION%' 
          OR Description LIKE '%TREASURER%' 
          OR Description LIKE '%MISC%' 
          THEN 'Administration'

        -- Revenue lines that don't fit elsewhere
        WHEN Description LIKE '%GRANT%' 
          OR Description LIKE '%INTEREST%' 
          OR Description LIKE '%TAX%'
          THEN 'General Revenue'

        ELSE 'Unmapped / Other'
    END
WHERE MappedDepartment IS NULL
   OR MappedDepartment = '';

PRINT 'MappedDepartment column populated based on description patterns';

-- ============================================================================
-- STEP 3: Verification - Show mapping results
-- ============================================================================
DECLARE @TotalRecords INT;
DECLARE @MappedRecords INT;
DECLARE @UnmappedRecords INT;

SELECT @TotalRecords = COUNT(*) FROM dbo.TownOfWileyBudget2026;
SELECT @MappedRecords = COUNT(*) FROM dbo.TownOfWileyBudget2026 WHERE MappedDepartment IS NOT NULL AND MappedDepartment != 'Unmapped';
SELECT @UnmappedRecords = COUNT(*) FROM dbo.TownOfWileyBudget2026 WHERE MappedDepartment = 'Unmapped' OR MappedDepartment IS NULL;

PRINT '===== Migration Summary =====';
PRINT 'Total Records: ' + CAST(@TotalRecords AS NVARCHAR(10));
PRINT 'Mapped Records: ' + CAST(@MappedRecords AS NVARCHAR(10));
PRINT 'Unmapped Records: ' + CAST(@UnmappedRecords AS NVARCHAR(10));

-- Show distribution by mapped department
PRINT '';
PRINT '===== Distribution by MappedDepartment =====';
SELECT MappedDepartment, COUNT(*) as RecordCount
FROM dbo.TownOfWileyBudget2026
GROUP BY MappedDepartment
ORDER BY RecordCount DESC;

-- ============================================================================
-- STEP 4: Show sample unmapped records for manual review (if any)
-- ============================================================================
IF @UnmappedRecords > 0
BEGIN
    PRINT '';
    PRINT '===== Sample Unmapped Records (first 10) =====';
    PRINT 'These records need manual review and possible mapping to departments:';
    SELECT TOP 10 
        Id, 
        Description, 
        MappedDepartment, 
        FundOrDepartment
    FROM dbo.TownOfWileyBudget2026
    WHERE MappedDepartment = 'Unmapped' OR MappedDepartment IS NULL
    ORDER BY Id;
END
GO
