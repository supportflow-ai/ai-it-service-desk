using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Application.Auth;

namespace ServiceDesk.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth")
            .AddEndpointFilter<ApiExceptionEndpointFilter>()
            .WithTags("Auth");

        auth.MapPost("/register", async (RegisterRequest request, AuthHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.RegisterAsync(request, ct);
            return Results.Ok(result);
        }).AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, AuthHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.LoginAsync(request, ct);
            return Results.Ok(result);
        }).AllowAnonymous();

        auth.MapGet("/me", async (AuthHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.GetMeAsync(ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        var admin = endpoints.MapGroup("/admin")
            .AddEndpointFilter<ApiExceptionEndpointFilter>()
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(ApplicationRoles.Admin));

        admin.MapPut("/users/{id}/role", async (string id, ChangeRoleRequest request, AuthHandlers handlers, CancellationToken ct) =>
        {
            await handlers.ChangeRoleAsync(id, request.Role, ct);
            return Results.NoContent();
        });

        return endpoints;
    }
}
