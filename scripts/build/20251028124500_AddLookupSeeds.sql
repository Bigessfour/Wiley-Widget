-- Idempotent seed SQL for AddLookupSeeds migration (copied from migration Up method)
-- Departments
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Departments')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'CULT')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('CULT', 'Culture and Recreation', NULL);
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'SAN')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('SAN', 'Sanitation', NULL);
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'UTIL')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('UTIL', 'Utilities', NULL);
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'COMM')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('COMM', 'Community Center', NULL);
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'CONS')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('CONS', 'Conservation', NULL);
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE DepartmentCode = 'REC')
        INSERT INTO Departments (DepartmentCode, Name, ParentId) VALUES ('REC', 'Recreation', NULL);
END
GO

-- Funds
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Funds')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Funds WHERE FundCode = '300-UTIL')
        INSERT INTO Funds (FundCode, Name, Type) VALUES ('300-UTIL', 'Utility Fund', 2);
    IF NOT EXISTS (SELECT 1 FROM Funds WHERE FundCode = '400-COMM')
        INSERT INTO Funds (FundCode, Name, Type) VALUES ('400-COMM', 'Community Center Fund', 3);
    IF NOT EXISTS (SELECT 1 FROM Funds WHERE FundCode = '500-CONS')
        INSERT INTO Funds (FundCode, Name, Type) VALUES ('500-CONS', 'Conservation Trust Fund', 6);
    IF NOT EXISTS (SELECT 1 FROM Funds WHERE FundCode = '600-REC')
        INSERT INTO Funds (FundCode, Name, Type) VALUES ('600-REC', 'Recreation Fund', 3);
END
GO

-- Vendors
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Vendor')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Acme Supplies')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('contact@acmesupplies.example.com', 1, 'Acme Supplies');
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Municipal Services Co.')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('info@muniservices.example.com', 1, 'Municipal Services Co.');
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Trail Builders LLC')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('projects@trailbuilders.example.com', 1, 'Trail Builders LLC');
END
GO

-- AppSettings
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppSettings')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM AppSettings WHERE Id = 1)
    BEGIN
        IF (
            (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppSettings' AND COLUMN_NAME IN (
                'Theme','EnableDataCaching','CacheExpirationMinutes','SelectedLogLevel','EnableFileLogging','LogFilePath','QuickBooksEnvironment','QboTokenExpiry','LastSelectedEnterpriseId'
            )) = 9
        )
        BEGIN
            IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.name = 'AppSettings' AND c.name = 'Id' AND c.is_identity = 1
            )
            BEGIN
                INSERT INTO AppSettings (Theme, UseDynamicColumns, EnableDataCaching, CacheExpirationMinutes, SelectedLogLevel, EnableFileLogging, LogFilePath, QuickBooksEnvironment, QboTokenExpiry, LastSelectedEnterpriseId, IncludeChartsInReports)
                VALUES ('FluentDark', 0, 1, 30, 'Information', 1, 'logs/wiley-widget.log', 'sandbox', '2026-01-01', 1, 0);
            END
            ELSE
            BEGIN
                INSERT INTO AppSettings (Id, Theme, UseDynamicColumns, EnableDataCaching, CacheExpirationMinutes, SelectedLogLevel, EnableFileLogging, LogFilePath, QuickBooksEnvironment, QboTokenExpiry, LastSelectedEnterpriseId, IncludeChartsInReports)
                VALUES (1, 'FluentDark', 0, 1, 30, 'Information', 1, 'logs/wiley-widget.log', 'sandbox', '2026-01-01', 1, 0);
            END
        END
    END
END
GO
