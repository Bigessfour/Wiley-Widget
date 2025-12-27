-- Idempotent script: update short numeric MigrationId entries to full migration filenames
-- Safe: only updates rows where full migration id does not already exist

DECLARE @map TABLE (ShortId nvarchar(50), FullId nvarchar(200), ProductVersion nvarchar(50));

INSERT INTO @map (ShortId, FullId, ProductVersion) VALUES
('20251013153901', '20251013153901_FixBudgetEntrySelfReference', '9.0.8'),
('20251028124500', '20251028124500_AddLookupSeeds', '9.0.8');

DECLARE @s nvarchar(50), @f nvarchar(200), @pv nvarchar(50);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT ShortId, FullId, ProductVersion FROM @map;

OPEN cur;
FETCH NEXT FROM cur INTO @s, @f, @pv;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @s)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @f)
        BEGIN
            UPDATE dbo.__EFMigrationsHistory SET MigrationId = @f, ProductVersion = @pv WHERE MigrationId = @s;
            PRINT 'Updated ' + @s + ' -> ' + @f;
        END
        ELSE
        BEGIN
            PRINT 'Full id ' + @f + ' already exists; leaving short id ' + @s + ' intact.';
        END
    END
    ELSE
    BEGIN
        PRINT 'Short id ' + @s + ' not present; skipping.';
    END

    FETCH NEXT FROM cur INTO @s, @f, @pv;
END

CLOSE cur;
DEALLOCATE cur;
GO
