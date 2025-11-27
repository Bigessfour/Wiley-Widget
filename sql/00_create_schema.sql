-- WileyWidget Database Schema for SQL Server
-- Creates all required tables for the application

USE WileyWidget;
GO

-- Create BudgetPeriods table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BudgetPeriods')
BEGIN
    CREATE TABLE BudgetPeriods (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        [Year] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Created BudgetPeriods table';
END
GO

-- Create Departments table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Departments')
BEGIN
    CREATE TABLE Departments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(20),
        [Name] NVARCHAR(100) NOT NULL,
        Fund NVARCHAR(50),
        ParentDepartmentId INT NULL,
        CONSTRAINT FK_Departments_Parent FOREIGN KEY (ParentDepartmentId) 
            REFERENCES Departments(Id)
    );
    PRINT 'Created Departments table';
END
GO

-- Create MunicipalAccounts table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MunicipalAccounts')
BEGIN
    CREATE TABLE MunicipalAccounts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AccountNumber NVARCHAR(50) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Type] INT NOT NULL,
        Fund INT NOT NULL,
        Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
        BudgetAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        DepartmentId INT NOT NULL,
        BudgetPeriodId INT NOT NULL,
        ParentAccountId INT NULL,
        QuickBooksId NVARCHAR(50) NULL,
        LastSyncDate DATETIME2 NULL,
        Notes NVARCHAR(500) NULL,
        FundDescription NVARCHAR(100) NULL,
        TypeDescription NVARCHAR(100) NULL,
        CONSTRAINT FK_MunicipalAccounts_Department FOREIGN KEY (DepartmentId) 
            REFERENCES Departments(Id),
        CONSTRAINT FK_MunicipalAccounts_BudgetPeriod FOREIGN KEY (BudgetPeriodId) 
            REFERENCES BudgetPeriods(Id),
        CONSTRAINT FK_MunicipalAccounts_Parent FOREIGN KEY (ParentAccountId) 
            REFERENCES MunicipalAccounts(Id)
    );
    PRINT 'Created MunicipalAccounts table';
END
GO

-- Create BudgetEntries table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BudgetEntries')
BEGIN
    CREATE TABLE BudgetEntries (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AccountNumber NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(500),
        BudgetedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        ActualAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        EncumbranceAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        FiscalYear INT NOT NULL,
        StartPeriod DATETIME2 NOT NULL,
        EndPeriod DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        MunicipalAccountId INT NULL,
        DepartmentId INT NOT NULL,
        BudgetPeriodId INT NOT NULL,
        ActivityCode NVARCHAR(20) NULL,
        CONSTRAINT FK_BudgetEntries_MunicipalAccount FOREIGN KEY (MunicipalAccountId) 
            REFERENCES MunicipalAccounts(Id),
        CONSTRAINT FK_BudgetEntries_Department FOREIGN KEY (DepartmentId) 
            REFERENCES Departments(Id),
        CONSTRAINT FK_BudgetEntries_BudgetPeriod FOREIGN KEY (BudgetPeriodId) 
            REFERENCES BudgetPeriods(Id)
    );
    PRINT 'Created BudgetEntries table';
END
GO

PRINT 'Schema creation completed successfully';
GO
