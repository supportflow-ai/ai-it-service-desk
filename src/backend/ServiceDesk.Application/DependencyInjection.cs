using Microsoft.Extensions.DependencyInjection;

namespace ServiceDesk.Application;

/// <summary>
/// Registers Application layer services.
/// Called from the API composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application-level services will be registered here as they are created.
        // Examples (future): MediatR handlers, validators, etc.

        return services;
    }
}
