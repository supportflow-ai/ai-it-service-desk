using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Identity.Models;

namespace ServiceDesk.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.RegisterAsync(request, ct);
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
            IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request, ct);
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
            IAuthService authService,
            System.Security.Claims.ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Results.Unauthorized();
            }

            var user = await authService.GetCurrentUserAsync(userId, ct);
            if (user == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(user);
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        return app;
    }
}
