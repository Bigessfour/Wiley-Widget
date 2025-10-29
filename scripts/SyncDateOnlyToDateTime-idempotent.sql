IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [AppSettings] (
        [Id] int NOT NULL IDENTITY,
        [Theme] nvarchar(max) NOT NULL,
        [WindowWidth] float NULL,
        [WindowHeight] float NULL,
        [WindowLeft] float NULL,
        [WindowTop] float NULL,
        [WindowMaximized] bit NULL,
        [UseDynamicColumns] bit NOT NULL,
        [QuickBooksAccessToken] nvarchar(max) NULL,
        [QuickBooksRefreshToken] nvarchar(max) NULL,
        [QuickBooksRealmId] nvarchar(max) NULL,
        [QuickBooksEnvironment] nvarchar(max) NOT NULL,
        [QuickBooksTokenExpiresUtc] datetime2 NULL,
        [QboAccessToken] nvarchar(max) NULL,
        [QboRefreshToken] nvarchar(max) NULL,
        [QboTokenExpiry] datetime2 NOT NULL,
        CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [BudgetPeriods] (
        [Id] int NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_BudgetPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Fund] nvarchar(450) NOT NULL,
        [ParentDepartmentId] int NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Enterprises] (
        [Id] int NOT NULL IDENTITY,
        [RowVersion] rowversion NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CurrentRate] decimal(18,2) NOT NULL,
        [MonthlyExpenses] decimal(18,2) NOT NULL,
        [CitizenCount] int NOT NULL,
        [TotalBudget] decimal(18,2) NOT NULL,
        [BudgetAmount] decimal(18,2) NOT NULL,
        [LastModified] datetime2 NULL,
        [Type] nvarchar(50) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [MeterReading] decimal(18,2) NULL,
        [MeterReadDate] datetime2 NULL,
        [PreviousMeterReading] decimal(18,2) NULL,
        [PreviousMeterReadDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ModifiedDate] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedDate] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Enterprises] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [FiscalYearSettings] (
        [Id] int NOT NULL IDENTITY,
        [FiscalYearStartMonth] int NOT NULL,
        [FiscalYearStartDay] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [LastModified] datetime2 NOT NULL,
        CONSTRAINT [PK_FiscalYearSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [OverallBudgets] (
        [Id] int NOT NULL IDENTITY,
        [SnapshotDate] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        [TotalMonthlyRevenue] decimal(18,2) NOT NULL,
        [TotalMonthlyExpenses] decimal(18,2) NOT NULL,
        [TotalMonthlyBalance] decimal(18,2) NOT NULL,
        [TotalCitizensServed] int NOT NULL,
        [AverageRatePerCitizen] decimal(18,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [IsCurrent] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_OverallBudgets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [UtilityCustomers] (
        [Id] int NOT NULL IDENTITY,
        [RowVersion] rowversion NOT NULL,
        [AccountNumber] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [CompanyName] nvarchar(100) NULL,
        [CustomerType] nvarchar(450) NOT NULL,
        [ServiceAddress] nvarchar(200) NOT NULL,
        [ServiceCity] nvarchar(50) NOT NULL,
        [ServiceState] nvarchar(2) NOT NULL,
        [ServiceZipCode] nvarchar(10) NOT NULL,
        [MailingAddress] nvarchar(200) NULL,
        [MailingCity] nvarchar(50) NULL,
        [MailingState] nvarchar(2) NULL,
        [MailingZipCode] nvarchar(10) NULL,
        [PhoneNumber] nvarchar(15) NULL,
        [EmailAddress] nvarchar(100) NULL,
        [MeterNumber] nvarchar(20) NULL,
        [ServiceLocation] nvarchar(450) NOT NULL,
        [Status] nvarchar(450) NOT NULL DEFAULT N'Active',
        [AccountOpenDate] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        [AccountCloseDate] datetime2 NULL,
        [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TaxId] nvarchar(20) NULL,
        [BusinessLicenseNumber] nvarchar(20) NULL,
        [Notes] nvarchar(500) NULL,
        [ConnectDate] datetime2 NULL,
        [DisconnectDate] datetime2 NULL,
        [LastPaymentAmount] decimal(18,2) NOT NULL,
        [LastPaymentDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        [LastModifiedDate] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT [PK_UtilityCustomers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Vendors] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ContactInfo] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Vendors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Widgets] (
        [Id] int NOT NULL IDENTITY,
        [RowVersion] rowversion NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Price] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedDate] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        [ModifiedDate] datetime2 NULL DEFAULT (CURRENT_TIMESTAMP),
        [Category] nvarchar(50) NULL,
        [SKU] nvarchar(20) NULL,
        CONSTRAINT [PK_Widgets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [MunicipalAccounts] (
        [Id] int NOT NULL IDENTITY,
        [RowVersion] rowversion NOT NULL,
        [AccountNumber] nvarchar(20) NOT NULL,
        [FundClass] nvarchar(max) NOT NULL,
        [DepartmentId] int NOT NULL,
        [ParentAccountId] int NULL,
        [BudgetPeriodId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Type] nvarchar(450) NOT NULL,
        [Fund] nvarchar(450) NOT NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [BudgetAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [QuickBooksId] nvarchar(50) NULL,
        [LastSyncDate] datetime2 NULL,
        [Notes] nvarchar(200) NULL,
        CONSTRAINT [PK_MunicipalAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId] FOREIGN KEY ([BudgetPeriodId]) REFERENCES [BudgetPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [BudgetInteractions] (
        [Id] int NOT NULL IDENTITY,
        [PrimaryEnterpriseId] int NOT NULL,
        [SecondaryEnterpriseId] int NULL,
        [InteractionType] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [MonthlyAmount] decimal(18,2) NOT NULL,
        [InteractionDate] datetime2 NOT NULL,
        [IsCost] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Notes] nvarchar(300) NULL,
        CONSTRAINT [PK_BudgetInteractions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId] FOREIGN KEY ([PrimaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId] FOREIGN KEY ([SecondaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [BudgetEntries] (
        [Id] int NOT NULL IDENTITY,
        [MunicipalAccountId] int NOT NULL,
        [BudgetPeriodId] int NOT NULL,
        [YearType] int NOT NULL,
        [EntryType] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [Notes] nvarchar(200) NULL,
        CONSTRAINT [PK_BudgetEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BudgetEntries_BudgetPeriods_BudgetPeriodId] FOREIGN KEY ([BudgetPeriodId]) REFERENCES [BudgetPeriods] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [VendorId] int NOT NULL,
        [MunicipalAccountId] int NOT NULL,
        [InvoiceNumber] nvarchar(50) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [IsPaid] bit NOT NULL,
        [PaymentDate] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Invoices_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] int NOT NULL IDENTITY,
        [MunicipalAccountId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [Type] int NOT NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_BudgetPeriodId] ON [BudgetEntries] ([BudgetPeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetInteractions_InteractionType] ON [BudgetInteractions] ([InteractionType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetInteractions_PrimaryEnterpriseId] ON [BudgetInteractions] ([PrimaryEnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetInteractions_SecondaryEnterpriseId] ON [BudgetInteractions] ([SecondaryEnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Status] ON [BudgetPeriods] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year] ON [BudgetPeriods] ([Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year_Status] ON [BudgetPeriods] ([Year], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Departments_Fund] ON [Departments] ([Fund]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Departments_Name] ON [Departments] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Enterprises_Name] ON [Enterprises] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Invoices_MunicipalAccountId] ON [Invoices] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Invoices_VendorId] ON [Invoices] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MunicipalAccounts_AccountNumber] ON [MunicipalAccounts] ([AccountNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_BudgetPeriodId] ON [MunicipalAccounts] ([BudgetPeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_DepartmentId] ON [MunicipalAccounts] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_Fund] ON [MunicipalAccounts] ([Fund]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_IsActive] ON [MunicipalAccounts] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_ParentAccountId] ON [MunicipalAccounts] ([ParentAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_QuickBooksId] ON [MunicipalAccounts] ([QuickBooksId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_Type] ON [MunicipalAccounts] ([Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OverallBudgets_IsCurrent] ON [OverallBudgets] ([IsCurrent]) WHERE IsCurrent = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_OverallBudgets_SnapshotDate] ON [OverallBudgets] ([SnapshotDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Transactions_MunicipalAccountId] ON [Transactions] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UtilityCustomers_AccountNumber] ON [UtilityCustomers] ([AccountNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_UtilityCustomers_CustomerType] ON [UtilityCustomers] ([CustomerType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_UtilityCustomers_EmailAddress] ON [UtilityCustomers] ([EmailAddress]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_UtilityCustomers_MeterNumber] ON [UtilityCustomers] ([MeterNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_UtilityCustomers_ServiceLocation] ON [UtilityCustomers] ([ServiceLocation]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_UtilityCustomers_Status] ON [UtilityCustomers] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Widgets_Category] ON [Widgets] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Widgets_CreatedDate] ON [Widgets] ([CreatedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Widgets_IsActive] ON [Widgets] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    CREATE INDEX [IX_Widgets_Name] ON [Widgets] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Widgets_SKU] ON [Widgets] ([SKU]) WHERE [SKU] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012223946_AddAppSettingsEntity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251012223946_AddAppSettingsEntity', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [CacheExpirationMinutes] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableDataCaching] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableFileLogging] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LogFilePath] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [SelectedLogLevel] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012224059_AddAdvancedSettingsToAppSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251012224059_AddAdvancedSettingsToAppSettings', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_BudgetPeriods_BudgetPeriodId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteractions] DROP CONSTRAINT [FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteractions] DROP CONSTRAINT [FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Departments] DROP CONSTRAINT [FK_Departments_Departments_ParentDepartmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoices] DROP CONSTRAINT [FK_Invoices_MunicipalAccounts_MunicipalAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoices] DROP CONSTRAINT [FK_Invoices_Vendors_VendorId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] DROP CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP TABLE [OverallBudgets];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP TABLE [Widgets];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_AccountNumber] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_CustomerType] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_EmailAddress] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_MeterNumber] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_ServiceLocation] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_UtilityCustomers_Status] ON [UtilityCustomers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_MunicipalAccounts_AccountNumber] ON [MunicipalAccounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_MunicipalAccounts_Fund] ON [MunicipalAccounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_MunicipalAccounts_IsActive] ON [MunicipalAccounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_MunicipalAccounts_QuickBooksId] ON [MunicipalAccounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_MunicipalAccounts_Type] ON [MunicipalAccounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_Enterprises_Name] ON [Enterprises];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_Departments_Code] ON [Departments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_Departments_Fund] ON [Departments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_Departments_Name] ON [Departments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Vendors] DROP CONSTRAINT [PK_Vendors];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoices] DROP CONSTRAINT [PK_Invoices];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetPeriods] DROP CONSTRAINT [PK_BudgetPeriods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_BudgetPeriods_Status] ON [BudgetPeriods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_BudgetPeriods_Year] ON [BudgetPeriods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_BudgetPeriods_Year_Status] ON [BudgetPeriods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteractions] DROP CONSTRAINT [PK_BudgetInteractions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DROP INDEX [IX_BudgetInteractions_InteractionType] ON [BudgetInteractions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'AccountNumber');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [MunicipalAccounts] DROP COLUMN [AccountNumber];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Departments]') AND [c].[name] = N'Code');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Departments] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Departments] DROP COLUMN [Code];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Departments]') AND [c].[name] = N'Fund');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Departments] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Departments] DROP COLUMN [Fund];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetEntries]') AND [c].[name] = N'Notes');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [BudgetEntries] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [BudgetEntries] DROP COLUMN [Notes];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Vendors]', N'Vendor', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Invoices]', N'Invoice', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetPeriods]', N'BudgetPeriod', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetInteractions]', N'BudgetInteraction', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Departments].[ParentDepartmentId]', N'ParentId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Departments].[IX_Departments_ParentDepartmentId]', N'IX_Departments_ParentId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[YearType]', N'FundType', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[EntryType]', N'FiscalYear', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[CreatedDate]', N'CreatedAt', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[BudgetPeriodId]', N'DepartmentId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[Amount]', N'Variance', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetEntries].[IX_BudgetEntries_BudgetPeriodId]', N'IX_BudgetEntries_DepartmentId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Invoice].[IX_Invoices_VendorId]', N'IX_Invoice_VendorId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[Invoice].[IX_Invoices_MunicipalAccountId]', N'IX_Invoice_MunicipalAccountId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetInteraction].[IX_BudgetInteractions_SecondaryEnterpriseId]', N'IX_BudgetInteraction_SecondaryEnterpriseId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC sp_rename N'[BudgetInteraction].[IX_BudgetInteractions_PrimaryEnterpriseId]', N'IX_BudgetInteraction_PrimaryEnterpriseId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'Status');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [UtilityCustomers] ALTER COLUMN [Status] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'ServiceLocation');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [UtilityCustomers] ALTER COLUMN [ServiceLocation] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'LastModifiedDate');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var6 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'CustomerType');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [UtilityCustomers] ALTER COLUMN [CustomerType] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'CurrentBalance');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var8 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'CreatedDate');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var9 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UtilityCustomers]') AND [c].[name] = N'AccountOpenDate');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [UtilityCustomers] DROP CONSTRAINT [' + @var10 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transactions]') AND [c].[name] = N'Type');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Transactions] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [Transactions] ALTER COLUMN [Type] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transactions]') AND [c].[name] = N'MunicipalAccountId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Transactions] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Transactions] ALTER COLUMN [MunicipalAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] ADD [BudgetEntryId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'Type');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [MunicipalAccounts] ALTER COLUMN [Type] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'IsActive');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var14 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'FundClass');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [MunicipalAccounts] ALTER COLUMN [FundClass] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'Fund');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [MunicipalAccounts] ALTER COLUMN [Fund] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'BudgetAmount');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var17 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'Balance');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var18 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD [AccountNumber_Value] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Departments] ADD [DepartmentCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetEntries]') AND [c].[name] = N'MunicipalAccountId');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [BudgetEntries] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [BudgetEntries] ALTER COLUMN [MunicipalAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [AccountNumber] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [ActivityCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [ActualAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [BudgetedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [Description] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [EncumbranceAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [EndPeriod] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [FundId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [IsGASBCompliant] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [ParentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [SourceFilePath] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [SourceRowNumber] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [StartPeriod] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetPeriod]') AND [c].[name] = N'Status');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [BudgetPeriod] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [BudgetPeriod] ALTER COLUMN [Status] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetInteraction]') AND [c].[name] = N'IsCost');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [BudgetInteraction] DROP CONSTRAINT [' + @var21 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Vendor] ADD CONSTRAINT [PK_Vendor] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoice] ADD CONSTRAINT [PK_Invoice] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetPeriod] ADD CONSTRAINT [PK_BudgetPeriod] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [PK_BudgetInteraction] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE TABLE [Funds] (
        [Id] int NOT NULL IDENTITY,
        [FundCode] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Type] int NOT NULL,
        CONSTRAINT [PK_Funds] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] ON;
    EXEC(N'INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (1, N''DPW'', N''Public Works'', NULL),
    (2, N''SAN'', N''Sanitation'', 1),
    (3, N''GG'', N''General Government'', NULL),
    (4, N''CUL'', N''Culture & Recreation'', NULL),
    (5, N''CC'', N''Community Center'', NULL),
    (6, N''CT'', N''Conservation Trust'', NULL),
    (7, N''REC'', N''Recreation'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] ON;
    EXEC(N'INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (1, N''100'', N''General Fund'', 1),
    (2, N''200'', N''Utility Fund'', 2),
    (3, N''300'', N''Community Center Fund'', 3),
    (4, N''400'', N''Conservation Trust Fund'', 4),
    (5, N''500'', N''Recreation Fund'', 5)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] ON;
    EXEC(N'INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (1, N''405'', N''GOV'', 0.0, 50000.0, ''2025-10-13T15:21:30.8091044Z'', 1, N''Road Maintenance'', 0.0, ''0001-01-01T00:00:00.0000000'', 2026, 1, 0, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''0001-01-01T00:00:00.0000000'', NULL, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] ON;
    EXEC(N'INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (2, N''SAN'', N''Sanitation'', 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] ON;
    EXEC(N'INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (2, N''405.1'', N''GOV'', 0.0, 20000.0, ''2025-10-13T15:21:30.8092333Z'', 1, N''Paving'', 0.0, ''0001-01-01T00:00:00.0000000'', 2026, 1, 0, CAST(1 AS bit), NULL, 1, NULL, NULL, ''0001-01-01T00:00:00.0000000'', NULL, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'BudgetEntryId', N'CreatedAt', N'Description', N'MunicipalAccountId', N'TransactionDate', N'Type', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Transactions]'))
        SET IDENTITY_INSERT [Transactions] ON;
    EXEC(N'INSERT INTO [Transactions] ([Id], [Amount], [BudgetEntryId], [CreatedAt], [Description], [MunicipalAccountId], [TransactionDate], [Type], [UpdatedAt])
    VALUES (1, 10000.0, 1, ''2025-10-13T15:21:30.8093448Z'', N''Initial payment for road work'', NULL, ''2025-10-13T15:21:30.8094078Z'', N''Payment'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'BudgetEntryId', N'CreatedAt', N'Description', N'MunicipalAccountId', N'TransactionDate', N'Type', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Transactions]'))
        SET IDENTITY_INSERT [Transactions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_Transactions_BudgetEntryId] ON [Transactions] ([BudgetEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_Transactions_TransactionDate] ON [Transactions] ([TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC(N'ALTER TABLE [Transactions] ADD CONSTRAINT [CK_Transaction_NonZero] CHECK ([Amount] != 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Departments_DepartmentCode] ON [Departments] ([DepartmentCode]) WHERE [DepartmentCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BudgetEntries_AccountNumber_FiscalYear] ON [BudgetEntries] ([AccountNumber], [FiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_ActivityCode] ON [BudgetEntries] ([ActivityCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_FundId] ON [BudgetEntries] ([FundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_ParentId] ON [BudgetEntries] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_SourceRowNumber] ON [BudgetEntries] ([SourceRowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    EXEC(N'ALTER TABLE [BudgetEntries] ADD CONSTRAINT [CK_Budget_Positive] CHECK ([BudgetedAmount] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_BudgetEntries_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_Funds_FundId] FOREIGN KEY ([FundId]) REFERENCES [Funds] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId] FOREIGN KEY ([PrimaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId] FOREIGN KEY ([SecondaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoice] ADD CONSTRAINT [FK_Invoice_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Invoice] ADD CONSTRAINT [FK_Invoice_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId] FOREIGN KEY ([BudgetPeriodId]) REFERENCES [BudgetPeriod] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [MunicipalAccounts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] ADD CONSTRAINT [FK_Transactions_BudgetEntries_BudgetEntryId] FOREIGN KEY ([BudgetEntryId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    ALTER TABLE [Transactions] ADD CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251013152132_AddBackendEnhancements', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_BudgetEntries_ParentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_Departments_DepartmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_Funds_FundId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] DROP CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] DROP CONSTRAINT [FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] DROP CONSTRAINT [FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Invoice_MunicipalAccounts_MunicipalAccountId')
    BEGIN
        ALTER TABLE [Invoice] DROP CONSTRAINT [FK_Invoice_MunicipalAccounts_MunicipalAccountId];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Invoice_Vendor_VendorId')
    BEGIN
        ALTER TABLE [Invoice] DROP CONSTRAINT [FK_Invoice_Vendor_VendorId];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId')
    BEGIN
        ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Transactions] DROP CONSTRAINT [FK_Transactions_BudgetEntries_BudgetEntryId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Transactions] DROP CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF OBJECT_ID(N'Invoice','U') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.key_constraints WHERE name = 'PK_Invoice')
    BEGIN
        ALTER TABLE [Invoice] DROP CONSTRAINT [PK_Invoice];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF OBJECT_ID(N'BudgetPeriod','U') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.key_constraints WHERE name = 'PK_BudgetPeriod')
    BEGIN
        ALTER TABLE [BudgetPeriod] DROP CONSTRAINT [PK_BudgetPeriod];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [BudgetEntries]
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [Departments]
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [Funds]
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [Transactions]
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [BudgetEntries]
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [Departments]
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC(N'DELETE FROM [Funds]
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF OBJECT_ID(N'Invoice','U') IS NOT NULL
    BEGIN
        EXEC sp_rename N'Invoice', N'Invoices';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF OBJECT_ID(N'dbo.BudgetPeriod','U') IS NOT NULL
    BEGIN
        EXEC sp_rename N'dbo.BudgetPeriod', N'BudgetPeriods';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    IF OBJECT_ID(N'dbo.MunicipalAccounts','U') IS NOT NULL AND COL_NAME('dbo.MunicipalAccounts','AccountNumber_Value') IS NOT NULL
    BEGIN
        EXEC sp_rename N'dbo.MunicipalAccounts.AccountNumber_Value', N'AccountNumber', 'COLUMN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC sp_rename N'[Invoices].[IX_Invoice_VendorId]', N'IX_Invoices_VendorId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    EXEC sp_rename N'[Invoices].[IX_Invoice_MunicipalAccountId]', N'IX_Invoices_MunicipalAccountId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'FundClass');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [MunicipalAccounts] ALTER COLUMN [FundClass] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'AccountNumber');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [MunicipalAccounts] ALTER COLUMN [AccountNumber] nvarchar(20) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Enterprises] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Enterprises] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD [EnterpriseId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [IncludeChartsInReports] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastReportEndDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastReportStartDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedEnterpriseId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedFormat] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedReportType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [QboClientId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [QboClientSecret] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetPeriods] ADD CONSTRAINT [PK_BudgetPeriods] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] int NOT NULL IDENTITY,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [User] nvarchar(max) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [Changes] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_Fund_Type] ON [MunicipalAccounts] ([Fund], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_BudgetInteraction_EnterpriseId] ON [BudgetInteraction] ([EnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_Invoices_InvoiceDate] ON [Invoices] ([InvoiceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_IsActive] ON [BudgetPeriods] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year] ON [BudgetPeriods] ([Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year_Status] ON [BudgetPeriods] ([Year], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_BudgetEntries_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_Funds_FundId] FOREIGN KEY ([FundId]) REFERENCES [Funds] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetEntries] ADD CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [FK_BudgetInteraction_Enterprises_EnterpriseId] FOREIGN KEY ([EnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId] FOREIGN KEY ([PrimaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [BudgetInteraction] ADD CONSTRAINT [FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId] FOREIGN KEY ([SecondaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId] FOREIGN KEY ([BudgetPeriodId]) REFERENCES [BudgetPeriods] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Transactions] ADD CONSTRAINT [FK_Transactions_BudgetEntries_BudgetEntryId] FOREIGN KEY ([BudgetEntryId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    ALTER TABLE [Transactions] ADD CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021042555_SyncDateOnlyToDateTime'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251021042555_SyncDateOnlyToDateTime', N'9.0.10');
END;

COMMIT;
GO

