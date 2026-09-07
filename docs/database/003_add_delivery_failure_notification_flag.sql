/*
  Adds DB-backed failure-notification profile selection to existing ReportingEngine V1 databases.
  EMAIL rows in RepScdhedularProject_DeliveryConfiguration can be marked as failure notification recipients.
*/

IF COL_LENGTH('dbo.RepScdhedularProject_DeliveryConfiguration', 'IsFailureNotification') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_DeliveryConfiguration
        ADD IsFailureNotification bit NOT NULL
            CONSTRAINT DF_RepScdhedularProject_DeliveryConfiguration_IsFailureNotification DEFAULT(0);
END
GO
