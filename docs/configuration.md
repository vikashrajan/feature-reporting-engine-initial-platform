# Reporting Engine Configuration Reference

## Overview

The Reporting Engine platform uses standard .NET configuration files for the primary application database connection. Frontend-managed operational settings such as Data Sources, Schedules, File Configurations, Delivery Configurations, SMTP profiles, and failure notification email profiles are stored in the ReportingEngine database.

---

## Configuration Sections

### 1. Database Configuration (`Database`)

```json
"Database": {
  "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=ReportingEngineDb;Trusted_Connection=True;TrustServerCertificate=True;",
  "CommandTimeoutSeconds": 180,
  "EnableDetailedLogging": false
}
```

### 2. Hangfire Settings (`Hangfire`)

```json
"Hangfire": {
  "SchemaName": "hangfire",
  "WorkerCount": 10,
  "DashboardEnabled": true,
  "DashboardPath": "/hangfire"
}
```
- `WorkerCount`: Controls thread concurrency for background executions in Worker.
- `SchemaName`: Isolate Hangfire internal tables under a dedicated SQL schema.

### 3. Execution Options (`Execution`)

```json
"Execution": {
  "TemporaryFilePath": "C:\\Temp\\ReportingEngine",
  "CommandTimeoutSeconds": 300,
  "MaxOutputRecords": 1000000,
  "CleanTemporaryFilesOnSuccess": true
}
```

### 4. Retry Options (`Retry`)

```json
"Retry": {
  "MaxRetryCount": 3,
  "InitialDelaySeconds": 30,
  "BackoffMultiplier": 2.0
}
```

### 5. Email Delivery Options (`Email`)

```json
"Email": {
  "Host": "smtp.example.com",
  "Port": 587,
  "EnableSsl": true,
  "UserName": "reporting@example.com",
  "Password": "SecretPassword",
  "FromAddress": "reporting@example.com",
  "FromDisplayName": "Enterprise Reporting Platform",
  "UseFileDrop": false,
  "FileDropPath": "C:\\Temp\\EmailDrop"
}
```
- `Email` in appsettings is only a fallback/default. Configure live SMTP profiles from the Delivery Configurations screen so they are stored in `RepScdhedularProject_DeliveryConfiguration`.
- Mark one or more EMAIL delivery rows as failure-notification profiles to send job failure emails from database-backed configuration.

### 6. Connection & Secret References (`ConnectionReferences`, `SecretReferences`)

Stores mapped connection strings or key references for data sources and delivery target credentials.
For Data Sources, `RepScdhedularProject_DataSource.ConnectionReference` stores the reference name and `RepScdhedularProject_DataSource.ConnectionString` stores the source database connection string. The scheduler uses the DB connection string first, then falls back to `ConnectionReferences:Values:<reference name>` for older rows.

```json
"ConnectionReferences": {
  "Values": {
    "SqlConn": "Server=(localdb)\\mssqllocaldb;Database=ReportingEngineDb;Trusted_Connection=True;TrustServerCertificate=True;",
    "CosmosConn": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDfjwjEG=;Database=Reporting;Container=Events",
    "DemoDB": "Server=(localdb)\\mssqllocaldb;Database=DemoDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
},
"SecretReferences": {
  "Values": {
    "SftpPasswordKey": "sftp-pass-vault-key",
    "BlobAccountKey": "azure-blob-key"
  }
}
```

### 7. Database Seeder Settings (`Seed`)

```json
"Seed": {
  "Enabled": true,
  "SeedSampleData": true
}
```

---

## Environment Variables Override Syntax

All configuration settings can be overridden via environment variables using double underscores `__`:

- `Database__ConnectionString`
- `Hangfire__WorkerCount`
- `Email__UseFileDrop`
- `Execution__TemporaryFilePath`
- `ConnectionReferences__Values__DemoDB`
