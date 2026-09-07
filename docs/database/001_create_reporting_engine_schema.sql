/*
  ReportingEngine V1 database schema
  Preserves the required RepScdhedularProject_* table names.

  Run on an empty SQL Server database before starting Admin/Worker when you do not
  want the application to auto-create tables.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.RepScdhedularProject_Customer
(
    CustomerId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_Customer PRIMARY KEY,
    CustomerCode nvarchar(50) NOT NULL,
    CustomerName nvarchar(200) NOT NULL,
    TimeZoneId nvarchar(100) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_Customer_IsActive DEFAULT (1),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_Customer_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL
);
GO

CREATE UNIQUE INDEX IX_RepScdhedularProject_Customer_CustomerCode
ON dbo.RepScdhedularProject_Customer(CustomerCode);
GO

CREATE TABLE dbo.RepScdhedularProject_DataSource
(
    DataSourceId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_DataSource PRIMARY KEY,
    DataSourceName nvarchar(200) NOT NULL,
    DataSourceType nvarchar(50) NOT NULL,
    ConnectionReference nvarchar(200) NOT NULL,
    ConnectionString nvarchar(max) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_DataSource_IsActive DEFAULT (1),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_DataSource_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL
);
GO

CREATE INDEX IX_RepScdhedularProject_DataSource_DataSourceName
ON dbo.RepScdhedularProject_DataSource(DataSourceName);
GO

CREATE TABLE dbo.RepScdhedularProject_Schedule
(
    ScheduleId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_Schedule PRIMARY KEY,
    ScheduleName nvarchar(200) NOT NULL,
    ScheduleType nvarchar(50) NOT NULL,
    CronExpression nvarchar(100) NULL,
    TimeZoneId nvarchar(100) NOT NULL,
    StartDate datetime2 NULL,
    EndDate datetime2 NULL,
    IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_Schedule_IsActive DEFAULT (1),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_Schedule_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL
);
GO

CREATE TABLE dbo.RepScdhedularProject_FileConfiguration
(
    FileConfigId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_FileConfiguration PRIMARY KEY,
    ConfigurationName nvarchar(200) NOT NULL,
    FileFormat nvarchar(30) NOT NULL,
    FileNamePattern nvarchar(500) NOT NULL,
    SplitEnabled bit NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_SplitEnabled DEFAULT (0),
    SplitType nvarchar(30) NULL,
    SplitValue bigint NULL,
    CompressionType nvarchar(30) NULL,
    ZipBatchSize int NULL,
    KeepLocalFiles bit NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_KeepLocalFiles DEFAULT (1),
    EncryptionEnabled bit NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_EncryptionEnabled DEFAULT (0),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_FileConfiguration_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL
);
GO

CREATE TABLE dbo.RepScdhedularProject_DeliveryConfiguration
(
    DeliveryConfigId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_DeliveryConfiguration PRIMARY KEY,
    DeliveryName nvarchar(200) NOT NULL,
    DeliveryType nvarchar(50) NOT NULL,
    DestinationReference nvarchar(500) NULL,
    EmailTo nvarchar(2000) NULL,
    EmailCc nvarchar(2000) NULL,
    EmailBcc nvarchar(2000) NULL,
    EmailSubjectTemplate nvarchar(1000) NULL,
    EmailBodyTemplate nvarchar(max) NULL,
    SecretReference nvarchar(500) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_DeliveryConfiguration_IsActive DEFAULT (1),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_DeliveryConfiguration_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL
);
GO

CREATE TABLE dbo.RepScdhedularProject_ReportDefinition
(
    ReportId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_ReportDefinition PRIMARY KEY,
    CustomerId bigint NOT NULL,
    ReportCode nvarchar(100) NOT NULL,
    ReportName nvarchar(300) NOT NULL,
    DataSourceId bigint NOT NULL,
    ScheduleId bigint NOT NULL,
    FileConfigId bigint NOT NULL,
    DeliveryConfigId bigint NOT NULL,
    QueryText nvarchar(max) NOT NULL,
    Description nvarchar(1000) NULL,
    VersionNumber int NOT NULL CONSTRAINT DF_RepScdhedularProject_ReportDefinition_VersionNumber DEFAULT (1),
    Status nvarchar(30) NOT NULL CONSTRAINT DF_RepScdhedularProject_ReportDefinition_Status DEFAULT ('DRAFT'),
    IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_ReportDefinition_IsActive DEFAULT (1),
    CreatedBy nvarchar(100) NOT NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_ReportDefinition_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedBy nvarchar(100) NULL,
    ModifiedDate datetime2 NULL,
    CONSTRAINT FK_RepScdhedularProject_ReportDefinition_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.RepScdhedularProject_Customer(CustomerId),
    CONSTRAINT FK_RepScdhedularProject_ReportDefinition_DataSource FOREIGN KEY (DataSourceId) REFERENCES dbo.RepScdhedularProject_DataSource(DataSourceId),
    CONSTRAINT FK_RepScdhedularProject_ReportDefinition_Schedule FOREIGN KEY (ScheduleId) REFERENCES dbo.RepScdhedularProject_Schedule(ScheduleId),
    CONSTRAINT FK_RepScdhedularProject_ReportDefinition_FileConfiguration FOREIGN KEY (FileConfigId) REFERENCES dbo.RepScdhedularProject_FileConfiguration(FileConfigId),
    CONSTRAINT FK_RepScdhedularProject_ReportDefinition_DeliveryConfiguration FOREIGN KEY (DeliveryConfigId) REFERENCES dbo.RepScdhedularProject_DeliveryConfiguration(DeliveryConfigId)
);
GO

CREATE UNIQUE INDEX IX_RepScdhedularProject_ReportDefinition_ReportCode
ON dbo.RepScdhedularProject_ReportDefinition(ReportCode);
GO

CREATE TABLE dbo.RepScdhedularProject_ReportParameter
(
    ParameterId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_ReportParameter PRIMARY KEY,
    ReportId bigint NOT NULL,
    ParameterName nvarchar(100) NOT NULL,
    ParameterType nvarchar(30) NOT NULL,
    ParameterValue nvarchar(1000) NULL,
    ValueSource nvarchar(50) NOT NULL,
    CONSTRAINT FK_RepScdhedularProject_ReportParameter_ReportDefinition FOREIGN KEY (ReportId) REFERENCES dbo.RepScdhedularProject_ReportDefinition(ReportId) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX IX_RepScdhedularProject_ReportParameter_ReportId_ParameterName
ON dbo.RepScdhedularProject_ReportParameter(ReportId, ParameterName);
GO

CREATE TABLE dbo.RepScdhedularProject_JobExecution
(
    ExecutionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_JobExecution PRIMARY KEY,
    ReportId bigint NOT NULL,
    ScheduledTime datetime2 NULL,
    StartedAt datetime2 NULL,
    CompletedAt datetime2 NULL,
    Status nvarchar(30) NOT NULL,
    RecordCount bigint NULL,
    FileCount int NULL,
    RetryCount int NOT NULL CONSTRAINT DF_RepScdhedularProject_JobExecution_RetryCount DEFAULT (0),
    ErrorCode nvarchar(100) NULL,
    ErrorMessage nvarchar(max) NULL,
    IdempotencyKey nvarchar(200) NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_JobExecution_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_RepScdhedularProject_JobExecution_ReportDefinition FOREIGN KEY (ReportId) REFERENCES dbo.RepScdhedularProject_ReportDefinition(ReportId)
);
GO

CREATE UNIQUE INDEX IX_RepScdhedularProject_JobExecution_IdempotencyKey
ON dbo.RepScdhedularProject_JobExecution(IdempotencyKey)
WHERE IdempotencyKey IS NOT NULL;
GO

CREATE INDEX IX_RepScdhedularProject_JobExecution_ReportId_Status
ON dbo.RepScdhedularProject_JobExecution(ReportId, Status);
GO

CREATE TABLE dbo.RepScdhedularProject_FileExecution
(
    FileExecutionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_FileExecution PRIMARY KEY,
    ExecutionId bigint NOT NULL,
    SequenceNumber int NOT NULL,
    FileName nvarchar(500) NOT NULL,
    FilePath nvarchar(1000) NULL,
    FileSizeBytes bigint NULL,
    RecordCount bigint NULL,
    GenerationStatus nvarchar(30) NOT NULL,
    DeliveryStatus nvarchar(30) NOT NULL,
    Checksum nvarchar(200) NULL,
    CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_FileExecution_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_RepScdhedularProject_FileExecution_JobExecution FOREIGN KEY (ExecutionId) REFERENCES dbo.RepScdhedularProject_JobExecution(ExecutionId) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX IX_RepScdhedularProject_FileExecution_ExecutionId_SequenceNumber
ON dbo.RepScdhedularProject_FileExecution(ExecutionId, SequenceNumber);
GO

CREATE TABLE dbo.RepScdhedularProject_AuditLog
(
    AuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_AuditLog PRIMARY KEY,
    EntityType nvarchar(100) NOT NULL,
    EntityId bigint NOT NULL,
    Action nvarchar(50) NOT NULL,
    OldValue nvarchar(max) NULL,
    NewValue nvarchar(max) NULL,
    PerformedBy nvarchar(100) NOT NULL,
    PerformedAt datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_AuditLog_PerformedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE INDEX IX_RepScdhedularProject_AuditLog_EntityType_EntityId
ON dbo.RepScdhedularProject_AuditLog(EntityType, EntityId);
GO
