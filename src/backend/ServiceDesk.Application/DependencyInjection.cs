using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Auth;

namespace ServiceDesk.Application;

/// <summary>
/// Registers Application layer services.
/// Called from the API composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthHandlers>();

        return services;
    }
}
