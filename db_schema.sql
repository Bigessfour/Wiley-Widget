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
        [Status] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_BudgetPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BudgetPeriods_Status] CHECK ([Status] BETWEEN 0 AND 3)
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
        [Fund] int NOT NULL,
        [ParentDepartmentId] int NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Departments_Fund] CHECK ([Fund] BETWEEN 0 AND 13),
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
        [Type] int NOT NULL,
        [Fund] int NOT NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [BudgetAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [QuickBooksId] nvarchar(50) NULL,
        [LastSyncDate] datetime2 NULL,
        [Notes] nvarchar(200) NULL,
        CONSTRAINT [PK_MunicipalAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MunicipalAccounts_Type] CHECK ([Type] BETWEEN 0 AND 31),
        CONSTRAINT [CK_MunicipalAccounts_Fund] CHECK ([Fund] BETWEEN 0 AND 13),
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
    VALUES (N'20251012223946_AddAppSettingsEntity', N'9.0.0');
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
    VALUES (N'20251012224059_AddAdvancedSettingsToAppSettings', N'9.0.0');
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
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'AccountNumber');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var0 + '];');
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
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_MunicipalAccounts_Type' AND parent_object_id = OBJECT_ID(N'[MunicipalAccounts]'))
    BEGIN
        ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [CK_MunicipalAccounts_Type] CHECK ([Type] BETWEEN 0 AND 31);
    END;
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
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_MunicipalAccounts_Fund' AND parent_object_id = OBJECT_ID(N'[MunicipalAccounts]'))
    BEGIN
        ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [CK_MunicipalAccounts_Fund] CHECK ([Fund] BETWEEN 0 AND 13);
    END;
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
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = N'CK_BudgetPeriod_Status' AND parent_object_id = OBJECT_ID(N'[BudgetPeriod]'))
    BEGIN
        ALTER TABLE [BudgetPeriod] ADD CONSTRAINT [CK_BudgetPeriod_Status] CHECK ([Status] BETWEEN 0 AND 3);
    END;
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

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (1, N'DPW', N'Public Works', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (1, N'100', N'General Fund', 1);
        SET IDENTITY_INSERT [Funds] OFF;
    END
    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (2, N'200', N'Utility Fund', 2);
        SET IDENTITY_INSERT [Funds] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251013152132_AddBackendEnhancements'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (1, N'405', N'GOV', 0, 50000, '2025-10-13T15:21:30.8091044Z', 1, N'Road Maintenance', 0, '0001-01-01T00:00:00.0000000', 2026, 1, 0, 1, NULL, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', NULL, 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (2, N'SAN', N'Sanitation', 1);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (2, N'405.1', N'GOV', 0, 20000, '2025-10-13T15:21:30.8092333Z', 1, N'Paving', 0, '0001-01-01T00:00:00.0000000', 2026, 1, 0, 1, NULL, 1, NULL, NULL, '0001-01-01T00:00:00.0000000', NULL, 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

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
    VALUES (N'20251013152132_AddBackendEnhancements', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedEnterpriseId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [IncludeChartsInReports] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastReportStartDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastReportEndDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedFormat] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [LastSelectedReportType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021134644_AddQboClientColumnsToAppSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251021134644_AddQboClientColumnsToAppSettings', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021231427_SeedConservationAccounts'
)
BEGIN
    IF OBJECT_ID(N'[BudgetPeriods]', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [BudgetPeriods] WHERE [Id] = 1)
        BEGIN
            SET IDENTITY_INSERT [BudgetPeriods] ON;
            INSERT INTO [BudgetPeriods] ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
            VALUES (1, 2026, N'2026', '2025-10-21T23:14:27.0000000Z', 2, '2026-01-01T00:00:00.0000000', '2026-12-31T00:00:00.0000000', CAST(1 AS bit));
            SET IDENTITY_INSERT [BudgetPeriods] OFF;
        END
    END
    ELSE IF OBJECT_ID(N'[BudgetPeriod]', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 1)
        BEGIN
            SET IDENTITY_INSERT [BudgetPeriod] ON;
            INSERT INTO [BudgetPeriod] ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
            VALUES (1, 2026, N'2026', '2025-10-21T23:14:27.0000000Z', 2, '2026-01-01T00:00:00.0000000', '2026-12-31T00:00:00.0000000', CAST(1 AS bit));
            SET IDENTITY_INSERT [BudgetPeriod] OFF;
        END
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021231427_SeedConservationAccounts'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber_Value', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundClass', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
    EXEC(N'INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundClass], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type])
    VALUES (1, N''110'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''CASH IN BANK'', NULL, NULL, NULL, 0),
    (2, N''110.1'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''CASH-BASEBALL FIELD PROJECT'', NULL, NULL, NULL, 0),
    (3, N''120'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''INVESTMENTS'', NULL, NULL, NULL, 1),
    (4, N''130'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''INTERGOVERNMENTAL RECEIVABLE'', NULL, NULL, NULL, 2),
    (5, N''140'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''GRANT RECEIVABLE'', NULL, NULL, NULL, 2),
    (6, N''210'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''ACCOUNTS PAYABLE'', NULL, NULL, NULL, 6),
    (7, N''211'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''BASEBALL FIELD PROJECT LOAN'', NULL, NULL, NULL, 7),
    (8, N''212'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''WALKING TRAIL LOAN'', NULL, NULL, NULL, 7),
    (9, N''230'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''DUE TO/FROM TOW GENERAL FUND'', NULL, NULL, NULL, 8),
    (10, N''240'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''DUE TO/FROM TOW UTILITY FUND'', NULL, NULL, NULL, 8),
    (11, N''290'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''FUND BALANCE'', NULL, NULL, NULL, 10),
    (12, N''3000'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''Opening Bal Equity'', NULL, NULL, NULL, 9),
    (13, N''33000'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''Retained Earnings'', NULL, NULL, NULL, 9),
    (14, N''310'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''STATE APPORTIONMENT'', NULL, NULL, NULL, 16),
    (15, N''314'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''WALKING TRAIL DONATION'', NULL, NULL, NULL, 13),
    (16, N''315'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''BASEBALL FIELD DONATIONS'', NULL, NULL, NULL, 13),
    (17, N''320'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''GRANT REVENUES'', NULL, NULL, NULL, 13),
    (18, N''323'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''MISC REVENUE'', NULL, NULL, NULL, 16),
    (19, N''325'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''WALKING TRAIL REVENUE'', NULL, NULL, NULL, 16),
    (20, N''360'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''INTEREST ON INVESTMENTS'', NULL, NULL, NULL, 14),
    (21, N''370'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''TRANSFER FROM REC FUND'', NULL, NULL, NULL, 30),
    (22, N''2111'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''BALLFIELD ACCRUED INTEREST'', NULL, NULL, NULL, 24),
    (23, N''2112'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''WALKING TRAIL ACCRUED INTEREST'', NULL, NULL, NULL, 24),
    (24, N''410'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''CAPITAL IMP - BALL COMPLEX'', NULL, NULL, NULL, 29),
    (25, N''420'', 0.0, 0.0, 1, 1, 8, 2, CAST(1 AS bit), NULL, N''PARKS - DEVELOPMENT'', NULL, NULL, NULL, 29)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber_Value', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundClass', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251021231427_SeedConservationAccounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251021231427_SeedConservationAccounts', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MunicipalAccounts]') AND [c].[name] = N'FundClass');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [MunicipalAccounts] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [MunicipalAccounts] DROP COLUMN [FundClass];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD [FundDescription] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD [TypeDescription] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundDescription] = N''General Fund'', [TypeDescription] = N''Asset''
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251022200702_AddMunicipalAccountDescriptions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251022200702_AddMunicipalAccountDescriptions', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    DROP INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries];
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetEntries]') AND [c].[name] = N'MunicipalAccountId');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [BudgetEntries] DROP CONSTRAINT [' + @var23 + '];');
    EXEC(N'UPDATE [BudgetEntries] SET [MunicipalAccountId] = 1 WHERE [MunicipalAccountId] IS NULL');
    ALTER TABLE [BudgetEntries] ALTER COLUMN [MunicipalAccountId] int NOT NULL;
    ALTER TABLE [BudgetEntries] ADD DEFAULT 1 FOR [MunicipalAccountId];
    CREATE INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [AutoSaveIntervalMinutes] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [CurrencyFormat] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [CurrentFiscalYear] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [DatabaseName] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [DatabaseServer] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [DateFormat] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [DefaultLanguage] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableAI] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableAutoSave] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableNotifications] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableQuickBooksSync] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [EnableSounds] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalPeriod] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalQuarter] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalYearEnd] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalYearStart] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalYearStartDay] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [FiscalYearStartMonth] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [QuickBooksCompanyFile] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [QuickBooksRedirectUri] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [SessionTimeoutMinutes] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [SyncIntervalMinutes] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [UseFiscalYearForReporting] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiApiEndpoint] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiApiKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiMaxTokens] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiModel] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiTemperature] float NOT NULL DEFAULT 0.0E0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    ALTER TABLE [AppSettings] ADD [XaiTimeout] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE TABLE [TaxRevenueSummaries] (
        [Id] int NOT NULL IDENTITY,
        [Description] nvarchar(200) NOT NULL,
        [PriorYearLevy] decimal(19,4) NOT NULL,
        [PriorYearAmount] decimal(19,4) NOT NULL,
        [CurrentYearLevy] decimal(19,4) NOT NULL,
        [CurrentYearAmount] decimal(19,4) NOT NULL,
        [BudgetYearLevy] decimal(19,4) NOT NULL,
        [BudgetYearAmount] decimal(19,4) NOT NULL,
        [IncDecLevy] decimal(19,4) NOT NULL,
        [IncDecAmount] decimal(19,4) NOT NULL,
        CONSTRAINT [PK_TaxRevenueSummaries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE TABLE [UtilityBills] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [BillNumber] nvarchar(50) NOT NULL,
        [BillDate] datetime2 NOT NULL,
        [DueDate] datetime2 NOT NULL,
        [PeriodStartDate] datetime2 NOT NULL,
        [PeriodEndDate] datetime2 NOT NULL,
        [WaterCharges] decimal(18,2) NOT NULL DEFAULT 0.0,
        [SewerCharges] decimal(18,2) NOT NULL DEFAULT 0.0,
        [GarbageCharges] decimal(18,2) NOT NULL DEFAULT 0.0,
        [StormwaterCharges] decimal(18,2) NOT NULL DEFAULT 0.0,
        [LateFees] decimal(18,2) NOT NULL DEFAULT 0.0,
        [OtherCharges] decimal(18,2) NOT NULL DEFAULT 0.0,
        [AmountPaid] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Status] int NOT NULL,
        [PaidDate] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [WaterUsageGallons] int NOT NULL,
        [PreviousMeterReading] int NOT NULL,
        [CurrentMeterReading] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedDate] datetime2 NULL,
        CONSTRAINT [PK_UtilityBills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UtilityBills_UtilityCustomers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [UtilityCustomers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE TABLE [Charges] (
        [Id] int NOT NULL IDENTITY,
        [BillId] int NOT NULL,
        [ChargeType] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Rate] decimal(18,4) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedDate] datetime2 NOT NULL,
        [UtilityBillId] int NULL,
        CONSTRAINT [PK_Charges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Charges_UtilityBills_BillId] FOREIGN KEY ([BillId]) REFERENCES [UtilityBills] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Charges_UtilityBills_UtilityBillId] FOREIGN KEY ([UtilityBillId]) REFERENCES [UtilityBills] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [AppSettings] ON;
        INSERT INTO [AppSettings] (
            [Id], [Theme], [UseDynamicColumns], [QuickBooksEnvironment],
            [QboAccessToken], [QboRefreshToken], [QboTokenExpiry],
            [EnableFileLogging], [EnableDataCaching], [LogFilePath], [CacheExpirationMinutes], [SelectedLogLevel],
            [LastSelectedEnterpriseId], [IncludeChartsInReports]
        )
        VALUES (
            1, N'Light', 0, N'Production',
            NULL, NULL, '0001-01-01T00:00:00.0000000',
            1, 1, N'logs/wiley-widget.log', 30, N'Information',
            0, 0
        );
        SET IDENTITY_INSERT [AppSettings] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    EXEC(N'UPDATE [AppSettings] SET [AutoSaveIntervalMinutes] = 5, [CurrencyFormat] = N''USD'', [CurrentFiscalYear] = N''2024-2025'', [DatabaseName] = N''WileyWidget'', [DatabaseServer] = N''localhost'', [DateFormat] = N''MM/dd/yyyy'', [DefaultLanguage] = N''en-US'', [EnableAI] = CAST(0 AS bit), [EnableAutoSave] = CAST(1 AS bit), [EnableNotifications] = CAST(1 AS bit), [EnableQuickBooksSync] = CAST(0 AS bit), [EnableSounds] = CAST(1 AS bit), [FiscalPeriod] = N''Q1'', [FiscalQuarter] = 1, [FiscalYearEnd] = N''June 30'', [FiscalYearStart] = N''July 1'', [FiscalYearStartDay] = 1, [FiscalYearStartMonth] = 7, [SessionTimeoutMinutes] = 60, [SyncIntervalMinutes] = 30, [UseFiscalYearForReporting] = CAST(1 AS bit), [XaiApiEndpoint] = N''https://api.x.ai/v1'', [XaiMaxTokens] = 2000, [XaiModel] = N''grok-4-0709'', [XaiTemperature] = 0.69999999999999996E0, [XaiTimeout] = 30
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [BudgetPeriod] ON;
        INSERT INTO [BudgetPeriod] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
        VALUES (1, '2024-10-28T00:00:00.0000000Z', '2025-12-31T23:59:59.0000000Z', 1, N'2025 Current', '2025-01-01T00:00:00.0000000Z', 1, 2025);
        SET IDENTITY_INSERT [BudgetPeriod] OFF;
    END
    IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [BudgetPeriod] ON;
        INSERT INTO [BudgetPeriod] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
        VALUES (2, '2025-10-28T00:00:00.0000000Z', '2026-12-31T23:59:59.0000000Z', 0, N'2026 Proposed', '2026-01-01T00:00:00.0000000Z', 1, 2026);
        SET IDENTITY_INSERT [BudgetPeriod] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    -- Update existing departments if present, or insert new ones
    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (1, N'ADMIN', N'Administration', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END
    ELSE
    BEGIN
        UPDATE [Departments] SET [DepartmentCode] = N'ADMIN', [Name] = N'Administration' WHERE [Id] = 1;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (2, N'DPW', N'Public Works', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 3)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (3, N'CULT', N'Culture and Recreation', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 5)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (5, N'UTIL', N'Utilities', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 6)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (6, N'COMM', N'Community Center', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 7)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (7, N'CONS', N'Conservation', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 8)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (8, N'REC', N'Recreation', NULL);
        SET IDENTITY_INSERT [Departments] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (1, N'100-GEN', N'General Fund', 1);
        SET IDENTITY_INSERT [Funds] OFF;
    END
    ELSE
    BEGIN
        UPDATE [Funds] SET [FundCode] = N'100-GEN', [Name] = N'General Fund' WHERE [Id] = 1;
    END

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (2, N'200-ENT', N'Enterprise Fund', 2);
        SET IDENTITY_INSERT [Funds] OFF;
    END
    ELSE
    BEGIN
        UPDATE [Funds] SET [FundCode] = N'200-ENT', [Name] = N'Enterprise Fund' WHERE [Id] = 2;
    END

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 3)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (3, N'300-UTIL', N'Utility Fund', 2);
        SET IDENTITY_INSERT [Funds] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 4)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (4, N'400-COMM', N'Community Center Fund', 3);
        SET IDENTITY_INSERT [Funds] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 5)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (5, N'500-CONS', N'Conservation Trust Fund', 6);
        SET IDENTITY_INSERT [Funds] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 6)
    BEGIN
        SET IDENTITY_INSERT [Funds] ON;
        INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
        VALUES (6, N'600-REC', N'Recreation Fund', 3);
        SET IDENTITY_INSERT [Funds] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BudgetYearAmount', N'BudgetYearLevy', N'CurrentYearAmount', N'CurrentYearLevy', N'Description', N'IncDecAmount', N'IncDecLevy', N'PriorYearAmount', N'PriorYearLevy') AND [object_id] = OBJECT_ID(N'[TaxRevenueSummaries]'))
        SET IDENTITY_INSERT [TaxRevenueSummaries] ON;
    EXEC(N'INSERT INTO [TaxRevenueSummaries] ([Id], [BudgetYearAmount], [BudgetYearLevy], [CurrentYearAmount], [CurrentYearLevy], [Description], [IncDecAmount], [IncDecLevy], [PriorYearAmount], [PriorYearLevy])
    VALUES (1, 1880448.0, 1880448.0, 1072691.0, 1072691.0, N''ASSESSED VALUATION-COUNTY FUND'', 807757.0, 807757.0, 1069780.0, 1069780.0),
    (2, 85692.0, 45.57, 48883.0, 45.57, N''GENERAL'', 36809.0, 0.0, 48750.0, 45.57),
    (3, 0.0, 0.0, 0.0, 0.0, N''UTILITY'', 0.0, 0.0, 0.0, 0.0),
    (4, 0.0, 0.0, 0.0, 0.0, N''COMMUNITY CENTER'', 0.0, 0.0, 0.0, 0.0),
    (5, 0.0, 0.0, 0.0, 0.0, N''CONSERVATION TRUST FUND'', 0.0, 0.0, 0.0, 0.0),
    (6, 0.0, 0.0, 0.0, 0.0, N''TEMPORARY MILL LEVY CREDIT'', 0.0, 0.0, 0.0, 0.0),
    (7, 85692.0, 45.57, 48883.0, 45.57, N''TOTAL'', 36810.0, 0.0, 48750.0, 45.57)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BudgetYearAmount', N'BudgetYearLevy', N'CurrentYearAmount', N'CurrentYearLevy', N'Description', N'IncDecAmount', N'IncDecLevy', N'PriorYearAmount', N'PriorYearLevy') AND [object_id] = OBJECT_ID(N'[TaxRevenueSummaries]'))
        SET IDENTITY_INSERT [TaxRevenueSummaries] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [Vendor] ON;
        INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
        VALUES (1, N'contact@acmesupplies.example.com', 1, N'Acme Supplies');
        SET IDENTITY_INSERT [Vendor] OFF;
    END
    IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [Vendor] ON;
        INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
        VALUES (2, N'info@muniservices.example.com', 1, N'Municipal Services Co.');
        SET IDENTITY_INSERT [Vendor] OFF;
    END
    IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 3)
    BEGIN
        SET IDENTITY_INSERT [Vendor] ON;
        INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
        VALUES (3, N'projects@trailbuilders.example.com', 1, N'Trail Builders LLC');
        SET IDENTITY_INSERT [Vendor] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    -- BudgetEntries Id 1-2 may exist from AddBackendEnhancements - update or skip
    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (1, N'332.1', NULL, 0, 360, '2025-10-28', 1, N'Federal: Mineral Lease', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 2)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (2, N'333.00', NULL, 0, 240, '2025-10-28', 1, N'State: Cigarette Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    -- BudgetEntries Id 3-20 are new
    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 3)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (3, N'334.31', NULL, 0, 18153, '2025-10-28', 1, N'Highways Users', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 4)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (4, N'313.00', NULL, 0, 1775, '2025-10-28', 1, N'Additional MV', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 5)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (5, N'337.17', NULL, 0, 1460, '2025-10-28', 1, N'County Road & Bridge', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 6)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (6, N'311.20', NULL, 0, 1500, '2025-10-28', 1, N'Senior Homestead Exemption', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 7)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (7, N'312.00', NULL, 0, 5100, '2025-10-28', 1, N'Specific Ownership Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 8)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (8, N'314.00', NULL, 0, 2500, '2025-10-28', 1, N'Tax A', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 9)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (9, N'319.00', NULL, 0, 35, '2025-10-28', 1, N'Penalties & Interest on Delinquent Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 10)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (10, N'336.00', NULL, 0, 120000, '2025-10-28', 1, N'Sales Tax', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 11)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (11, N'318.20', NULL, 0, 7058, '2025-10-28', 1, N'Franchise Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 12)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (12, N'322.70', NULL, 0, 50, '2025-10-28', 1, N'Animal Licenses', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 13)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (13, N'310.00', NULL, 0, 6000, '2025-10-28', 1, N'Charges for Services: WSD Collection Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 14)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (14, N'370.00', NULL, 0, 12000, '2025-10-28', 1, N'Housing Authority Mgt Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 15)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (15, N'373.00', NULL, 0, 2400, '2025-10-28', 1, N'Pickup Usage Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 16)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (16, N'361.00', NULL, 0, 325, '2025-10-28', 1, N'Miscellaneous Receipts: Interest Earnings', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 17)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (17, N'365.00', NULL, 0, 100, '2025-10-28', 1, N'Dividends', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 18)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (18, N'363.00', NULL, 0, 1100, '2025-10-28', 1, N'Lease', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 19)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (19, N'350.00', NULL, 0, 10000, '2025-10-28', 1, N'Wiley Hay Days Donations', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 20)
    BEGIN
        SET IDENTITY_INSERT [BudgetEntries] ON;
        INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
        VALUES (20, N'362.00', NULL, 0, 2500, '2025-10-28', 1, N'Donations', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
        SET IDENTITY_INSERT [BudgetEntries] OFF;
    END

    -- Department Id=4 (child of 2)
    IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 4)
    BEGIN
        SET IDENTITY_INSERT [Departments] ON;
        INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
        VALUES (4, N'SAN', N'Sanitation', 2);
        SET IDENTITY_INSERT [Departments] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (1, N'100', 0, 0, 1, 1, 1, N'General Fund', 1, NULL, N'GENERAL ACCOUNT', NULL, NULL, NULL, 1, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 26)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (26, N'425', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'MISC EXPENSE', NULL, NULL, NULL, 24, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 27)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (27, N'430', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'TRAIL MAINTENANCE', NULL, NULL, NULL, 24, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 28)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (28, N'435', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'PARK IMPROVEMENTS', NULL, NULL, NULL, 29, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 29)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (29, N'440', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'EQUIPMENT PURCHASES', NULL, NULL, NULL, 29, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 30)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (30, N'445', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'PROJECTS - SMALL', NULL, NULL, NULL, 24, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

    IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 31)
    BEGIN
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
        INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
        VALUES (31, N'450', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'RESERVES ALLOCATION', NULL, NULL, NULL, 30, N'Asset', 0);
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
    END

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_Charges_BillId] ON [Charges] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_Charges_ChargeType] ON [Charges] ([ChargeType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_Charges_UtilityBillId] ON [Charges] ([UtilityBillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_BillDate] ON [UtilityBills] ([BillDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UtilityBills_BillNumber] ON [UtilityBills] ([BillNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_CustomerId] ON [UtilityBills] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_DueDate] ON [UtilityBills] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_Status] ON [UtilityBills] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251129182650_CapturePendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251129182650_CapturePendingModelChanges', N'9.0.0');
END;

COMMIT;
GO

