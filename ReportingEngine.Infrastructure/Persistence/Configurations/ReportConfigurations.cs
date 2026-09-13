using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReportingEngine.Domain.Entities;

namespace ReportingEngine.Infrastructure.Persistence.Configurations;

public sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("RepScdhedularProject_ReportDefinition");
        builder.HasKey(x => x.ReportId);
        builder.Property(x => x.ReportId).ValueGeneratedOnAdd();
        builder.Property(x => x.ReportCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReportName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.QueryText).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.VersionNumber).IsRequired().HasDefaultValue(1);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired().HasDefaultValue("DRAFT");
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasIndex(x => x.ReportCode).IsUnique();

        builder.HasOne(x => x.Customer).WithMany(x => x.Reports).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DataSource).WithMany(x => x.Reports).HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Schedule).WithMany(x => x.Reports).HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FileConfiguration).WithMany(x => x.Reports).HasForeignKey(x => x.FileConfigId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeliveryConfiguration).WithMany(x => x.Reports).HasForeignKey(x => x.DeliveryConfigId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReportParameterConfiguration : IEntityTypeConfiguration<ReportParameter>
{
    public void Configure(EntityTypeBuilder<ReportParameter> builder)
    {
        builder.ToTable("RepScdhedularProject_ReportParameter");
        builder.HasKey(x => x.ParameterId);
        builder.Property(x => x.ParameterId).ValueGeneratedOnAdd();
        builder.Property(x => x.ParameterName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ParameterType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ParameterValue).HasMaxLength(1000);
        builder.Property(x => x.ValueSource).HasMaxLength(50).IsRequired();
        builder.HasOne(x => x.Report).WithMany(x => x.Parameters).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ReportId, x.ParameterName }).IsUnique();
    }
}

public sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("RepScdhedularProject_JobExecution");
        builder.HasKey(x => x.ExecutionId);
        builder.Property(x => x.ExecutionId).ValueGeneratedOnAdd();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ExecutionQuery).HasColumnType("nvarchar(max)");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => new { x.ReportId, x.Status });
        builder.HasOne(x => x.Report).WithMany(x => x.Executions).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FileExecutionConfiguration : IEntityTypeConfiguration<FileExecution>
{
    public void Configure(EntityTypeBuilder<FileExecution> builder)
    {
        builder.ToTable("RepScdhedularProject_FileExecution");
        builder.HasKey(x => x.FileExecutionId);
        builder.Property(x => x.FileExecutionId).ValueGeneratedOnAdd();
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(1000);
        builder.Property(x => x.GenerationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DeliveryStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(200);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(x => new { x.ExecutionId, x.SequenceNumber }).IsUnique();
        builder.HasOne(x => x.Execution).WithMany(x => x.Files).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("RepScdhedularProject_AuditLog");
        builder.HasKey(x => x.AuditId);
        builder.Property(x => x.AuditId).ValueGeneratedOnAdd();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OldValue).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValue).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PerformedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PerformedAt).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
