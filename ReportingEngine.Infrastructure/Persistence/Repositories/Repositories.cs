using Microsoft.EntityFrameworkCore;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReportingEngineDbContext _db;
    public UnitOfWork(ReportingEngineDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ReportingEngineDbContext _db;
    public CustomerRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Customers.AsNoTracking().OrderBy(x => x.CustomerName).ToListAsync(cancellationToken);

    public Task<Customer?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == id, cancellationToken);

    public Task<Customer?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(x => x.CustomerCode == code, cancellationToken);

    public async Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default)
    {
        await _db.Customers.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(Customer entity, CancellationToken cancellationToken = default)
    {
        _db.Customers.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class DataSourceRepository : IDataSourceRepository
{
    private readonly ReportingEngineDbContext _db;
    public DataSourceRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.DataSources.AsNoTracking().OrderBy(x => x.DataSourceName).ToListAsync(cancellationToken);

    public Task<DataSource?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.DataSources.FirstOrDefaultAsync(x => x.DataSourceId == id, cancellationToken);

    public async Task<DataSource> AddAsync(DataSource entity, CancellationToken cancellationToken = default)
    {
        await _db.DataSources.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(DataSource entity, CancellationToken cancellationToken = default)
    {
        _db.DataSources.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class ScheduleRepository : IScheduleRepository
{
    private readonly ReportingEngineDbContext _db;
    public ScheduleRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<Schedule>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Schedules.AsNoTracking().OrderBy(x => x.ScheduleName).ToListAsync(cancellationToken);

    public Task<Schedule?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Schedules.FirstOrDefaultAsync(x => x.ScheduleId == id, cancellationToken);

    public async Task<Schedule> AddAsync(Schedule entity, CancellationToken cancellationToken = default)
    {
        await _db.Schedules.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(Schedule entity, CancellationToken cancellationToken = default)
    {
        _db.Schedules.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class FileConfigurationRepository : IFileConfigurationRepository
{
    private readonly ReportingEngineDbContext _db;
    public FileConfigurationRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<FileConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.FileConfigurations.AsNoTracking().OrderBy(x => x.ConfigurationName).ToListAsync(cancellationToken);

    public Task<FileConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.FileConfigurations.FirstOrDefaultAsync(x => x.FileConfigId == id, cancellationToken);

    public async Task<FileConfiguration> AddAsync(FileConfiguration entity, CancellationToken cancellationToken = default)
    {
        await _db.FileConfigurations.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(FileConfiguration entity, CancellationToken cancellationToken = default)
    {
        _db.FileConfigurations.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class DeliveryConfigurationRepository : IDeliveryConfigurationRepository
{
    private readonly ReportingEngineDbContext _db;
    public DeliveryConfigurationRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<DeliveryConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.DeliveryConfigurations.AsNoTracking().OrderBy(x => x.DeliveryName).ToListAsync(cancellationToken);

    public Task<DeliveryConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.DeliveryConfigurations.FirstOrDefaultAsync(x => x.DeliveryConfigId == id, cancellationToken);

    public async Task<DeliveryConfiguration> AddAsync(DeliveryConfiguration entity, CancellationToken cancellationToken = default)
    {
        await _db.DeliveryConfigurations.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(DeliveryConfiguration entity, CancellationToken cancellationToken = default)
    {
        _db.DeliveryConfigurations.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class ReportRepository : IReportRepository
{
    private readonly ReportingEngineDbContext _db;
    public ReportRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReportDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Reports.AsNoTracking().Include(x => x.Parameters).OrderBy(x => x.ReportName).ToListAsync(cancellationToken);

    public Task<ReportDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Reports.FirstOrDefaultAsync(x => x.ReportId == id, cancellationToken);

    public Task<ReportDefinition?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Reports
            .Include(x => x.Customer)
            .Include(x => x.DataSource)
            .Include(x => x.Schedule)
            .Include(x => x.FileConfiguration)
            .Include(x => x.DeliveryConfiguration)
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.ReportId == id, cancellationToken);

    public Task<ReportDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Reports.FirstOrDefaultAsync(x => x.ReportCode == code, cancellationToken);

    public async Task<IReadOnlyList<ReportDefinition>> GetActiveReportsAsync(CancellationToken cancellationToken = default) =>
        await _db.Reports
            .Include(x => x.Schedule)
            .Where(x => x.IsActive && x.Status == ReportStatuses.Active)
            .ToListAsync(cancellationToken);

    public async Task<ReportDefinition> AddAsync(ReportDefinition entity, CancellationToken cancellationToken = default)
    {
        await _db.Reports.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(ReportDefinition entity, CancellationToken cancellationToken = default)
    {
        _db.Reports.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class JobExecutionRepository : IJobExecutionRepository
{
    private readonly ReportingEngineDbContext _db;
    public JobExecutionRepository(ReportingEngineDbContext db) => _db = db;

    public Task<JobExecution?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.JobExecutions.FirstOrDefaultAsync(x => x.ExecutionId == id, cancellationToken);

    public Task<JobExecution?> GetByIdWithFilesAsync(long id, CancellationToken cancellationToken = default) =>
        _db.JobExecutions.Include(x => x.Files).FirstOrDefaultAsync(x => x.ExecutionId == id, cancellationToken);

    public Task<JobExecution?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default) =>
        _db.JobExecutions.Include(x => x.Files).FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

    public async Task<IReadOnlyList<JobExecution>> GetByReportIdAsync(long reportId, CancellationToken cancellationToken = default) =>
        await _db.JobExecutions.AsNoTracking()
            .Include(x => x.Files)
            .Where(x => x.ReportId == reportId)
            .OrderByDescending(x => x.ExecutionId)
            .ToListAsync(cancellationToken);

    public Task<JobExecution?> GetLastSuccessfulAsync(long reportId, CancellationToken cancellationToken = default) =>
        _db.JobExecutions
            .Where(x => x.ReportId == reportId && x.Status == JobExecutionStatuses.Success)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var running = await _db.JobExecutions.CountAsync(
            x => x.Status == JobExecutionStatuses.Running || x.Status == JobExecutionStatuses.Retrying, cancellationToken);
        var successful = await _db.JobExecutions.CountAsync(
            x => x.Status == JobExecutionStatuses.Success && x.CompletedAt >= since, cancellationToken);
        var failed = await _db.JobExecutions.CountAsync(
            x => x.Status == JobExecutionStatuses.Failed && x.CompletedAt >= since, cancellationToken);
        var upcoming = await _db.Reports.CountAsync(
            x => x.IsActive && x.Status == ReportStatuses.Active, cancellationToken);
        return new DashboardCounts(running, successful, failed, upcoming);
    }

    public async Task<JobExecution> AddAsync(JobExecution entity, CancellationToken cancellationToken = default)
    {
        await _db.JobExecutions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(JobExecution entity, CancellationToken cancellationToken = default)
    {
        _db.JobExecutions.Update(entity);
        return Task.CompletedTask;
    }
}

public sealed class FileExecutionRepository : IFileExecutionRepository
{
    private readonly ReportingEngineDbContext _db;
    public FileExecutionRepository(ReportingEngineDbContext db) => _db = db;

    public async Task<FileExecution> AddAsync(FileExecution entity, CancellationToken cancellationToken = default)
    {
        await _db.FileExecutions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(FileExecution entity, CancellationToken cancellationToken = default)
    {
        _db.FileExecutions.Update(entity);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<FileExecution>> GetByExecutionIdAsync(long executionId, CancellationToken cancellationToken = default) =>
        await _db.FileExecutions.Where(x => x.ExecutionId == executionId).OrderBy(x => x.SequenceNumber).ToListAsync(cancellationToken);
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ReportingEngineDbContext _db;
    public AuditLogRepository(ReportingEngineDbContext db) => _db = db;

    public async Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default) =>
        await _db.AuditLogs.AddAsync(entity, cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default) =>
        await _db.AuditLogs.AsNoTracking().OrderByDescending(x => x.AuditId).Take(take).ToListAsync(cancellationToken);
}
