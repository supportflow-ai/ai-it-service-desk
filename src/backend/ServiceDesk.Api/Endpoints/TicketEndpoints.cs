using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.GetMyTickets;
using ServiceDesk.Application.Ticketing.GetTicketDetail;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets").AddEndpointFilter<LegacyErrorFilter>();

        group.MapPost("/", async (
            [FromBody] CreateTicketCommand command,
            HttpContext context,
            ICreateTicket createTicket,
            CancellationToken ct) =>
        {
            var hasKey = context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues);
            if (hasKey && (keyValues.Count != 1 || string.IsNullOrWhiteSpace(keyValues[0])))
                return Results.BadRequest(new { error = "Supply a single non-empty Idempotency-Key." });

            var ticket = await createTicket.ExecuteAsync(command, hasKey ? keyValues[0]! : null, ct);
            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
        })
        .WithName("CreateTicket")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/me", async (
            IGetMyTickets getMyTickets,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
        {
            var tickets = await getMyTickets.ExecuteAsync(new(page, pageSize), ct);
            return Results.Ok(tickets);
        })
        .WithName("GetMyTickets")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<PagedTicketResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IGetTicketDetail getTicketDetail,
            CancellationToken ct) =>
        {
            var ticket = await getTicketDetail.ExecuteAsync(new(id), ct);
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
