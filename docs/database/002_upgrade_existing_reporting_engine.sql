/*
  ReportingEngine V1 upgrade script for existing databases.

  This keeps the existing RepScdhedularProject_* tables and adds columns needed
  by the batch ZIP/local cleanup changes. S3, Azure File Share, multiple SMTP
  profiles, dynamic time zones, and failure-notification settings do not require
  additional application tables; they reuse existing configuration columns.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.RepScdhedularProject_FileConfiguration', 'U') IS NULL
BEGIN
    THROW 51000, 'RepScdhedularProject_FileConfiguration was not found. Run 001_create_reporting_engine_schema.sql for a new database.', 1;
END
GO

IF COL_LENGTH('dbo.RepScdhedularProject_FileConfiguration', 'ZipBatchSize') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_FileConfiguration
        ADD ZipBatchSize int NULL;
END
GO

IF COL_LENGTH('dbo.RepScdhedularProject_FileConfiguration', 'KeepLocalFiles') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_FileConfiguration
        ADD KeepLocalFiles bit NOT NULL
            CONSTRAINT DF_RepScdhedularProject_FileConfiguration_KeepLocalFiles DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.RepScdhedularProject_FileExecution', 'FilePath') IS NOT NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_FileExecution
        ALTER COLUMN FilePath nvarchar(1000) NULL;
END
GO

/*
  Optional one-time cleanup for already delivered historical rows:
  uncomment this block only if old local files have already been removed from disk
  and you want the history grid to stop showing old local paths.

UPDATE fe
SET FilePath = NULL
FROM dbo.RepScdhedularProject_FileExecution fe
WHERE fe.DeliveryStatus = 'DELIVERED'
  AND fe.FilePath IS NOT NULL;
*/
GO
