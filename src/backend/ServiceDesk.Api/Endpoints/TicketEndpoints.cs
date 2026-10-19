using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Application.Auth;
using ServiceDesk.Application.Ticketing;

namespace ServiceDesk.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tickets = endpoints.MapGroup("/tickets")
            .AddEndpointFilter<ApiExceptionEndpointFilter>()
            .WithTags("Tickets")
            .RequireAuthorization();

        tickets.MapPost("/", async (CreateTicketRequest request, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.CreateAsync(request, ct);
            return Results.Ok(result);
        });

        tickets.MapGet("/me", async ([AsParameters] TicketListRequest request, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.ListAsync(request, ct);
            return Results.Ok(result);
        });

        tickets.MapGet("/{id:guid}", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.GetAsync(id, ct);
            return Results.Ok(result);
        });

        tickets.MapPatch("/{id:guid}", async (Guid id, UpdateTicketRequest request, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.UpdateAsync(id, request, ct);
            return Results.Ok(result);
        });

        tickets.MapPost("/{id:guid}/claim", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.ClaimAsync(id, ct);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(ApplicationRoles.Agent, ApplicationRoles.Admin));

        tickets.MapPost("/{id:guid}/assign", async (Guid id, AssignTicketRequest request, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.AssignAsync(id, request, ct);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequireRole(ApplicationRoles.Admin));

        tickets.MapPost("/{id:guid}/resolve", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.ResolveAsync(id, ct);
            return Results.Ok(result);
        });

        tickets.MapPost("/{id:guid}/reopen", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.ReopenAsync(id, ct);
            return Results.Ok(result);
        });

        tickets.MapPost("/{id:guid}/close", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.CloseAsync(id, ct);
            return Results.Ok(result);
        });

        tickets.MapPost("/{id:guid}/cancel", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.CancelAsync(id, ct);
            return Results.Ok(result);
        });

        tickets.MapPost("/{id:guid}/comments", async (Guid id, AddCommentRequest request, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.AddCommentAsync(id, request, ct);
            return Results.Ok(result);
        });

        tickets.MapGet("/{id:guid}/comments", async (Guid id, TicketHandlers handlers, CancellationToken ct) =>
        {
            var result = await handlers.ListCommentsAsync(id, ct);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
