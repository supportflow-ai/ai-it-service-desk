using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using ServiceDesk.SharedKernel.Web;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.Modules.Ticketing.Application.GetMyTickets;
using ServiceDesk.Modules.Ticketing.Application.GetTicketDetail;
using ServiceDesk.Modules.Ticketing.Application.UpdateTicket;
using ServiceDesk.Modules.Ticketing.Application.ClaimTicket;
using ServiceDesk.Modules.Ticketing.Application.ResolveTicket;
using ServiceDesk.Modules.Ticketing.Application.ReopenTicket;
using ServiceDesk.Modules.Ticketing.Application.CloseTicket;
using ServiceDesk.Modules.Ticketing.Application.CancelTicket;
using ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;
using ServiceDesk.Modules.Ticketing.Application.AssignTicket;
using ServiceDesk.Modules.Ticketing.Application.UnassignTicket;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.AddInternalNote;
using ServiceDesk.Modules.Ticketing.Application.GetTicketTimeline;
using ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl;
using ServiceDesk.Modules.Ticketing.Application.GetAttachmentDownloadUrl;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments;
using ServiceDesk.SharedKernel.Authorization;


namespace ServiceDesk.Modules.Ticketing.Presentation;

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
