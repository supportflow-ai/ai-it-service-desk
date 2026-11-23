using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.CancelTicket;
using ServiceDesk.Modules.Ticketing.Application.CloseTicket;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.PendingTicket;
using ServiceDesk.Modules.Ticketing.Application.ReopenTicket;
using ServiceDesk.Modules.Ticketing.Application.ResolveTicket;
using ServiceDesk.Modules.Ticketing.Application.ResumeTicket;
using ServiceDesk.Modules.Ticketing.Application.TriageTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Presentation;

/// <summary>Request body chuyển trạng thái phiếu sang tạm dừng.</summary>
public record PendingTicketRequest(TicketStatus Status, uint? ExpectedRevision = null);

public static partial class TicketEndpoints
{
    /// <summary>Endpoint chuyển trạng thái giải quyết, mở lại, đóng, hủy, tạm dừng và tiếp tục.</summary>
    private static void MapTicketLifecycle(RouteGroupBuilder group)
    {
        // POST /api/tickets/{id}/pending: Tạm dừng xử lý vé (InProgress -> PendingUser hoặc PendingExternal)
        group.MapPost("/{id:guid}/pending", async (
            Guid id,
            [FromBody] PendingTicketRequest request,
            [FromServices] IPendingTicket pendingTicket,
            CancellationToken ct) =>
        {
            var ticket = await pendingTicket.ExecuteAsync(new(id, request.Status, request.ExpectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("PendingTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/tickets/{id}/resume: Tiếp tục xử lý vé (PendingUser hoặc PendingExternal -> InProgress)
        group.MapPost("/{id:guid}/resume", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            [FromServices] IResumeTicket resumeTicket,
            CancellationToken ct) =>
        {
            var ticket = await resumeTicket.ExecuteAsync(new(id, expectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("ResumeTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
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

        // POST /api/tickets/{id}/triage: Phân loại thủ công mức độ ảnh hưởng và mức độ khẩn cấp (DEC-09)
        group.MapPost("/{id:guid}/triage", async (
            Guid id,
            [FromBody] TriageTicketRequest request,
            [FromServices] ITriageTicket triageTicket,
            CancellationToken ct) =>
        {
            var ticket = await triageTicket.ExecuteAsync(new(id, request.Impact, request.Urgency, request.ExpectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("TriageTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
