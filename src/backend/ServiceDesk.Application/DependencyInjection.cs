using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Identity.LoginUser;
using ServiceDesk.Application.Identity.GetCurrentUser;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Application.Ticketing.GetMyTickets;
using ServiceDesk.Application.Ticketing.GetTicketDetail;

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
        services.AddScoped<ICreateTicket, CreateTicketUseCase>();
        services.AddScoped<IGetMyTickets, GetMyTicketsUseCase>();
        services.AddScoped<IGetTicketDetail, GetTicketDetailUseCase>();
        return services;
    }

    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUser, RegisterUserUseCase>();
        services.AddScoped<ILoginUser, LoginUserUseCase>();
        services.AddScoped<IGetCurrentUser, GetCurrentUserUseCase>();
        return services;
    }
}
