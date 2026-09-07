/*
Adds DB-backed SMTP profiles and independent job failure notification profiles.
Existing V1 table names are preserved.
*/

IF OBJECT_ID('dbo.RepScdhedularProject_SmtpConfiguration', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepScdhedularProject_SmtpConfiguration (
        SmtpConfigId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_SmtpConfiguration PRIMARY KEY,
        ProfileName nvarchar(200) NOT NULL,
        Host nvarchar(300) NOT NULL,
        Port int NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_Port DEFAULT(587),
        EnableSsl bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_EnableSsl DEFAULT(1),
        FromAddress nvarchar(320) NOT NULL,
        FromDisplayName nvarchar(200) NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_FromDisplayName DEFAULT(''),
        UserName nvarchar(320) NULL,
        Password nvarchar(max) NULL,
        TimeoutSeconds int NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_TimeoutSeconds DEFAULT(120),
        UseFileDrop bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_UseFileDrop DEFAULT(0),
        FileDropPath nvarchar(1000) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_IsActive DEFAULT(1),
        CreatedBy nvarchar(100) NOT NULL,
        CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_SmtpConfiguration_CreatedDate DEFAULT(SYSUTCDATETIME()),
        ModifiedBy nvarchar(100) NULL,
        ModifiedDate datetime2 NULL
    );

    CREATE UNIQUE INDEX IX_RepScdhedularProject_SmtpConfiguration_ProfileName
        ON dbo.RepScdhedularProject_SmtpConfiguration(ProfileName);
END;
GO

IF COL_LENGTH('dbo.RepScdhedularProject_DeliveryConfiguration', 'SmtpConfigId') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_DeliveryConfiguration
        ADD SmtpConfigId bigint NULL;
END;
GO

IF OBJECT_ID('dbo.FK_RepScdhedularProject_DeliveryConfiguration_SmtpConfiguration_SmtpConfigId', 'F') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_DeliveryConfiguration
        ADD CONSTRAINT FK_RepScdhedularProject_DeliveryConfiguration_SmtpConfiguration_SmtpConfigId
        FOREIGN KEY (SmtpConfigId)
        REFERENCES dbo.RepScdhedularProject_SmtpConfiguration(SmtpConfigId);
END;
GO

IF OBJECT_ID('dbo.RepScdhedularProject_JobFailureNotificationProfile', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepScdhedularProject_JobFailureNotificationProfile (
        FailureProfileId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepScdhedularProject_JobFailureNotificationProfile PRIMARY KEY,
        ProfileName nvarchar(200) NOT NULL,
        SmtpConfigId bigint NOT NULL,
        EmailTo nvarchar(2000) NOT NULL,
        EmailCc nvarchar(2000) NULL,
        EmailBcc nvarchar(2000) NULL,
        SubjectTemplate nvarchar(1000) NULL,
        BodyTemplate nvarchar(max) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_RepScdhedularProject_JobFailureNotificationProfile_IsActive DEFAULT(1),
        CreatedBy nvarchar(100) NOT NULL,
        CreatedDate datetime2 NOT NULL CONSTRAINT DF_RepScdhedularProject_JobFailureNotificationProfile_CreatedDate DEFAULT(SYSUTCDATETIME()),
        ModifiedBy nvarchar(100) NULL,
        ModifiedDate datetime2 NULL,
        CONSTRAINT FK_RepScdhedularProject_JobFailureNotificationProfile_SmtpConfiguration_SmtpConfigId
            FOREIGN KEY (SmtpConfigId)
            REFERENCES dbo.RepScdhedularProject_SmtpConfiguration(SmtpConfigId)
    );

    CREATE UNIQUE INDEX IX_RepScdhedularProject_JobFailureNotificationProfile_ProfileName
        ON dbo.RepScdhedularProject_JobFailureNotificationProfile(ProfileName);
END;
GO
