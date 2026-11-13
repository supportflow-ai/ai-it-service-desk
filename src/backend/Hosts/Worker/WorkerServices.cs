using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Platform;
using ServiceDesk.Modules.AIAssistance;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Ticketing;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

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
        services.AddPersistence(configuration);
        services.AddStorage(configuration);
        services.AddClock();
        // Ticketing tra vai trò/tên hiển thị qua danh bạ người dùng do Identity công bố.
        services.AddIdentityUserDirectory();
        services.AddTicketingModule();
        services.AddAIAssistanceModule(configuration);
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        return services;
    }
}
