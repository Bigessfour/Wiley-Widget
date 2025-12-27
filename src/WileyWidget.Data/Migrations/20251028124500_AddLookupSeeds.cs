using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Idempotent inserts: use conditional SQL so applying this migration multiple
            // times (or against a DB with overlapping data) does not fail.

            // Departments: insert by DepartmentCode to avoid PK conflicts with existing Ids
            migrationBuilder.Sql(@"
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
");

            // Funds: insert by FundCode
            migrationBuilder.Sql(@"
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
");

            // Vendors: insert by Name to avoid duplicate entries
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Vendor')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Acme Supplies')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('contact@acmesupplies.example.com', 1, 'Acme Supplies');
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Municipal Services Co.')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('info@muniservices.example.com', 1, 'Municipal Services Co.');
    IF NOT EXISTS (SELECT 1 FROM Vendor WHERE Name = 'Trail Builders LLC')
        INSERT INTO Vendor (ContactInfo, IsActive, Name) VALUES ('projects@trailbuilders.example.com', 1, 'Trail Builders LLC');
END
");

            // AppSettings: only insert if the table and the expected columns exist and the row is missing
            migrationBuilder.Sql(@"
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
            INSERT INTO AppSettings (Id, Theme, EnableDataCaching, CacheExpirationMinutes, SelectedLogLevel, EnableFileLogging, LogFilePath, QuickBooksEnvironment, QboTokenExpiry, LastSelectedEnterpriseId)
            VALUES (1, 'FluentDark', 1, 30, 'Information', 1, 'logs/wiley-widget.log', 'sandbox', '2026-01-01', 1);
        END
    END
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Delete by unique keys where possible to make Down idempotent and safe
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppSettings')
BEGIN
    DELETE FROM AppSettings WHERE Id = 1;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Vendor')
BEGIN
    DELETE FROM Vendor WHERE Name IN ('Acme Supplies','Municipal Services Co.','Trail Builders LLC');
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Funds')
BEGIN
    DELETE FROM Funds WHERE FundCode IN ('300-UTIL','400-COMM','500-CONS','600-REC');
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Departments')
BEGIN
    DELETE FROM Departments WHERE DepartmentCode IN ('CULT','SAN','UTIL','COMM','CONS','REC');
END
");
        }
    }
}
