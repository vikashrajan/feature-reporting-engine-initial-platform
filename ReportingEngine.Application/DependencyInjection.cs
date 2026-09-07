using Microsoft.Extensions.DependencyInjection;
using ReportingEngine.Application.Services;

namespace ReportingEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IFileConfigurationService, FileConfigurationService>();
        services.AddScoped<IDeliveryConfigurationService, DeliveryConfigurationService>();
        services.AddScoped<ISmtpConfigurationService, SmtpConfigurationService>();
        services.AddScoped<IJobFailureNotificationProfileService, JobFailureNotificationProfileService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();
        return services;
    }
}
