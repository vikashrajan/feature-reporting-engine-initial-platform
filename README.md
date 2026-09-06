# Enterprise Reporting Engine & Scheduler Platform (.NET 8)

A production-ready enterprise **Reporting Engine and Reporting Scheduler** platform built with **.NET 8**, **C# 12**, **Entity Framework Core 8**, **Hangfire**, **SQL Server**, and **Razor Pages API/Admin Portal**.

---

## Key Features

- **Clean Architecture**: Decoupled `Domain`, `Application`, `Infrastructure`, `Admin Web API/UI`, `Worker Service`, and `Tests`.
- **Database Schema**: Full EF Core persistence mapped to required table naming standards (`RepScdhedularProject_*`).
- **Data Extraction**:
  - `SQL` (SQL Server) with streaming `IAsyncEnumerable` reader to minimize memory footprint.
  - `COSMOS` (Azure Cosmos DB) provider.
  - Extensible provider resolver pattern for adding PostgreSQL, Oracle, REST API.
- **File Generation & Compression**:
  - Format generators for `CSV`, `JSON`, `TXT`, `EXCEL`, `XML`.
  - Compression support for `ZIP` and `GZIP`.
- **Dynamic File Splitting**:
  - Split large datasets by `RECORD_COUNT` or `FILE_SIZE` with dynamic sequence tokens.
- **Delivery Channels**:
  - `EMAIL` (SMTP with attachments or local file-drop debugging).
  - `SHARED_FOLDER` (UNC or local network storage).
  - `SFTP`, `FTP`, `BLOB` (Azure Blob Storage).
- **Scheduling & Background Execution**:
  - Hangfire for distributed background job scheduling.
  - Concurrent job execution for different reports without blocking.
  - `[DisableConcurrentExecution]` to prevent overlapping instances of the *same* report.
- **Idempotency & Resilience**:
  - Deterministic idempotency keying (`sched:{reportId}:{timestamp}`) preventing double execution.
  - Automatic retry with exponential backoff and transient failure detection.
- **Parameter Resolution**:
  - Support for `STATIC`, `SYSTEM` (`@UtcNow`, `@Today`), `PREVIOUS_EXECUTION`, and `CURRENT_EXECUTION`.
- **Dynamic Tokenization**:
  - Token replacement in file patterns and email subjects/bodies (`{CustomerCode}`, `{ReportCode}`, `{ExecutionDate}`, `{RecordCount}`, etc.).
- **Audit & Management UI**:
  - ASP.NET Core Admin API with Swagger documentation.
  - Razor Pages Admin Portal for Customer, DataSource, Schedule, FileConfig, DeliveryConfig, and Report Management.
  - Integrated Hangfire Dashboard (`/hangfire`).

---

## Solution Structure

```
ReportingEngine/
├── ReportingEngine.Domain/            # Entities, Value Objects, Enums, Constants
├── ReportingEngine.Application/       # Interfaces, DTOs, Use Cases, Validators
├── ReportingEngine.Infrastructure/    # EF Core, Hangfire, Data & Delivery Providers, File Processors
├── ReportingEngine.Admin/             # ASP.NET Core Web API + Razor Pages Admin UI
├── ReportingEngine.Worker/            # Background Worker Service running Hangfire Server
├── ReportingEngine.Tests/             # xUnit Unit & Integration Tests
└── docs/                              # Technical Documentation (Architecture, Config, Deployment)
```

---

## Quick Start

### Prerequisites

- **.NET 8.0 SDK** (or .NET 10 SDK with RollForward enabled)
- **SQL Server** (LocalDB, Express, or Enterprise)

### Running Locally

1. **Configure Connection Strings & Settings**:
   Edit `appsettings.json` in `ReportingEngine.Admin` and `ReportingEngine.Worker`:
   ```json
   "Database": {
     "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=ReportingEngineDb;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

2. **Database Auto-Initialization & Seeding**:
   Upon starting either Admin or Worker, EF Core migrations run automatically and sample seed data is populated.

3. **Start Admin Portal**:
   ```bash
   dotnet run --project ReportingEngine.Admin
   ```
   Access Admin Portal at: `http://localhost:5000` (or configured HTTPS port).
   Hangfire Dashboard at: `http://localhost:5000/hangfire`.

4. **Start Worker Service**:
   ```bash
   dotnet run --project ReportingEngine.Worker
   ```

5. **Run Test Suite**:
   ```bash
   dotnet test
   ```

---

## Documentation

For full architectural blueprints, configuration parameters, and production deployment guides, refer to the `docs/` directory:
- [Architecture Blueprint](docs/architecture.md)
- [Configuration Reference](docs/configuration.md)
- [Deployment Guide](docs/deployment.md)
- [Database Scripts](docs/database/README.md)
