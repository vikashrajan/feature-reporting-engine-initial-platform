# Enterprise Reporting Engine Deployment Guide

## Deployment Architecture

The solution provides two independently deployable applications:
1. **ReportingEngine.Admin**: Web application hosting REST API, Swagger UI, Razor Pages Management Portal, and Hangfire Dashboard.
2. **ReportingEngine.Worker**: Windows Service / Linux Daemon application hosting the Hangfire Processing Server.

---

## Option 1: Docker / Container Deployment

### Admin Dockerfile (`ReportingEngine.Admin/Dockerfile`)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ReportingEngine.Admin/ReportingEngine.Admin.csproj", "ReportingEngine.Admin/"]
COPY ["ReportingEngine.Application/ReportingEngine.Application.csproj", "ReportingEngine.Application/"]
COPY ["ReportingEngine.Domain/ReportingEngine.Domain.csproj", "ReportingEngine.Domain/"]
COPY ["ReportingEngine.Infrastructure/ReportingEngine.Infrastructure.csproj", "ReportingEngine.Infrastructure/"]
RUN dotnet restore "ReportingEngine.Admin/ReportingEngine.Admin.csproj"
COPY . .
WORKDIR "/src/ReportingEngine.Admin"
RUN dotnet build "ReportingEngine.Admin.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ReportingEngine.Admin.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ReportingEngine.Admin.dll"]
```

### Worker Dockerfile (`ReportingEngine.Worker/Dockerfile`)

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ReportingEngine.Worker/ReportingEngine.Worker.csproj", "ReportingEngine.Worker/"]
COPY ["ReportingEngine.Application/ReportingEngine.Application.csproj", "ReportingEngine.Application/"]
COPY ["ReportingEngine.Domain/ReportingEngine.Domain.csproj", "ReportingEngine.Domain/"]
COPY ["ReportingEngine.Infrastructure/ReportingEngine.Infrastructure.csproj", "ReportingEngine.Infrastructure/"]
RUN dotnet restore "ReportingEngine.Worker/ReportingEngine.Worker.csproj"
COPY . .
WORKDIR "/src/ReportingEngine.Worker"
RUN dotnet build "ReportingEngine.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ReportingEngine.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ReportingEngine.Worker.dll"]
```

### Docker Compose (`docker-compose.yml`)

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourSecurePassword123!
    ports:
      - "1433:1433"

  admin:
    build:
      context: .
      dockerfile: ReportingEngine.Admin/Dockerfile
    ports:
      - "5000:80"
    environment:
      - Database__ConnectionString=Server=sqlserver;Database=ReportingEngineDb;User Id=sa;Password=YourSecurePassword123!;TrustServerCertificate=True;
    depends_on:
      - sqlserver

  worker:
    build:
      context: .
      dockerfile: ReportingEngine.Worker/Dockerfile
    environment:
      - Database__ConnectionString=Server=sqlserver;Database=ReportingEngineDb;User Id=sa;Password=YourSecurePassword123!;TrustServerCertificate=True;
      - Hangfire__WorkerCount=20
    depends_on:
      - sqlserver
```

---

## Option 2: Windows Service Installation

To install `ReportingEngine.Worker` as a native Windows Service:

1. **Publish Release Output**:
   ```bash
   dotnet publish ReportingEngine.Worker/ReportingEngine.Worker.csproj -c Release -o C:\Services\ReportingEngineWorker
   ```

2. **Create Service using `sc.exe`**:
   ```cmd
   sc.exe create "ReportingEngineWorker" binPath= "C:\Services\ReportingEngineWorker\ReportingEngine.Worker.exe" start= auto
   ```

3. **Configure Service Account & Start**:
   ```cmd
   sc.exe config "ReportingEngineWorker" obj= "NT AUTHORITY\LocalService"
   sc.exe start "ReportingEngineWorker"
   ```

---

## Option 3: IIS Deployment (Admin Web API)

1. **Install ASP.NET Core Hosting Bundle 8.0** on IIS Server.
2. **Publish Release Output**:
   ```bash
   dotnet publish ReportingEngine.Admin/ReportingEngine.Admin.csproj -c Release -o C:\inetpub\wwwroot\ReportingAdmin
   ```
3. Create Application Pool in IIS set to **"No Managed Code"**.
4. Bind Application Pool to the IIS Web Site.

---

## Health Checks & Monitoring

- **Health Check Endpoint**: `GET /health`
  - Returns `Healthy` if SQL Server database connection and Hangfire job subsystem are operational.
- **Hangfire Dashboard**: `http://<server-url>/hangfire`
  - Displays real-time job execution queue, active worker threads, processing history, retries, and failed jobs.
