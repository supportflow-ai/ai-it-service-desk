using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Infrastructure;

namespace ServiceDesk.Worker;

/// <summary>
/// Đăng ký các dịch vụ phụ thuộc cần thiết cho Background Worker:
/// - Không phụ thuộc HttpContext (dùng AnonymousCurrentUser).
/// - Chỉ đăng ký các module nghiệp vụ và hạ tầng phục vụ tác vụ ngầm.
/// </summary>
public static class WorkerServices
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTicketingApplication();
        services.AddPersistence(configuration);
        services.AddClock();
        services.AddAiAdapters();
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        return services;
    }
}
