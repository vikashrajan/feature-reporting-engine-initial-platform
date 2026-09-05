using ReportingEngine.Domain.Entities;

namespace ReportingEngine.Application.Abstractions.Repositories;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Customer?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Customer entity, CancellationToken cancellationToken = default);
}

public interface IDataSourceRepository
{
    Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSource?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<DataSource> AddAsync(DataSource entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataSource entity, CancellationToken cancellationToken = default);
}

public interface IScheduleRepository
{
    Task<IReadOnlyList<Schedule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Schedule?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Schedule> AddAsync(Schedule entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Schedule entity, CancellationToken cancellationToken = default);
}

public interface IFileConfigurationRepository
{
    Task<IReadOnlyList<FileConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FileConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<FileConfiguration> AddAsync(FileConfiguration entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FileConfiguration entity, CancellationToken cancellationToken = default);
}

public interface IDeliveryConfigurationRepository
{
    Task<IReadOnlyList<DeliveryConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DeliveryConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<DeliveryConfiguration> AddAsync(DeliveryConfiguration entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(DeliveryConfiguration entity, CancellationToken cancellationToken = default);
}

public interface IReportRepository
{
    Task<IReadOnlyList<ReportDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReportDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDefinition?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportDefinition>> GetActiveReportsAsync(CancellationToken cancellationToken = default);
    Task<ReportDefinition> AddAsync(ReportDefinition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ReportDefinition entity, CancellationToken cancellationToken = default);
}

public interface IJobExecutionRepository
{
    Task<JobExecution?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<JobExecution?> GetByIdWithFilesAsync(long id, CancellationToken cancellationToken = default);
    Task<JobExecution?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecution>> GetByReportIdAsync(long reportId, CancellationToken cancellationToken = default);
    Task<JobExecution?> GetLastSuccessfulAsync(long reportId, CancellationToken cancellationToken = default);
    Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);
    Task<JobExecution> AddAsync(JobExecution entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(JobExecution entity, CancellationToken cancellationToken = default);
}

public interface IFileExecutionRepository
{
    Task<FileExecution> AddAsync(FileExecution entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FileExecution entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileExecution>> GetByExecutionIdAsync(long executionId, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DashboardCounts(int Running, int Successful, int Failed, int Upcoming);
