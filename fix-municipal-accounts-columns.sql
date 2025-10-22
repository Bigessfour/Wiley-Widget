-- Idempotent script to add missing columns to MunicipalAccounts table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MunicipalAccounts') AND name = 'FundDescription')
BEGIN
    ALTER TABLE MunicipalAccounts ADD FundDescription NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MunicipalAccounts') AND name = 'TypeDescription')
BEGIN
    ALTER TABLE MunicipalAccounts ADD TypeDescription NVARCHAR(MAX) NULL;
END
