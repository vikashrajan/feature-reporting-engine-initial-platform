/*
  Adds DB-backed source connection storage for existing ReportingEngine V1 databases.
  Keeps the existing RepScdhedularProject_DataSource table and ConnectionReference column.
*/

IF COL_LENGTH('dbo.RepScdhedularProject_DataSource', 'ConnectionString') IS NULL
BEGIN
    ALTER TABLE dbo.RepScdhedularProject_DataSource
        ADD ConnectionString nvarchar(max) NULL;
END
GO
