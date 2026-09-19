using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets");

        group.MapPost("/", async (
            [FromBody] CreateTicketCommand command,
            HttpContext context,
            ITicketService ticketService,
            ICurrentUser currentUser,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            if (currentUser.UserId == null || !Guid.TryParse(currentUser.UserId, out var requesterId))
            {
                return Results.Unauthorized();
            }

            string? idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                string cacheKey = $"idempotency:ticket:{idempotencyKey.Trim()}";
                if (cache.TryGetValue(cacheKey, out TicketDto? cachedTicket) && cachedTicket is not null)
                {
                    return Results.Created($"/api/tickets/{cachedTicket.Id}", cachedTicket);
                }
            }

            try
            {
                var ticket = await ticketService.CreateTicketAsync(command, requesterId, ct);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    string cacheKey = $"idempotency:ticket:{idempotencyKey.Trim()}";
                    cache.Set(cacheKey, ticket, TimeSpan.FromMinutes(10));
                }

                return Results.Created($"/api/tickets/{ticket.Id}", ticket);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateTicket")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ITicketService ticketService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId == null || !Guid.TryParse(currentUser.UserId, out var requesterId))
            {
                return Results.Unauthorized();
            }

            var ticket = await ticketService.GetTicketByIdAsync(id, requesterId, ct);
            return ticket is not null
                ? Results.Ok(ticket)
                : Results.NotFound();
        })
        .WithName("GetTicketById")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
