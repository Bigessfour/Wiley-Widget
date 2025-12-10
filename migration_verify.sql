BEGIN TRANSACTION;
ALTER TABLE [Funds] ADD [AccountNumber_Value] nvarchar(20) NOT NULL DEFAULT N'';

DROP INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries];
DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BudgetEntries]') AND [c].[name] = N'MunicipalAccountId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [BudgetEntries] DROP CONSTRAINT [' + @var0 + '];');
UPDATE [BudgetEntries] SET [MunicipalAccountId] = 0 WHERE [MunicipalAccountId] IS NULL;
ALTER TABLE [BudgetEntries] ALTER COLUMN [MunicipalAccountId] int NOT NULL;
ALTER TABLE [BudgetEntries] ADD DEFAULT 0 FOR [MunicipalAccountId];
CREATE INDEX [IX_BudgetEntries_MunicipalAccountId] ON [BudgetEntries] ([MunicipalAccountId]);

ALTER TABLE [AppSettings] ADD [AutoSaveIntervalMinutes] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [CurrencyFormat] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [CurrentFiscalYear] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [DatabaseName] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [DatabaseServer] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [DateFormat] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [DefaultLanguage] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [EnableAI] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [EnableAutoSave] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [EnableNotifications] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [EnableQuickBooksSync] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [EnableSounds] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [FiscalPeriod] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [FiscalQuarter] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [FiscalYearEnd] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [FiscalYearStart] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [FiscalYearStartDay] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [FiscalYearStartMonth] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [QuickBooksCompanyFile] nvarchar(max) NULL;

ALTER TABLE [AppSettings] ADD [QuickBooksRedirectUri] nvarchar(max) NULL;

ALTER TABLE [AppSettings] ADD [SessionTimeoutMinutes] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [SyncIntervalMinutes] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [UseFiscalYearForReporting] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AppSettings] ADD [XaiApiEndpoint] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [XaiApiKey] nvarchar(max) NULL;

ALTER TABLE [AppSettings] ADD [XaiMaxTokens] int NOT NULL DEFAULT 0;

ALTER TABLE [AppSettings] ADD [XaiModel] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [AppSettings] ADD [XaiTemperature] float NOT NULL DEFAULT 0.0E0;

ALTER TABLE [AppSettings] ADD [XaiTimeout] int NOT NULL DEFAULT 0;

CREATE TABLE [AIContextEntities] (
    [Id] int NOT NULL IDENTITY,
    [ConversationId] nvarchar(450) NOT NULL,
    [EntityType] nvarchar(100) NOT NULL,
    [EntityValue] nvarchar(500) NOT NULL,
    [NormalizedValue] nvarchar(500) NOT NULL,
    [Context] nvarchar(2000) NULL,
    [ConfidenceScore] float NULL,
    [FirstMentionedAt] datetime2 NOT NULL,
    [LastMentionedAt] datetime2 NOT NULL,
    [MentionCount] int NOT NULL,
    [MetadataJson] nvarchar(max) NULL,
    [ImportanceScore] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Tags] nvarchar(500) NULL,
    CONSTRAINT [PK_AIContextEntities] PRIMARY KEY ([Id])
);

CREATE TABLE [ConversationHistories] (
    [Id] int NOT NULL IDENTITY,
    [ConversationId] nvarchar(450) NOT NULL,
    [Title] nvarchar(500) NOT NULL,
    [Description] nvarchar(max) NULL,
    [MessagesJson] nvarchar(max) NOT NULL,
    [InitialContext] nvarchar(max) NULL,
    [MetadataJson] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [LastAccessedAt] datetime2 NULL,
    [MessageCount] int NOT NULL,
    [ToolCallCount] int NOT NULL,
    [IsArchived] bit NOT NULL,
    [IsFavorite] bit NOT NULL,
    CONSTRAINT [PK_ConversationHistories] PRIMARY KEY ([Id])
);

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

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AutoSaveIntervalMinutes', N'CacheExpirationMinutes', N'CurrencyFormat', N'CurrentFiscalYear', N'DatabaseName', N'DatabaseServer', N'DateFormat', N'DefaultLanguage', N'EnableAI', N'EnableAutoSave', N'EnableDataCaching', N'EnableFileLogging', N'EnableNotifications', N'EnableQuickBooksSync', N'EnableSounds', N'FiscalPeriod', N'FiscalQuarter', N'FiscalYearEnd', N'FiscalYearStart', N'FiscalYearStartDay', N'FiscalYearStartMonth', N'IncludeChartsInReports', N'LastReportEndDate', N'LastReportStartDate', N'LastSelectedEnterpriseId', N'LastSelectedFormat', N'LastSelectedReportType', N'LogFilePath', N'QboAccessToken', N'QboClientId', N'QboClientSecret', N'QboRefreshToken', N'QboTokenExpiry', N'QuickBooksAccessToken', N'QuickBooksCompanyFile', N'QuickBooksEnvironment', N'QuickBooksRealmId', N'QuickBooksRedirectUri', N'QuickBooksRefreshToken', N'QuickBooksTokenExpiresUtc', N'SelectedLogLevel', N'SessionTimeoutMinutes', N'SyncIntervalMinutes', N'Theme', N'UseDynamicColumns', N'UseFiscalYearForReporting', N'WindowHeight', N'WindowLeft', N'WindowMaximized', N'WindowTop', N'WindowWidth', N'XaiApiEndpoint', N'XaiApiKey', N'XaiMaxTokens', N'XaiModel', N'XaiTemperature', N'XaiTimeout') AND [object_id] = OBJECT_ID(N'[AppSettings]'))
    SET IDENTITY_INSERT [AppSettings] ON;
INSERT INTO [AppSettings] ([Id], [AutoSaveIntervalMinutes], [CacheExpirationMinutes], [CurrencyFormat], [CurrentFiscalYear], [DatabaseName], [DatabaseServer], [DateFormat], [DefaultLanguage], [EnableAI], [EnableAutoSave], [EnableDataCaching], [EnableFileLogging], [EnableNotifications], [EnableQuickBooksSync], [EnableSounds], [FiscalPeriod], [FiscalQuarter], [FiscalYearEnd], [FiscalYearStart], [FiscalYearStartDay], [FiscalYearStartMonth], [IncludeChartsInReports], [LastReportEndDate], [LastReportStartDate], [LastSelectedEnterpriseId], [LastSelectedFormat], [LastSelectedReportType], [LogFilePath], [QboAccessToken], [QboClientId], [QboClientSecret], [QboRefreshToken], [QboTokenExpiry], [QuickBooksAccessToken], [QuickBooksCompanyFile], [QuickBooksEnvironment], [QuickBooksRealmId], [QuickBooksRedirectUri], [QuickBooksRefreshToken], [QuickBooksTokenExpiresUtc], [SelectedLogLevel], [SessionTimeoutMinutes], [SyncIntervalMinutes], [Theme], [UseDynamicColumns], [UseFiscalYearForReporting], [WindowHeight], [WindowLeft], [WindowMaximized], [WindowTop], [WindowWidth], [XaiApiEndpoint], [XaiApiKey], [XaiMaxTokens], [XaiModel], [XaiTemperature], [XaiTimeout])
VALUES (1, 5, 30, N'USD', N'2024-2025', N'WileyWidget', N'localhost', N'MM/dd/yyyy', N'en-US', CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), N'Q1', 1, N'June 30', N'July 1', 1, 7, CAST(1 AS bit), NULL, NULL, 1, NULL, NULL, N'logs/wiley-widget.log', NULL, NULL, NULL, NULL, '2026-01-01T00:00:00.0000000Z', NULL, NULL, N'sandbox', NULL, NULL, NULL, NULL, N'Information', 60, 30, N'FluentDark', CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, NULL, NULL, NULL, N'https://api.x.ai/v1', NULL, 2000, N'grok-4-0709', 0.69999999999999996E0, 30);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AutoSaveIntervalMinutes', N'CacheExpirationMinutes', N'CurrencyFormat', N'CurrentFiscalYear', N'DatabaseName', N'DatabaseServer', N'DateFormat', N'DefaultLanguage', N'EnableAI', N'EnableAutoSave', N'EnableDataCaching', N'EnableFileLogging', N'EnableNotifications', N'EnableQuickBooksSync', N'EnableSounds', N'FiscalPeriod', N'FiscalQuarter', N'FiscalYearEnd', N'FiscalYearStart', N'FiscalYearStartDay', N'FiscalYearStartMonth', N'IncludeChartsInReports', N'LastReportEndDate', N'LastReportStartDate', N'LastSelectedEnterpriseId', N'LastSelectedFormat', N'LastSelectedReportType', N'LogFilePath', N'QboAccessToken', N'QboClientId', N'QboClientSecret', N'QboRefreshToken', N'QboTokenExpiry', N'QuickBooksAccessToken', N'QuickBooksCompanyFile', N'QuickBooksEnvironment', N'QuickBooksRealmId', N'QuickBooksRedirectUri', N'QuickBooksRefreshToken', N'QuickBooksTokenExpiresUtc', N'SelectedLogLevel', N'SessionTimeoutMinutes', N'SyncIntervalMinutes', N'Theme', N'UseDynamicColumns', N'UseFiscalYearForReporting', N'WindowHeight', N'WindowLeft', N'WindowMaximized', N'WindowTop', N'WindowWidth', N'XaiApiEndpoint', N'XaiApiKey', N'XaiMaxTokens', N'XaiModel', N'XaiTemperature', N'XaiTimeout') AND [object_id] = OBJECT_ID(N'[AppSettings]'))
    SET IDENTITY_INSERT [AppSettings] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedDate', N'EndDate', N'IsActive', N'Name', N'StartDate', N'Status', N'Year') AND [object_id] = OBJECT_ID(N'[BudgetPeriods]'))
    SET IDENTITY_INSERT [BudgetPeriods] ON;
INSERT INTO [BudgetPeriods] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
VALUES (2, '2025-10-28T00:00:00.0000000Z', '2026-12-31T23:59:59.0000000Z', CAST(0 AS bit), N'2026 Proposed', '2026-01-01T00:00:00.0000000Z', 1, 2026);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedDate', N'EndDate', N'IsActive', N'Name', N'StartDate', N'Status', N'Year') AND [object_id] = OBJECT_ID(N'[BudgetPeriods]'))
    SET IDENTITY_INSERT [BudgetPeriods] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] ON;
INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
VALUES (1, N'ADMIN', N'Administration', NULL),
(2, N'DPW', N'Public Works', NULL),
(3, N'CULT', N'Culture and Recreation', NULL),
(5, N'UTIL', N'Utilities', NULL),
(6, N'COMM', N'Community Center', NULL),
(7, N'CONS', N'Conservation', NULL),
(8, N'REC', N'Recreation', NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
    SET IDENTITY_INSERT [Funds] ON;
INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
VALUES (1, N'100-GEN', N'General Fund', 1),
(2, N'200-ENT', N'Enterprise Fund', 2),
(3, N'300-UTIL', N'Utility Fund', 2),
(4, N'400-COMM', N'Community Center Fund', 3),
(5, N'500-CONS', N'Conservation Trust Fund', 6),
(6, N'600-REC', N'Recreation Fund', 3);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FundCode', N'Name', N'Type') AND [object_id] = OBJECT_ID(N'[Funds]'))
    SET IDENTITY_INSERT [Funds] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BudgetYearAmount', N'BudgetYearLevy', N'CurrentYearAmount', N'CurrentYearLevy', N'Description', N'IncDecAmount', N'IncDecLevy', N'PriorYearAmount', N'PriorYearLevy') AND [object_id] = OBJECT_ID(N'[TaxRevenueSummaries]'))
    SET IDENTITY_INSERT [TaxRevenueSummaries] ON;
INSERT INTO [TaxRevenueSummaries] ([Id], [BudgetYearAmount], [BudgetYearLevy], [CurrentYearAmount], [CurrentYearLevy], [Description], [IncDecAmount], [IncDecLevy], [PriorYearAmount], [PriorYearLevy])
VALUES (1, 1880448.0, 1880448.0, 1072691.0, 1072691.0, N'ASSESSED VALUATION-COUNTY FUND', 807757.0, 807757.0, 1069780.0, 1069780.0),
(2, 85692.0, 45.57, 48883.0, 45.57, N'GENERAL', 36809.0, 0.0, 48750.0, 45.57),
(3, 0.0, 0.0, 0.0, 0.0, N'UTILITY', 0.0, 0.0, 0.0, 0.0),
(4, 0.0, 0.0, 0.0, 0.0, N'COMMUNITY CENTER', 0.0, 0.0, 0.0, 0.0),
(5, 0.0, 0.0, 0.0, 0.0, N'CONSERVATION TRUST FUND', 0.0, 0.0, 0.0, 0.0),
(6, 0.0, 0.0, 0.0, 0.0, N'TEMPORARY MILL LEVY CREDIT', 0.0, 0.0, 0.0, 0.0),
(7, 85692.0, 45.57, 48883.0, 45.57, N'TOTAL', 36810.0, 0.0, 48750.0, 45.57);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BudgetYearAmount', N'BudgetYearLevy', N'CurrentYearAmount', N'CurrentYearLevy', N'Description', N'IncDecAmount', N'IncDecLevy', N'PriorYearAmount', N'PriorYearLevy') AND [object_id] = OBJECT_ID(N'[TaxRevenueSummaries]'))
    SET IDENTITY_INSERT [TaxRevenueSummaries] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactInfo', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Vendor]'))
    SET IDENTITY_INSERT [Vendor] ON;
INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
VALUES (1, N'contact@acmesupplies.example.com', CAST(1 AS bit), N'Acme Supplies'),
(2, N'info@muniservices.example.com', CAST(1 AS bit), N'Municipal Services Co.'),
(3, N'projects@trailbuilders.example.com', CAST(1 AS bit), N'Trail Builders LLC');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactInfo', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Vendor]'))
    SET IDENTITY_INSERT [Vendor] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
    SET IDENTITY_INSERT [BudgetEntries] ON;
INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
VALUES (1, N'332.1', NULL, 0.0, 360.0, '2025-10-28T00:00:00.0000000', 1, N'Federal: Mineral Lease', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(2, N'333.00', NULL, 0.0, 240.0, '2025-10-28T00:00:00.0000000', 1, N'State: Cigarette Taxes', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(3, N'334.31', NULL, 0.0, 18153.0, '2025-10-28T00:00:00.0000000', 1, N'Highways Users', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(4, N'313.00', NULL, 0.0, 1775.0, '2025-10-28T00:00:00.0000000', 1, N'Additional MV', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(5, N'337.17', NULL, 0.0, 1460.0, '2025-10-28T00:00:00.0000000', 1, N'County Road & Bridge', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(6, N'311.20', NULL, 0.0, 1500.0, '2025-10-28T00:00:00.0000000', 1, N'Senior Homestead Exemption', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(7, N'312.00', NULL, 0.0, 5100.0, '2025-10-28T00:00:00.0000000', 1, N'Specific Ownership Taxes', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(8, N'314.00', NULL, 0.0, 2500.0, '2025-10-28T00:00:00.0000000', 1, N'Tax A', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(9, N'319.00', NULL, 0.0, 35.0, '2025-10-28T00:00:00.0000000', 1, N'Penalties & Interest on Delinquent Taxes', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(10, N'336.00', NULL, 0.0, 120000.0, '2025-10-28T00:00:00.0000000', 1, N'Sales Tax', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(11, N'318.20', NULL, 0.0, 7058.0, '2025-10-28T00:00:00.0000000', 1, N'Franchise Fee', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(12, N'322.70', NULL, 0.0, 50.0, '2025-10-28T00:00:00.0000000', 1, N'Animal Licenses', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(13, N'310.00', NULL, 0.0, 6000.0, '2025-10-28T00:00:00.0000000', 1, N'Charges for Services: WSD Collection Fee', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(14, N'370.00', NULL, 0.0, 12000.0, '2025-10-28T00:00:00.0000000', 1, N'Housing Authority Mgt Fee', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(15, N'373.00', NULL, 0.0, 2400.0, '2025-10-28T00:00:00.0000000', 1, N'Pickup Usage Fee', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(16, N'361.00', NULL, 0.0, 325.0, '2025-10-28T00:00:00.0000000', 1, N'Miscellaneous Receipts: Interest Earnings', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(17, N'365.00', NULL, 0.0, 100.0, '2025-10-28T00:00:00.0000000', 1, N'Dividends', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(18, N'363.00', NULL, 0.0, 1100.0, '2025-10-28T00:00:00.0000000', 1, N'Lease', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(19, N'350.00', NULL, 0.0, 10000.0, '2025-10-28T00:00:00.0000000', 1, N'Wiley Hay Days Donations', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0),
(20, N'362.00', NULL, 0.0, 2500.0, '2025-10-28T00:00:00.0000000', 1, N'Donations', 0.0, '0001-01-01T00:00:00.0000000', 2026, 1, 1, CAST(1 AS bit), 0, NULL, NULL, NULL, '0001-01-01T00:00:00.0000000', '2025-10-28T00:00:00.0000000', 0.0);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountNumber', N'ActivityCode', N'ActualAmount', N'BudgetedAmount', N'CreatedAt', N'DepartmentId', N'Description', N'EncumbranceAmount', N'EndPeriod', N'FiscalYear', N'FundId', N'FundType', N'IsGASBCompliant', N'MunicipalAccountId', N'ParentId', N'SourceFilePath', N'SourceRowNumber', N'StartPeriod', N'UpdatedAt', N'Variance') AND [object_id] = OBJECT_ID(N'[BudgetEntries]'))
    SET IDENTITY_INSERT [BudgetEntries] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] ON;
INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
VALUES (4, N'SAN', N'Sanitation', 2);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartmentCode', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundDescription', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type', N'TypeDescription', N'AccountNumber') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
INSERT INTO [MunicipalAccounts] ([Id], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [AccountNumber])
VALUES (26, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'MISC EXPENSE', NULL, NULL, NULL, 24, N'Asset', N'425'),
(27, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'TRAIL MAINTENANCE', NULL, NULL, NULL, 24, N'Asset', N'430'),
(28, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'PARK IMPROVEMENTS', NULL, NULL, NULL, 29, N'Asset', N'435'),
(29, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'EQUIPMENT PURCHASES', NULL, NULL, NULL, 29, N'Asset', N'440'),
(30, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'PROJECTS - SMALL', NULL, NULL, NULL, 24, N'Asset', N'445'),
(31, 0.0, 0.0, 1, 1, 8, N'Conservation Trust Fund', CAST(1 AS bit), NULL, N'RESERVES ALLOCATION', NULL, NULL, NULL, 30, N'Asset', N'450');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'BudgetAmount', N'BudgetPeriodId', N'DepartmentId', N'Fund', N'FundDescription', N'IsActive', N'LastSyncDate', N'Name', N'Notes', N'ParentAccountId', N'QuickBooksId', N'Type', N'TypeDescription', N'AccountNumber') AND [object_id] = OBJECT_ID(N'[MunicipalAccounts]'))
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;

CREATE INDEX [IX_AIContextEntities_ConversationId] ON [AIContextEntities] ([ConversationId]);

CREATE INDEX [IX_AIContextEntities_IsActive_Filtered] ON [AIContextEntities] ([IsActive]) WHERE [IsActive] = 1;

CREATE INDEX [IX_AIContextEntities_LastMentionedAt] ON [AIContextEntities] ([LastMentionedAt] DESC);

CREATE INDEX [IX_AIContextEntities_Type_NormalizedValue] ON [AIContextEntities] ([EntityType], [NormalizedValue]);

CREATE INDEX [IX_Charges_BillId] ON [Charges] ([BillId]);

CREATE INDEX [IX_Charges_ChargeType] ON [Charges] ([ChargeType]);

CREATE INDEX [IX_Charges_UtilityBillId] ON [Charges] ([UtilityBillId]);

CREATE UNIQUE INDEX [IX_ConversationHistories_ConversationId] ON [ConversationHistories] ([ConversationId]);

CREATE INDEX [IX_ConversationHistories_IsArchived_Filtered] ON [ConversationHistories] ([IsArchived]) WHERE [IsArchived] = 0;

CREATE INDEX [IX_ConversationHistories_UpdatedAt] ON [ConversationHistories] ([UpdatedAt] DESC);

CREATE INDEX [IX_UtilityBills_BillDate] ON [UtilityBills] ([BillDate]);

CREATE UNIQUE INDEX [IX_UtilityBills_BillNumber] ON [UtilityBills] ([BillNumber]);

CREATE INDEX [IX_UtilityBills_CustomerId] ON [UtilityBills] ([CustomerId]);

CREATE INDEX [IX_UtilityBills_DueDate] ON [UtilityBills] ([DueDate]);

CREATE INDEX [IX_UtilityBills_Status] ON [UtilityBills] ([Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251209181941_CapturePendingModelChanges', N'9.0.0');

COMMIT;
GO

