using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;

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
            CancellationToken ct) =>
        {
            if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var requesterId))
                return Results.Unauthorized();

            var hasKey = context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues);
            if (hasKey && (keyValues.Count != 1 || string.IsNullOrWhiteSpace(keyValues[0])))
                return Results.BadRequest(new { error = "Supply a single non-empty Idempotency-Key." });

            try
            {
                var ticket = hasKey
                    ? await ticketService.CreateTicketIdempotentlyAsync(command, requesterId, keyValues[0]!, ct)
                    : await ticketService.CreateTicketAsync(command, requesterId, ct);
                return Results.Created($"/api/tickets/{ticket.Id}", ticket);
            }
            catch (IdempotencyConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (IdempotencyInProgressException ex)
            {
                context.Response.Headers.RetryAfter = "1";
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
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
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/me", async (
            ITicketService ticketService,
            ICurrentUser currentUser,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
        {
            if (currentUser.UserId == null || !Guid.TryParse(currentUser.UserId, out var requesterId))
            {
                return Results.Unauthorized();
            }

            if (page < 1 || pageSize < 1 || pageSize > 100 || page > (int.MaxValue / pageSize) + 1)
            {
                return Results.BadRequest(new { error = "page must be at least 1 and pageSize must be between 1 and 100" });
            }

            var tickets = await ticketService.GetTicketsForRequesterAsync(requesterId, page, pageSize, ct);
            return Results.Ok(tickets);
        })
        .WithName("GetMyTickets")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<PagedTicketResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ITicketService ticketService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId == null || !Guid.TryParse(currentUser.UserId, out var requesterId))
                return Results.Unauthorized();

            var ticket = await ticketService.GetTicketByIdAsync(id, requesterId, ct);
            return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
        })
        .WithName("GetTicketById")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
