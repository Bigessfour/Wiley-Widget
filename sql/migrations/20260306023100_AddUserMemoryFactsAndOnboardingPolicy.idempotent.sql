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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [ActivityLog] (
        [Id] int NOT NULL IDENTITY,
        [Timestamp] datetime2 NOT NULL,
        [Activity] nvarchar(200) NOT NULL,
        [Details] nvarchar(500) NOT NULL,
        [User] nvarchar(100) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Icon] nvarchar(100) NOT NULL,
        [ActivityType] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [DurationMs] bigint NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] nvarchar(max) NOT NULL,
        [Severity] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ActivityLog] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
        [DatabaseServer] nvarchar(max) NOT NULL,
        [DatabaseName] nvarchar(max) NOT NULL,
        [QuickBooksCompanyFile] nvarchar(max) NULL,
        [EnableQuickBooksSync] bit NOT NULL,
        [SyncIntervalMinutes] int NOT NULL,
        [QuickBooksRedirectUri] nvarchar(max) NULL,
        [EnableAutoSave] bit NOT NULL,
        [AutoSaveIntervalMinutes] int NOT NULL,
        [ApplicationFont] nvarchar(max) NOT NULL,
        [UseDynamicColumns] bit NOT NULL,
        [EnableDataCaching] bit NOT NULL,
        [CacheExpirationMinutes] int NOT NULL,
        [SelectedLogLevel] nvarchar(max) NOT NULL,
        [EnableFileLogging] bit NOT NULL,
        [LogFilePath] nvarchar(max) NOT NULL,
        [QuickBooksAccessToken] nvarchar(max) NULL,
        [QuickBooksRefreshToken] nvarchar(max) NULL,
        [QuickBooksRealmId] nvarchar(max) NULL,
        [QuickBooksEnvironment] nvarchar(max) NOT NULL,
        [QuickBooksTokenExpiresUtc] datetime2 NULL,
        [QboAccessToken] nvarchar(max) NULL,
        [QboRefreshToken] nvarchar(max) NULL,
        [QboTokenExpiry] datetime2 NOT NULL,
        [QboClientId] nvarchar(max) NULL,
        [QboClientSecret] nvarchar(max) NULL,
        [EnableAI] bit NOT NULL,
        [XaiApiKey] nvarchar(max) NULL,
        [XaiModel] nvarchar(max) NOT NULL,
        [XaiApiEndpoint] nvarchar(max) NOT NULL,
        [XaiTimeout] int NOT NULL,
        [XaiMaxTokens] int NOT NULL,
        [XaiTemperature] float NOT NULL,
        [EnableNotifications] bit NOT NULL,
        [EnableSounds] bit NOT NULL,
        [DefaultLanguage] nvarchar(max) NOT NULL,
        [DateFormat] nvarchar(max) NOT NULL,
        [CurrencyFormat] nvarchar(max) NOT NULL,
        [SessionTimeoutMinutes] int NOT NULL,
        [FiscalYearStart] nvarchar(max) NOT NULL,
        [FiscalYearStartMonth] int NOT NULL,
        [FiscalYearStartDay] int NOT NULL,
        [FiscalYearEnd] nvarchar(max) NOT NULL,
        [CurrentFiscalYear] nvarchar(max) NOT NULL,
        [UseFiscalYearForReporting] bit NOT NULL,
        [FiscalQuarter] int NOT NULL,
        [FiscalPeriod] nvarchar(max) NOT NULL,
        [LastSelectedReportType] nvarchar(max) NULL,
        [LastSelectedFormat] nvarchar(max) NULL,
        [LastReportStartDate] datetime2 NULL,
        [LastReportEndDate] datetime2 NULL,
        [IncludeChartsInReports] bit NOT NULL,
        [LastSelectedEnterpriseId] int NOT NULL,
        CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
        CONSTRAINT [PK_BudgetPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [ConversationHistories] (
        [ConversationId] nvarchar(128) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [MessagesJson] nvarchar(max) NOT NULL,
        [MessageCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ConversationHistories] PRIMARY KEY ([ConversationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [DepartmentCurrentCharges] (
        [Id] int NOT NULL IDENTITY,
        [Department] nvarchar(50) NOT NULL,
        [CurrentCharge] decimal(18,2) NOT NULL,
        [CustomerCount] int NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DepartmentCurrentCharges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [DepartmentGoals] (
        [Id] int NOT NULL IDENTITY,
        [Department] nvarchar(50) NOT NULL,
        [AdjustmentFactor] decimal(18,4) NOT NULL DEFAULT 1.0,
        [TargetProfitMarginPercent] decimal(18,4) NOT NULL,
        [RecommendationText] nvarchar(1000) NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [Source] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DepartmentGoals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ParentId] int NULL,
        [DepartmentCode] nvarchar(20) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedDate] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Enterprises] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [TelemetryLogs] (
        [Id] int NOT NULL IDENTITY,
        [EventType] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NULL,
        [StackTrace] nvarchar(max) NULL,
        [CorrelationId] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [User] nvarchar(max) NULL,
        [SessionId] nvarchar(max) NULL,
        CONSTRAINT [PK_TelemetryLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [TownOfWileyBudgetData] (
        [Id] int NOT NULL IDENTITY,
        [SourceFile] nvarchar(500) NULL,
        [FundOrDepartment] nvarchar(100) NULL,
        [AccountCode] nvarchar(20) NULL,
        [Description] nvarchar(200) NULL,
        [PriorYearActual] decimal(19,4) NULL,
        [SevenMonthActual] decimal(19,4) NULL,
        [EstimateCurrentYr] decimal(19,4) NULL,
        [BudgetYear] decimal(19,4) NULL,
        [ActualYTD] decimal(19,4) NULL,
        [Remaining] decimal(19,4) NULL,
        [PercentOfBudget] int NULL,
        [Category] nvarchar(50) NULL,
        [MappedDepartment] nvarchar(50) NULL,
        CONSTRAINT [PK_TownOfWileyBudgetData] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [UtilityCustomers] (
        [Id] int NOT NULL IDENTITY,
        [RowVersion] rowversion NOT NULL,
        [AccountNumber] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [CompanyName] nvarchar(100) NULL,
        [CustomerType] int NOT NULL,
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
        [ServiceLocation] int NOT NULL,
        [Status] int NOT NULL,
        [AccountOpenDate] datetime2 NOT NULL,
        [AccountCloseDate] datetime2 NULL,
        [CurrentBalance] decimal(18,2) NOT NULL,
        [TaxId] nvarchar(20) NULL,
        [BusinessLicenseNumber] nvarchar(20) NULL,
        [Notes] nvarchar(500) NULL,
        [ConnectDate] datetime2 NULL,
        [DisconnectDate] datetime2 NULL,
        [LastPaymentAmount] decimal(18,2) NOT NULL,
        [LastPaymentDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_UtilityCustomers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [Vendor] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ContactInfo] nvarchar(200) NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(50) NULL,
        [MailingAddressLine1] nvarchar(200) NULL,
        [MailingAddressLine2] nvarchar(200) NULL,
        [MailingAddressCity] nvarchar(100) NULL,
        [MailingAddressState] nvarchar(50) NULL,
        [MailingAddressPostalCode] nvarchar(20) NULL,
        [MailingAddressCountry] nvarchar(100) NULL,
        [QuickBooksId] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Vendor] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [MunicipalAccounts] (
        [Id] int NOT NULL IDENTITY,
        [DepartmentId] int NOT NULL,
        [AccountNumber] nvarchar(20) NOT NULL,
        [ParentAccountId] int NULL,
        [BudgetPeriodId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Type] int NOT NULL,
        [TypeDescription] nvarchar(50) NOT NULL,
        [FundDescription] nvarchar(max) NOT NULL,
        [Fund] int NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [BudgetAmount] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [QuickBooksId] nvarchar(50) NULL,
        [LastSyncDate] datetime2 NULL,
        [Notes] nvarchar(200) NULL,
        [AccountNumber_Value] AS [AccountNumber],
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MunicipalAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId] FOREIGN KEY ([BudgetPeriodId]) REFERENCES [BudgetPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MunicipalAccounts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [BudgetInteraction] (
        [Id] int NOT NULL IDENTITY,
        [PrimaryEnterpriseId] int NOT NULL,
        [SecondaryEnterpriseId] int NULL,
        [InteractionType] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [MonthlyAmount] decimal(18,2) NOT NULL,
        [InteractionDate] datetime2 NOT NULL,
        [IsCost] bit NOT NULL,
        [Notes] nvarchar(300) NULL,
        [EnterpriseId] int NULL,
        CONSTRAINT [PK_BudgetInteraction] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BudgetInteraction_Enterprises_EnterpriseId] FOREIGN KEY ([EnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId] FOREIGN KEY ([PrimaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId] FOREIGN KEY ([SecondaryEnterpriseId]) REFERENCES [Enterprises] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [BudgetEntries] (
        [Id] int NOT NULL IDENTITY,
        [AccountNumber] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [BudgetedAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [ActualAmount] decimal(18,2) NOT NULL,
        [Variance] decimal(18,2) NOT NULL,
        [ParentId] int NULL,
        [FiscalYear] int NOT NULL,
        [StartPeriod] datetime2 NOT NULL,
        [EndPeriod] datetime2 NOT NULL,
        [FundType] int NOT NULL,
        [EncumbranceAmount] decimal(18,2) NOT NULL,
        [IsGASBCompliant] bit NOT NULL,
        [DepartmentId] int NOT NULL,
        [FundId] int NULL,
        [MunicipalAccountId] int NULL,
        [SourceFilePath] nvarchar(500) NULL,
        [SourceRowNumber] int NULL,
        [ActivityCode] nvarchar(10) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BudgetEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Budget_Positive] CHECK ([BudgetedAmount] > 0),
        CONSTRAINT [FK_BudgetEntries_BudgetEntries_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetEntries_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetEntries_Funds_FundId] FOREIGN KEY ([FundId]) REFERENCES [Funds] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
        [Status] nvarchar(50) NOT NULL DEFAULT N'Pending',
        [IsPaid] bit NOT NULL,
        [PaymentDate] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] int NOT NULL IDENTITY,
        [BudgetEntryId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [MunicipalAccountId] int NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Transaction_NonZero] CHECK ([Amount] != 0),
        CONSTRAINT [FK_Transactions_BudgetEntries_BudgetEntryId] FOREIGN KEY ([BudgetEntryId]) REFERENCES [BudgetEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Transactions_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [CheckNumber] nvarchar(20) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Payee] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [MunicipalAccountId] int NULL,
        [VendorId] int NULL,
        [InvoiceId] int NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'Cleared',
        [IsCleared] bit NOT NULL,
        [Memo] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_MunicipalAccounts_MunicipalAccountId] FOREIGN KEY ([MunicipalAccountId]) REFERENCES [MunicipalAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ApplicationFont', N'AutoSaveIntervalMinutes', N'CacheExpirationMinutes', N'CurrencyFormat', N'CurrentFiscalYear', N'DatabaseName', N'DatabaseServer', N'DateFormat', N'DefaultLanguage', N'EnableAI', N'EnableAutoSave', N'EnableDataCaching', N'EnableFileLogging', N'EnableNotifications', N'EnableQuickBooksSync', N'EnableSounds', N'FiscalPeriod', N'FiscalQuarter', N'FiscalYearEnd', N'FiscalYearStart', N'FiscalYearStartDay', N'FiscalYearStartMonth', N'IncludeChartsInReports', N'LastReportEndDate', N'LastReportStartDate', N'LastSelectedEnterpriseId', N'LastSelectedFormat', N'LastSelectedReportType', N'LogFilePath', N'QboAccessToken', N'QboClientId', N'QboClientSecret', N'QboRefreshToken', N'QboTokenExpiry', N'QuickBooksAccessToken', N'QuickBooksCompanyFile', N'QuickBooksEnvironment', N'QuickBooksRealmId', N'QuickBooksRedirectUri', N'QuickBooksRefreshToken', N'QuickBooksTokenExpiresUtc', N'SelectedLogLevel', N'SessionTimeoutMinutes', N'SyncIntervalMinutes', N'Theme', N'UseDynamicColumns', N'UseFiscalYearForReporting', N'WindowHeight', N'WindowLeft', N'WindowMaximized', N'WindowTop', N'WindowWidth', N'XaiApiEndpoint', N'XaiApiKey', N'XaiMaxTokens', N'XaiModel', N'XaiTemperature', N'XaiTimeout') AND [object_id] = OBJECT_ID(N'[AppSettings]'))
        SET IDENTITY_INSERT [AppSettings] ON;
    EXEC(N'INSERT INTO [AppSettings] ([Id], [ApplicationFont], [AutoSaveIntervalMinutes], [CacheExpirationMinutes], [CurrencyFormat], [CurrentFiscalYear], [DatabaseName], [DatabaseServer], [DateFormat], [DefaultLanguage], [EnableAI], [EnableAutoSave], [EnableDataCaching], [EnableFileLogging], [EnableNotifications], [EnableQuickBooksSync], [EnableSounds], [FiscalPeriod], [FiscalQuarter], [FiscalYearEnd], [FiscalYearStart], [FiscalYearStartDay], [FiscalYearStartMonth], [IncludeChartsInReports], [LastReportEndDate], [LastReportStartDate], [LastSelectedEnterpriseId], [LastSelectedFormat], [LastSelectedReportType], [LogFilePath], [QboAccessToken], [QboClientId], [QboClientSecret], [QboRefreshToken], [QboTokenExpiry], [QuickBooksAccessToken], [QuickBooksCompanyFile], [QuickBooksEnvironment], [QuickBooksRealmId], [QuickBooksRedirectUri], [QuickBooksRefreshToken], [QuickBooksTokenExpiresUtc], [SelectedLogLevel], [SessionTimeoutMinutes], [SyncIntervalMinutes], [Theme], [UseDynamicColumns], [UseFiscalYearForReporting], [WindowHeight], [WindowLeft], [WindowMaximized], [WindowTop], [WindowWidth], [XaiApiEndpoint], [XaiApiKey], [XaiMaxTokens], [XaiModel], [XaiTemperature], [XaiTimeout])
    VALUES (1, N''Segoe UI, 9pt'', 5, 30, N''USD'', N''2024-2025'', N''WileyWidget'', N''localhost'', N''MM/dd/yyyy'', N''en-US'', CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''Q1'', 1, N''June 30'', N''July 1'', 1, 7, CAST(1 AS bit), NULL, NULL, 1, NULL, NULL, N''logs/wiley-widget.log'', NULL, NULL, NULL, NULL, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, N''sandbox'', NULL, NULL, NULL, NULL, N''Information'', 60, 30, N''FluentDark'', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, N''https://api.x.ai/v1'', NULL, 2000, N''grok-4.1'', 0.69999999999999996E0, 30)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ApplicationFont', N'AutoSaveIntervalMinutes', N'CacheExpirationMinutes', N'CurrencyFormat', N'CurrentFiscalYear', N'DatabaseName', N'DatabaseServer', N'DateFormat', N'DefaultLanguage', N'EnableAI', N'EnableAutoSave', N'EnableDataCaching', N'EnableFileLogging', N'EnableNotifications', N'EnableQuickBooksSync', N'EnableSounds', N'FiscalPeriod', N'FiscalQuarter', N'FiscalYearEnd', N'FiscalYearStart', N'FiscalYearStartDay', N'FiscalYearStartMonth', N'IncludeChartsInReports', N'LastReportEndDate', N'LastReportStartDate', N'LastSelectedEnterpriseId', N'LastSelectedFormat', N'LastSelectedReportType', N'LogFilePath', N'QboAccessToken', N'QboClientId', N'QboClientSecret', N'QboRefreshToken', N'QboTokenExpiry', N'QuickBooksAccessToken', N'QuickBooksCompanyFile', N'QuickBooksEnvironment', N'QuickBooksRealmId', N'QuickBooksRedirectUri', N'QuickBooksRefreshToken', N'QuickBooksTokenExpiresUtc', N'SelectedLogLevel', N'SessionTimeoutMinutes', N'SyncIntervalMinutes', N'Theme', N'UseDynamicColumns', N'UseFiscalYearForReporting', N'WindowHeight', N'WindowLeft', N'WindowMaximized', N'WindowTop', N'WindowWidth', N'XaiApiEndpoint', N'XaiApiKey', N'XaiMaxTokens', N'XaiModel', N'XaiTemperature', N'XaiTimeout') AND [object_id] = OBJECT_ID(N'[AppSettings]'))
        SET IDENTITY_INSERT [AppSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedDate', N'EndDate', N'IsActive', N'Name', N'StartDate', N'Status', N'Year') AND [object_id] = OBJECT_ID(N'[BudgetPeriods]'))
        SET IDENTITY_INSERT [BudgetPeriods] ON;
    EXEC(N'INSERT INTO [BudgetPeriods] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
    VALUES (1, ''2025-10-01T00:00:00.0000000Z'', ''2025-12-31T23:59:59.0000000Z'', CAST(1 AS bit), N''2025 Adopted'', ''2025-01-01T00:00:00.0000000Z'', 2, 2025),
    (2, ''2025-10-28T00:00:00.0000000Z'', ''2026-12-31T23:59:59.0000000Z'', CAST(0 AS bit), N''2026 Proposed'', ''2026-01-01T00:00:00.0000000Z'', 1, 2026)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedDate', N'EndDate', N'IsActive', N'Name', N'StartDate', N'Status', N'Year') AND [object_id] = OBJECT_ID(N'[BudgetPeriods]'))
        SET IDENTITY_INSERT [BudgetPeriods] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] ON;
    EXEC(N'INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (1, N''ADMIN'', N''Administration'', NULL),
    (2, N''DPW'', N''Public Works'', NULL),
    (3, N''CULT'', N''Culture and Recreation'', NULL),
    (5, N''UTIL'', N''Utilities'', NULL),
    (6, N''COMM'', N''Community Center'', NULL),
    (7, N''CONS'', N''Conservation'', NULL),
    (8, N''REC'', N''Recreation'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] ON;
    EXEC(N'INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (1, N''100-GEN'', N''General Fund'', 1),
    (2, N''200-ENT'', N''Enterprise Fund'', 2),
    (3, N''300-UTIL'', N''Utility Fund'', 2),
    (4, N''400-COMM'', N''Community Center Fund'', 3),
    (5, N''500-CONS'', N''Conservation Trust Fund'', 6),
    (6, N''600-REC'', N''Recreation Fund'', 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
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
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactInfo', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Vendor]'))
        SET IDENTITY_INSERT [Vendor] ON;
    EXEC(N'INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
    VALUES (1, N''contact@acmesupplies.example.com'', CAST(1 AS bit), N''Acme Supplies''),
    (2, N''info@muniservices.example.com'', CAST(1 AS bit), N''Municipal Services Co.''),
    (3, N''projects@trailbuilders.example.com'', CAST(1 AS bit), N''Trail Builders LLC'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactInfo', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Vendor]'))
        SET IDENTITY_INSERT [Vendor] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] ON;
    EXEC(N'INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (1, N''332.1'', NULL, 0.0, 360.0, ''2026-01-01T00:00:00.0000000Z'', 1, N''Federal: Mineral Lease'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2026-01-01T00:00:00.0000000Z'', 0.0),
    (2, N''333.00'', NULL, 0.0, 240.0, ''2025-10-28T00:00:00.0000000'', 1, N''State: Cigarette Taxes'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (3, N''334.31'', NULL, 0.0, 18153.0, ''2025-10-28T00:00:00.0000000'', 1, N''Highways Users'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (4, N''313.00'', NULL, 0.0, 1775.0, ''2025-10-28T00:00:00.0000000'', 1, N''Additional MV'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (5, N''337.17'', NULL, 0.0, 1460.0, ''2025-10-28T00:00:00.0000000'', 1, N''County Road & Bridge'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (6, N''311.20'', NULL, 0.0, 1500.0, ''2025-10-28T00:00:00.0000000'', 1, N''Senior Homestead Exemption'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (7, N''312.00'', NULL, 0.0, 5100.0, ''2025-10-28T00:00:00.0000000'', 1, N''Specific Ownership Taxes'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (8, N''314.00'', NULL, 0.0, 2500.0, ''2025-10-28T00:00:00.0000000'', 1, N''Tax A'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (9, N''319.00'', NULL, 0.0, 35.0, ''2025-10-28T00:00:00.0000000'', 1, N''Penalties & Interest on Delinquent Taxes'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (10, N''336.00'', NULL, 0.0, 120000.0, ''2025-10-28T00:00:00.0000000'', 1, N''Sales Tax'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (11, N''318.20'', NULL, 0.0, 7058.0, ''2025-10-28T00:00:00.0000000'', 1, N''Franchise Fee'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (12, N''322.70'', NULL, 0.0, 50.0, ''2025-10-28T00:00:00.0000000'', 1, N''Animal Licenses'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (13, N''310.00'', NULL, 0.0, 6000.0, ''2025-10-28T00:00:00.0000000'', 1, N''Charges for Services: WSD Collection Fee'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (14, N''370.00'', NULL, 0.0, 12000.0, ''2025-10-28T00:00:00.0000000'', 1, N''Housing Authority Mgt Fee'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (15, N''373.00'', NULL, 0.0, 2400.0, ''2025-10-28T00:00:00.0000000'', 1, N''Pickup Usage Fee'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (16, N''361.00'', NULL, 0.0, 325.0, ''2025-10-28T00:00:00.0000000'', 1, N''Miscellaneous Receipts: Interest Earnings'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (17, N''365.00'', NULL, 0.0, 100.0, ''2025-10-28T00:00:00.0000000'', 1, N''Dividends'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (18, N''363.00'', NULL, 0.0, 1100.0, ''2025-10-28T00:00:00.0000000'', 1, N''Lease'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (19, N''350.00'', NULL, 0.0, 10000.0, ''2025-10-28T00:00:00.0000000'', 1, N''Wiley Hay Days Donations'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0),
    (20, N''362.00'', NULL, 0.0, 2500.0, ''2025-10-28T00:00:00.0000000'', 1, N''Donations'', 0.0, ''2026-06-30T00:00:00.0000000'', 2026, 1, 1, CAST(1 AS bit), NULL, NULL, NULL, NULL, ''2025-07-01T00:00:00.0000000'', ''2025-10-28T00:00:00.0000000'', 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
        SET IDENTITY_INSERT [BudgetEntries] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] ON;
    EXEC(N'INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (4, N''SAN'', N''Sanitation'', 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundDescription', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type', N'TypeDescription') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
        SET IDENTITY_INSERT [MunicipalAccounts] ON;
    EXEC(N'INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription])
    VALUES (1, N''110'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''CASH IN BANK'', NULL, NULL, NULL, 0, N''Asset''),
    (2, N''110.1'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''CASH-BASEBALL FIELD PROJECT'', NULL, NULL, NULL, 0, N''Asset''),
    (3, N''120'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''INVESTMENTS'', NULL, NULL, NULL, 1, N''Asset''),
    (4, N''130'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''INTERGOVERNMENTAL RECEIVABLE'', NULL, NULL, NULL, 2, N''Asset''),
    (5, N''140'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''GRANT RECEIVABLE'', NULL, NULL, NULL, 2, N''Asset''),
    (6, N''210'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''ACCOUNTS PAYABLE'', NULL, NULL, NULL, 6, N''Asset''),
    (7, N''211'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''BASEBALL FIELD PROJECT LOAN'', NULL, NULL, NULL, 7, N''Asset''),
    (8, N''212'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''WALKING TRAIL LOAN'', NULL, NULL, NULL, 7, N''Asset''),
    (9, N''230'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''DUE TO/FROM TOW GENERAL FUND'', NULL, NULL, NULL, 8, N''Asset''),
    (10, N''240'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''DUE TO/FROM TOW UTILITY FUND'', NULL, NULL, NULL, 8, N''Asset''),
    (11, N''290'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''FUND BALANCE'', NULL, NULL, NULL, 10, N''Asset''),
    (12, N''3000'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''Opening Bal Equity'', NULL, NULL, NULL, 9, N''Asset''),
    (13, N''33000'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''Retained Earnings'', NULL, NULL, NULL, 9, N''Asset''),
    (14, N''310'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''STATE APPORTIONMENT'', NULL, NULL, NULL, 16, N''Asset''),
    (15, N''314'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''WALKING TRAIL DONATION'', NULL, NULL, NULL, 13, N''Asset''),
    (16, N''315'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''BASEBALL FIELD DONATIONS'', NULL, NULL, NULL, 13, N''Asset''),
    (17, N''320'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''GRANT REVENUES'', NULL, NULL, NULL, 13, N''Asset''),
    (18, N''323'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''MISC REVENUE'', NULL, NULL, NULL, 16, N''Asset''),
    (19, N''325'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''WALKING TRAIL REVENUE'', NULL, NULL, NULL, 16, N''Asset''),
    (20, N''360'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''INTEREST ON INVESTMENTS'', NULL, NULL, NULL, 14, N''Asset''),
    (21, N''370'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''TRANSFER FROM REC FUND'', NULL, NULL, NULL, 30, N''Asset''),
    (22, N''2111'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''BALLFIELD ACCRUED INTEREST'', NULL, NULL, NULL, 24, N''Asset''),
    (23, N''2112'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''WALKING TRAIL ACCRUED INTEREST'', NULL, NULL, NULL, 24, N''Asset''),
    (24, N''410'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''CAPITAL IMP - BALL COMPLEX'', NULL, NULL, NULL, 29, N''Asset''),
    (25, N''420'', 0.0, 0.0, 1, 1, 8, N''General Fund'', CAST(1 AS bit), NULL, N''PARKS - DEVELOPMENT'', NULL, NULL, NULL, 29, N''Asset''),
    (26, N''425'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''MISC EXPENSE'', NULL, NULL, NULL, 24, N''Asset''),
    (27, N''430'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''TRAIL MAINTENANCE'', NULL, NULL, NULL, 24, N''Asset''),
    (28, N''435'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''PARK IMPROVEMENTS'', NULL, NULL, NULL, 29, N''Asset''),
    (29, N''440'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''EQUIPMENT PURCHASES'', NULL, NULL, NULL, 29, N''Asset''),
    (30, N''445'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''PROJECTS - SMALL'', NULL, NULL, NULL, 24, N''Asset''),
    (31, N''450'', 0.0, 0.0, 1, 1, 8, N''Conservation Trust Fund'', CAST(1 AS bit), NULL, N''RESERVES ALLOCATION'', NULL, NULL, NULL, 30, N''Asset'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundDescription', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type', N'TypeDescription') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
        SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivityLog_Timestamp] ON [ActivityLog] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BudgetEntries_AccountNumber_FiscalYear] ON [BudgetEntries] ([AccountNumber], [FiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_ActivityCode] ON [BudgetEntries] ([ActivityCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_DepartmentId] ON [BudgetEntries] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_FiscalYear] ON [BudgetEntries] ([FiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_FundId] ON [BudgetEntries] ([FundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_ParentId] ON [BudgetEntries] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetEntries_SourceRowNumber] ON [BudgetEntries] ([SourceRowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetInteraction_EnterpriseId] ON [BudgetInteraction] ([EnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetInteraction_PrimaryEnterpriseId] ON [BudgetInteraction] ([PrimaryEnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetInteraction_SecondaryEnterpriseId] ON [BudgetInteraction] ([SecondaryEnterpriseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_IsActive] ON [BudgetPeriods] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year] ON [BudgetPeriods] ([Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetPeriods_Year_Status] ON [BudgetPeriods] ([Year], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Charges_BillId] ON [Charges] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Charges_ChargeType] ON [Charges] ([ChargeType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Charges_UtilityBillId] ON [Charges] ([UtilityBillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConversationHistories_UpdatedAt] ON [ConversationHistories] ([UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DepartmentCurrentCharges_Department] ON [DepartmentCurrentCharges] ([Department]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DepartmentCurrentCharges_IsActive] ON [DepartmentCurrentCharges] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DepartmentGoals_Department_IsActive] ON [DepartmentGoals] ([Department], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Departments_DepartmentCode] ON [Departments] ([DepartmentCode]) WHERE [DepartmentCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentId] ON [Departments] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_InvoiceDate] ON [Invoices] ([InvoiceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_MunicipalAccountId] ON [Invoices] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_VendorId] ON [Invoices] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_BudgetPeriodId] ON [MunicipalAccounts] ([BudgetPeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_DepartmentId] ON [MunicipalAccounts] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_Fund_Type] ON [MunicipalAccounts] ([Fund], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_ParentAccountId] ON [MunicipalAccounts] ([ParentAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_CheckNumber] ON [Payments] ([CheckNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_MunicipalAccountId] ON [Payments] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Payee] ON [Payments] ([Payee]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate] ON [Payments] ([PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Status] ON [Payments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_VendorId] ON [Payments] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TownOfWileyBudgetData_AccountCode] ON [TownOfWileyBudgetData] ([AccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TownOfWileyBudgetData_FundOrDepartment] ON [TownOfWileyBudgetData] ([FundOrDepartment]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TownOfWileyBudgetData_MappedDepartment] ON [TownOfWileyBudgetData] ([MappedDepartment]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Transactions_BudgetEntryId] ON [Transactions] ([BudgetEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Transactions_MunicipalAccountId] ON [Transactions] ([MunicipalAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Transactions_TransactionDate] ON [Transactions] ([TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_BillDate] ON [UtilityBills] ([BillDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UtilityBills_BillNumber] ON [UtilityBills] ([BillNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_CustomerId] ON [UtilityBills] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_DueDate] ON [UtilityBills] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UtilityBills_Status] ON [UtilityBills] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vendor_IsActive] ON [Vendor] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vendor_Name] ON [Vendor] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207002747_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207002747_InitialCreate', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214031052_AddWileySanitationDistrictFund'
)
BEGIN
    EXEC(N'UPDATE [AppSettings] SET [XaiModel] = N''grok-4-1-fast-reasoning''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214031052_AddWileySanitationDistrictFund'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] ON;
    EXEC(N'INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (7, N''700-WSD'', N''Wiley Sanitation District'', 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
        SET IDENTITY_INSERT [Funds] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214031052_AddWileySanitationDistrictFund'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214031052_AddWileySanitationDistrictFund', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC sp_rename N'[MunicipalAccounts].[Fund]', N'FundType', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC sp_rename N'[MunicipalAccounts].[IX_MunicipalAccounts_Fund_Type]', N'IX_MunicipalAccounts_FundType_Type', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD [FundId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 26;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 27;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 28;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 29;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 30;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    EXEC(N'UPDATE [MunicipalAccounts] SET [FundId] = NULL
    WHERE [Id] = 31;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    CREATE INDEX [IX_MunicipalAccounts_FundId] ON [MunicipalAccounts] ([FundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    ALTER TABLE [MunicipalAccounts] ADD CONSTRAINT [FK_MunicipalAccounts_Funds_FundId] FOREIGN KEY ([FundId]) REFERENCES [Funds] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214033023_AlignMunicipalAccountWithFundTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214033023_AlignMunicipalAccountWithFundTable', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218192931_AddSavedScenarioSnapshots'
)
BEGIN
    CREATE TABLE [SavedScenarioSnapshots] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [RateIncreasePercent] decimal(19,4) NOT NULL,
        [ExpenseIncreasePercent] decimal(19,4) NOT NULL,
        [RevenueTarget] decimal(19,4) NOT NULL,
        [ProjectedValue] decimal(19,4) NOT NULL,
        [Variance] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SavedScenarioSnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218192931_AddSavedScenarioSnapshots'
)
BEGIN
    CREATE INDEX [IX_SavedScenarioSnapshots_CreatedAtUtc] ON [SavedScenarioSnapshots] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260218192931_AddSavedScenarioSnapshots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260218192931_AddSavedScenarioSnapshots', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260306023100_AddUserMemoryFactsAndOnboardingPolicy'
)
BEGIN
    CREATE TABLE [UserMemoryFacts] (
        [Id] nvarchar(32) NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [FactKey] nvarchar(64) NOT NULL,
        [FactValue] nvarchar(512) NOT NULL,
        [Confidence] float NOT NULL,
        [ObservationCount] int NOT NULL,
        [SourceConversationId] nvarchar(128) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [LastObservedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserMemoryFacts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260306023100_AddUserMemoryFactsAndOnboardingPolicy'
)
BEGIN
    CREATE INDEX [IX_UserMemoryFacts_LastObservedAtUtc] ON [UserMemoryFacts] ([LastObservedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260306023100_AddUserMemoryFactsAndOnboardingPolicy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserMemoryFacts_UserId_FactKey] ON [UserMemoryFacts] ([UserId], [FactKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260306023100_AddUserMemoryFactsAndOnboardingPolicy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260306023100_AddUserMemoryFactsAndOnboardingPolicy', N'10.0.2');
END;

COMMIT;
GO

