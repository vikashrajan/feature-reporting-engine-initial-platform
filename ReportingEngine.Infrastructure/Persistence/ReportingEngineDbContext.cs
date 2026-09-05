using Microsoft.EntityFrameworkCore;
using ReportingEngine.Domain.Entities;

namespace ReportingEngine.Infrastructure.Persistence;

public sealed class ReportingEngineDbContext : DbContext
{
    public ReportingEngineDbContext(DbContextOptions<ReportingEngineDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<FileConfiguration> FileConfigurations => Set<FileConfiguration>();
    public DbSet<DeliveryConfiguration> DeliveryConfigurations => Set<DeliveryConfiguration>();
    public DbSet<ReportDefinition> Reports => Set<ReportDefinition>();
    public DbSet<ReportParameter> ReportParameters => Set<ReportParameter>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<FileExecution> FileExecutions => Set<FileExecution>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingEngineDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
