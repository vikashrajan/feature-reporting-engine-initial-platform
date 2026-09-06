using ReportingEngine.Application.DTOs;

namespace ReportingEngine.Application.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(long id, UpdateCustomerRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IDataSourceService
{
    Task<IReadOnlyList<DataSourceDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSourceDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<DataSourceDto> CreateAsync(CreateDataSourceRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<DataSourceDto> UpdateAsync(long id, UpdateDataSourceRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ScheduleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ScheduleDto> CreateAsync(CreateScheduleRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<ScheduleDto> UpdateAsync(long id, UpdateScheduleRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IFileConfigurationService
{
    Task<IReadOnlyList<FileConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FileConfigurationDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<FileConfigurationDto> CreateAsync(CreateFileConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<FileConfigurationDto> UpdateAsync(long id, UpdateFileConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IDeliveryConfigurationService
{
    Task<IReadOnlyList<DeliveryConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DeliveryConfigurationDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<DeliveryConfigurationDto> CreateAsync(CreateDeliveryConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<DeliveryConfigurationDto> UpdateAsync(long id, UpdateDeliveryConfigurationRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<IReadOnlyList<ReportDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReportDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDto> CreateAsync(CreateReportRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task<ReportDto> UpdateAsync(long id, UpdateReportRequest request, string performedBy, CancellationToken cancellationToken = default);
    Task ActivateAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task PauseAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task ResumeAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task RunNowAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecutionDto>> GetExecutionsAsync(long reportId, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, string performedBy, CancellationToken cancellationToken = default);
}

public interface IExecutionService
{
    Task<JobExecutionDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task RetryAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task CancelAsync(long id, string performedBy, CancellationToken cancellationToken = default);
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardDetailDto>> GetDashboardDetailsAsync(string category, CancellationToken cancellationToken = default);
}

public interface IEmailSettingsService
{
    EmailSettingsDto GetSettings();
    EmailSettingsDto UpdateSettings(UpdateEmailSettingsRequest request);
    Task<bool> TestEmailAsync(TestEmailRequest request, CancellationToken cancellationToken = default);
}

