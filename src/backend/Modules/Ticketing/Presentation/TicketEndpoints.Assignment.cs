using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.AssignTicket;
using ServiceDesk.Modules.Ticketing.Application.ClaimTicket;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.UnassignTicket;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Presentation;

/// <summary>Request body phân công ticket.</summary>
public record AssignTicketRequest(Guid AgentId, uint? ExpectedRevision = null);

public static partial class TicketEndpoints
{
    /// <summary>Endpoint tiếp nhận, phân công và hủy phân công.</summary>
    private static void MapTicketAssignment(RouteGroupBuilder group)
    {
        // POST /api/tickets/{id}/claim: Kỹ thuật viên tiếp nhận xử lý vé (Submitted -> InProgress)
        group.MapPost("/{id:guid}/claim", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            IClaimTicket claimTicket,
            CancellationToken ct) =>
        {
            var ticket = await claimTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("ClaimTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/assign: Phân công ticket cho kỹ thuật viên
        group.MapPost("/{id:guid}/assign", async (
            Guid id,
            [FromBody] AssignTicketRequest request,
            [FromServices] IAssignTicket assignTicket,
            CancellationToken ct) =>
        {
            var ticket = await assignTicket.ExecuteAsync(new(id, request.AgentId, request.ExpectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("AssignTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/unassign: Hủy phân công ticket
        group.MapPost("/{id:guid}/unassign", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            [FromServices] IUnassignTicket unassignTicket,
            CancellationToken ct) =>
        {
            var ticket = await unassignTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("UnassignTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
