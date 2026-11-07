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
    /// <summary>Endpoint chuyển trạng thái giải quyết, mở lại, đóng, hủy.</summary>
    private static void MapTicketLifecycle(RouteGroupBuilder group)
    {
        // POST /api/tickets/{id}/resolve: Kỹ thuật viên / Admin đánh dấu giải quyết xong (InProgress -> Resolved)
        group.MapPost("/{id:guid}/resolve", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            IResolveTicket resolveTicket,
            CancellationToken ct) =>
        {
            var ticket = await resolveTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("ResolveTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/reopen: Mở lại vé nếu sự cố tái diễn hoặc chưa triệt để (Resolved -> InProgress)
        group.MapPost("/{id:guid}/reopen", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            IReopenTicket reopenTicket,
            CancellationToken ct) =>
        {
            var ticket = await reopenTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("ReopenTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/close: Đóng vé hoàn tất sau khi nghiệm thu (Resolved -> Closed)
        group.MapPost("/{id:guid}/close", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            ICloseTicket closeTicket,
            CancellationToken ct) =>
        {
            var ticket = await closeTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("CloseTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/cancel: Hủy vé khi còn ở trạng thái chờ tiếp nhận (Submitted -> Cancelled)
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            ICancelTicket cancelTicket,
            CancellationToken ct) =>
        {
            var ticket = await cancelTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("CancelTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
