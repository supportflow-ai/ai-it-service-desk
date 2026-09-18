using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            ITicketService ticketService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId == null)
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(currentUser.UserId, out var requesterId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var ticket = await ticketService.CreateTicketAsync(command, requesterId, ct);
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

        return app;
    }
}
