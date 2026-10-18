using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity;
using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Application.Ticketing.Interfaces;

namespace ServiceDesk.Application;

/// <summary>
/// Registers Application layer services.
/// Called from the API composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddTicketingApplication().AddIdentityApplication();
    }

    public static IServiceCollection AddTicketingApplication(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        return services;
    }

    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
