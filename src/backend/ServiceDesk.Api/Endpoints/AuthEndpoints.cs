using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Identity.LoginUser;
using ServiceDesk.Application.Identity.GetCurrentUser;
using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            IRegisterUser registerUser,
            CancellationToken ct) =>
        {
            var result = await registerUser.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { errors = result.Errors });
            }

            return Results.Created("/api/auth/me", new
            {
                token = result.Token,
                user = result.User
            });
        })
        .WithName("Register")
        .AllowAnonymous();

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            ILoginUser loginUser,
            CancellationToken ct) =>
        {
            var result = await loginUser.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                token = result.Token,
                user = result.User
            });
        })
        .WithName("Login")
        .AllowAnonymous();

        group.MapGet("/me", async (
            IGetCurrentUser getCurrentUser,
            CancellationToken ct) =>
        {
            var user = await getCurrentUser.ExecuteAsync(ct);
            if (user == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(user);
        })
        .AddEndpointFilter<LegacyErrorFilter>()
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        return app;
    }
}
