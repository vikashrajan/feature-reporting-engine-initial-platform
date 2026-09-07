using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReportingEngine.Domain.Entities;

namespace ReportingEngine.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("RepScdhedularProject_Customer");
        builder.HasKey(x => x.CustomerId);
        builder.Property(x => x.CustomerId).ValueGeneratedOnAdd();
        builder.Property(x => x.CustomerCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasIndex(x => x.CustomerCode).IsUnique();
    }
}

public sealed class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("RepScdhedularProject_DataSource");
        builder.HasKey(x => x.DataSourceId);
        builder.Property(x => x.DataSourceId).ValueGeneratedOnAdd();
        builder.Property(x => x.DataSourceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DataSourceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ConnectionReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ConnectionString).HasColumnType("nvarchar(max)");
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasIndex(x => x.DataSourceName);
    }
}

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("RepScdhedularProject_Schedule");
        builder.HasKey(x => x.ScheduleId);
        builder.Property(x => x.ScheduleId).ValueGeneratedOnAdd();
        builder.Property(x => x.ScheduleName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ScheduleType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CronExpression).HasMaxLength(100);
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
    }
}

public sealed class FileConfigurationConfiguration : IEntityTypeConfiguration<FileConfiguration>
{
    public void Configure(EntityTypeBuilder<FileConfiguration> builder)
    {
        builder.ToTable("RepScdhedularProject_FileConfiguration");
        builder.HasKey(x => x.FileConfigId);
        builder.Property(x => x.FileConfigId).ValueGeneratedOnAdd();
        builder.Property(x => x.ConfigurationName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FileFormat).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FileNamePattern).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SplitEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.SplitType).HasMaxLength(30);
        builder.Property(x => x.CompressionType).HasMaxLength(30);
        builder.Property(x => x.ZipBatchSize);
        builder.Property(x => x.KeepLocalFiles).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.EncryptionEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
    }
}

public sealed class DeliveryConfigurationConfiguration : IEntityTypeConfiguration<DeliveryConfiguration>
{
    public void Configure(EntityTypeBuilder<DeliveryConfiguration> builder)
    {
        builder.ToTable("RepScdhedularProject_DeliveryConfiguration");
        builder.HasKey(x => x.DeliveryConfigId);
        builder.Property(x => x.DeliveryConfigId).ValueGeneratedOnAdd();
        builder.Property(x => x.DeliveryName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DeliveryType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DestinationReference).HasMaxLength(500);
        builder.Property(x => x.EmailTo).HasMaxLength(2000);
        builder.Property(x => x.EmailCc).HasMaxLength(2000);
        builder.Property(x => x.EmailBcc).HasMaxLength(2000);
        builder.Property(x => x.EmailSubjectTemplate).HasMaxLength(1000);
        builder.Property(x => x.EmailBodyTemplate).HasColumnType("nvarchar(max)");
        builder.Property(x => x.SecretReference).HasMaxLength(500);
        builder.Property(x => x.SmtpConfigId);
        builder.Property(x => x.IsFailureNotification).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasOne(x => x.SmtpConfiguration).WithMany(x => x.DeliveryConfigurations).HasForeignKey(x => x.SmtpConfigId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SmtpConfigurationConfiguration : IEntityTypeConfiguration<SmtpConfiguration>
{
    public void Configure(EntityTypeBuilder<SmtpConfiguration> builder)
    {
        builder.ToTable("RepScdhedularProject_SmtpConfiguration");
        builder.HasKey(x => x.SmtpConfigId);
        builder.Property(x => x.SmtpConfigId).ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Host).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Port).IsRequired();
        builder.Property(x => x.EnableSsl).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.FromDisplayName).HasMaxLength(200);
        builder.Property(x => x.UserName).HasMaxLength(320);
        builder.Property(x => x.Password).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TimeoutSeconds).IsRequired().HasDefaultValue(120);
        builder.Property(x => x.UseFileDrop).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.FileDropPath).HasMaxLength(1000);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasIndex(x => x.ProfileName).IsUnique();
    }
}

public sealed class JobFailureNotificationProfileConfiguration : IEntityTypeConfiguration<JobFailureNotificationProfile>
{
    public void Configure(EntityTypeBuilder<JobFailureNotificationProfile> builder)
    {
        builder.ToTable("RepScdhedularProject_JobFailureNotificationProfile");
        builder.HasKey(x => x.FailureProfileId);
        builder.Property(x => x.FailureProfileId).ValueGeneratedOnAdd();
        builder.Property(x => x.ProfileName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EmailTo).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EmailCc).HasMaxLength(2000);
        builder.Property(x => x.EmailBcc).HasMaxLength(2000);
        builder.Property(x => x.SubjectTemplate).HasMaxLength(1000);
        builder.Property(x => x.BodyTemplate).HasColumnType("nvarchar(max)");
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasOne(x => x.SmtpConfiguration).WithMany(x => x.FailureNotificationProfiles).HasForeignKey(x => x.SmtpConfigId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ProfileName).IsUnique();
    }
}
