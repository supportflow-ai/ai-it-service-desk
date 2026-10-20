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
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Request body cập nhật thông tin ticket.</summary>
public record UpdateTicketRequest(string Title, string Description, string CategoryId);

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
            var ticket = await updateTicket.ExecuteAsync(new(id, request.Title, request.Description, request.CategoryId), ct);
            return Results.Ok(ticket);
        })
        .WithName("UpdateTicket")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/claim: Kỹ thuật viên tiếp nhận xử lý vé (Submitted -> InProgress)
        group.MapPost("/{id:guid}/claim", async (
            Guid id,
            IClaimTicket claimTicket,
            CancellationToken ct) =>
        {
            var ticket = await claimTicket.ExecuteAsync(new(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("ClaimTicket")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/resolve: Kỹ thuật viên / Admin đánh dấu giải quyết xong (InProgress -> Resolved)
        group.MapPost("/{id:guid}/resolve", async (
            Guid id,
            IResolveTicket resolveTicket,
            CancellationToken ct) =>
        {
            var ticket = await resolveTicket.ExecuteAsync(new(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("ResolveTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/reopen: Mở lại vé nếu sự cố tái diễn hoặc chưa triệt để (Resolved -> InProgress)
        group.MapPost("/{id:guid}/reopen", async (
            Guid id,
            IReopenTicket reopenTicket,
            CancellationToken ct) =>
        {
            var ticket = await reopenTicket.ExecuteAsync(new(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("ReopenTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/close: Đóng vé hoàn tất sau khi nghiệm thu (Resolved -> Closed)
        group.MapPost("/{id:guid}/close", async (
            Guid id,
            ICloseTicket closeTicket,
            CancellationToken ct) =>
        {
            var ticket = await closeTicket.ExecuteAsync(new(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("CloseTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/cancel: Hủy vé khi còn ở trạng thái chờ tiếp nhận (Submitted -> Cancelled)
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            ICancelTicket cancelTicket,
            CancellationToken ct) =>
        {
            var ticket = await cancelTicket.ExecuteAsync(new(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("CancelTicket")
        .RequireAuthorization()
        .Produces<TicketDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
