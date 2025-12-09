using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    public partial class AddActivityLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ActivityLogs' AND s.name = 'dbo')
BEGIN
    CREATE TABLE dbo.ActivityLogs
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ActivityType NVARCHAR(100) NOT NULL,
        Activity NVARCHAR(200) NOT NULL,
        Details NVARCHAR(1000) NULL,
        [User] NVARCHAR(100) NOT NULL DEFAULT 'System',
        EntityId NVARCHAR(100) NULL,
        EntityType NVARCHAR(100) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Success',
        DurationMs BIGINT NULL,
        Source NVARCHAR(200) NULL,
        MetadataJson NVARCHAR(MAX) NULL,
        Severity NVARCHAR(50) NOT NULL DEFAULT 'Info',
        IsArchived BIT NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_ActivityLogs_Timestamp ON dbo.ActivityLogs (Timestamp DESC);
    CREATE INDEX IX_ActivityLogs_ActivityType ON dbo.ActivityLogs (ActivityType);
    CREATE INDEX IX_ActivityLogs_User ON dbo.ActivityLogs ([User]);
    CREATE INDEX IX_ActivityLogs_Entity ON dbo.ActivityLogs (EntityType, EntityId);
    CREATE INDEX IX_ActivityLogs_IsArchived_FilterED ON dbo.ActivityLogs (IsArchived) WHERE IsArchived = 0;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ActivityLogs' AND s.name = 'dbo')
BEGIN
    DROP TABLE dbo.ActivityLogs;
END");
        }
    }
}
