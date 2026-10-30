using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.GetMyTickets;
using ServiceDesk.Application.Ticketing.GetTicketDetail;
using ServiceDesk.Application.Ticketing.UpdateTicket;
using ServiceDesk.Application.Ticketing.ClaimTicket;
using ServiceDesk.Application.Ticketing.ResolveTicket;
using ServiceDesk.Application.Ticketing.ReopenTicket;
using ServiceDesk.Application.Ticketing.CloseTicket;
using ServiceDesk.Application.Ticketing.CancelTicket;
using ServiceDesk.Application.Ticketing.GetAgentQueue;
using ServiceDesk.Application.Ticketing.AssignTicket;
using ServiceDesk.Application.Ticketing.UnassignTicket;
using ServiceDesk.Application.Ticketing.AddComment;
using ServiceDesk.Application.Ticketing.AddInternalNote;
using ServiceDesk.Application.Ticketing.GetTicketTimeline;
using ServiceDesk.Application.Ticketing.GenerateAttachmentUploadUrl;
using ServiceDesk.Application.Ticketing.GetAttachmentDownloadUrl;
using ServiceDesk.Application.Ticketing.ConfirmAttachmentUpload;
using ServiceDesk.Application.Ticketing.GetTicketAttachments;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Request body cập nhật thông tin ticket.</summary>
public record UpdateTicketRequest(string Title, string Description, string CategoryId, uint? ExpectedRevision = null);

/// <summary>Request body phân công ticket.</summary>
public record AssignTicketRequest(Guid AgentId, uint? ExpectedRevision = null);

/// <summary>Request body gửi bình luận công khai.</summary>
public record AddCommentRequest(string Content);

/// <summary>Request body gửi ghi chú nội bộ.</summary>
public record AddInternalNoteRequest(string Content);

/// <summary>
/// Định nghĩa các API endpoints cho phân hệ quản lý vé (Ticket Management).
/// Áp dụng bộ lọc lỗi LegacyErrorFilter để chuẩn hóa các lỗi nghiệp vụ về mã HTTP tương ứng.
/// </summary>
public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets").AddEndpointFilter<LegacyErrorFilter>();

        // POST /api/tickets: Tạo vé mới (yêu cầu quyền Requester)
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

        // GET /api/tickets/me: Lấy danh sách vé của người dùng hiện tại có phân trang
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

        // GET /api/tickets/queue: Hàng đợi vé dành cho Agent/Admin có lọc và phân trang
        group.MapGet("/queue", async (
            [FromServices] IGetAgentQueue getAgentQueue,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10,
            int? status = null,
            string? category = null,
            string? scope = null) =>
        {
            var tickets = await getAgentQueue.ExecuteAsync(new(status, category, scope, page, pageSize), ct);
            return Results.Ok(tickets);
        })
        .WithName("GetAgentQueue")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<PagedTicketResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/tickets/{id}: Lấy thông tin chi tiết một vé
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

        // POST /api/tickets/{id}/update: Cập nhật thông tin vé khi còn ở trạng thái Submitted
        group.MapPost("/{id:guid}/update", async (
            Guid id,
            [FromBody] UpdateTicketRequest request,
            IUpdateTicket updateTicket,
            CancellationToken ct) =>
        {
            var ticket = await updateTicket.ExecuteAsync(new(id, request.Title, request.Description, request.CategoryId, request.ExpectedRevision), ct);
            return Results.Ok(ticket);
        })
        .WithName("UpdateTicket")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

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

        // POST /api/tickets/{id}/comments: Gửi bình luận công khai
        group.MapPost("/{id:guid}/comments", async (
            Guid id,
            [FromBody] AddCommentRequest request,
            [FromServices] IAddComment addComment,
            CancellationToken ct) =>
        {
            var comment = await addComment.ExecuteAsync(new(id, request.Content), ct);
            return Results.Created($"/api/tickets/{id}/comments/{comment.Id}", comment);
        })
        .WithName("AddTicketComment")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TimelineItemDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/internal-notes: Gửi ghi chú nội bộ (chỉ Agent/Admin)
        group.MapPost("/{id:guid}/internal-notes", async (
            Guid id,
            [FromBody] AddInternalNoteRequest request,
            [FromServices] IAddInternalNote addInternalNote,
            CancellationToken ct) =>
        {
            var note = await addInternalNote.ExecuteAsync(new(id, request.Content), ct);
            return Results.Created($"/api/tickets/{id}/internal-notes/{note.Id}", note);
        })
        .WithName("AddTicketInternalNote")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TimelineItemDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/tickets/{id}/timeline: Lấy dòng hoạt động & trao đổi
        group.MapGet("/{id:guid}/timeline", async (
            Guid id,
            [FromServices] IGetTicketTimeline getTicketTimeline,
            CancellationToken ct) =>
        {
            var timeline = await getTicketTimeline.ExecuteAsync(new(id), ct);
            return Results.Ok(timeline);
        })
        .WithName("GetTicketTimeline")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<IReadOnlyList<TimelineItemDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/attachments/upload-url: Sinh pre-signed URL tải tệp đính kèm
        group.MapPost("/{id:guid}/attachments/upload-url", async (
            Guid id,
            [FromBody] GenerateUploadUrlRequest request,
            [FromServices] IGenerateAttachmentUploadUrl generateUploadUrl,
            CancellationToken ct) =>
        {
            var result = await generateUploadUrl.ExecuteAsync(new(id, request), ct);
            return Results.Ok(result);
        })
        .WithName("GenerateAttachmentUploadUrl")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<UploadUrlResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/tickets/{id}/attachments/download-url: Sinh pre-signed URL tải xuống
        group.MapGet("/{id:guid}/attachments/download-url", async (
            Guid id,
            string storageKey,
            [FromServices] IGetAttachmentDownloadUrl getDownloadUrl,
            CancellationToken ct) =>
        {
            var result = await getDownloadUrl.ExecuteAsync(new(id, storageKey), ct);
            return Results.Ok(result);
        })
        .WithName("GetAttachmentDownloadUrl")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<DownloadUrlResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/attachments/confirm: Lưu metadata sau khi upload thành công
        group.MapPost("/{id:guid}/attachments/confirm", async (
            Guid id,
            [FromBody] ConfirmAttachmentUploadRequest request,
            [FromServices] IConfirmAttachmentUpload confirmUpload,
            CancellationToken ct) =>
        {
            var result = await confirmUpload.ExecuteAsync(new(id, request), ct);
            return Results.Created($"/api/tickets/{id}/attachments/{result.Id}", result);
        })
        .WithName("ConfirmAttachmentUpload")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketAttachmentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/tickets/{id}/attachments: Lấy danh sách tệp đính kèm của ticket
        group.MapGet("/{id:guid}/attachments", async (
            Guid id,
            [FromServices] IGetTicketAttachments getAttachments,
            CancellationToken ct) =>
        {
            var attachments = await getAttachments.ExecuteAsync(new(id), ct);
            return Results.Ok(attachments);
        })
        .WithName("GetTicketAttachments")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<IReadOnlyList<TicketAttachmentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
