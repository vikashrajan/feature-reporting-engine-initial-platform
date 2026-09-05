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
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        return services;
    }
}
