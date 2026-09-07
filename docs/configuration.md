# Reporting Engine Configuration Reference

## Overview

The Reporting Engine platform uses standard .NET configuration files (`appsettings.json`, ignored `appsettings.Local.json` overrides, and environment variables).

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
- `UseFileDrop`: Set to `true` during local development/testing to write emails as files instead of sending via SMTP.
- Save private SMTP credentials in `ReportingEngine.Admin/appsettings.Local.json` and `ReportingEngine.Worker/appsettings.Local.json`, or use the Delivery Configurations screen. These local files are intentionally ignored by Git.

### 6. Connection & Secret References (`ConnectionReferences`, `SecretReferences`)

Stores mapped connection strings or key references for data sources and delivery target credentials.
For Data Sources, `RepScdhedularProject_DataSource.ConnectionReference` stores the reference name, while the real source database connection string is saved under `ConnectionReferences:Values:<reference name>`. The Data Sources screen can write this private value to local ignored config for both Admin and Worker.

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
