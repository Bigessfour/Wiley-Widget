/*
    create_activitylogs.sql

    Creates the dbo.ActivityLogs table (if it doesn't already exist) and adds
    indexes matching the EF model configuration. This script is intended to be
    run manually in environments where you prefer to apply schema changes
    outside of EF migrations (e.g., emergency or one-off patches).

    NOTE: Keeping this script in the repo helps documentation / CI teams.
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ActivityLogs' AND s.name = 'dbo')
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
    -- Filtered index for non-archived rows (keeps the index small)
    CREATE INDEX IX_ActivityLogs_IsArchived_Filtered ON dbo.ActivityLogs (IsArchived) WHERE IsArchived = 0;
END
