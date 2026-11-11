using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;
using ServiceDesk.Modules.Ticketing.Application.GetMyTickets;
using ServiceDesk.Modules.Ticketing.Application.GetTicketDetail;
using ServiceDesk.Modules.Ticketing.Application.UpdateTicket;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Presentation;

/// <summary>Request body cập nhật thông tin ticket.</summary>
public record UpdateTicketRequest(string Title, string Description, string CategoryId, uint? ExpectedRevision = null);

public static partial class TicketEndpoints
{
    /// <summary>Endpoint tạo, đọc và cập nhật ticket.</summary>
    private static void MapTicketCrud(RouteGroupBuilder group)
    {
        // POST /api/tickets: Tạo vé mới (yêu cầu quyền Requester)
        group.MapPost("/", async (
            [FromBody] CreateTicketCommand command,
            HttpContext context,
            ICreateTicket createTicket,
            CancellationToken ct) =>
        {
            var hasKey = context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues);
            if (hasKey && (keyValues.Count != 1 || string.IsNullOrWhiteSpace(keyValues[0])))
                throw new InvalidInputException("idempotency_key_invalid", "Chỉ được gửi một header Idempotency-Key không rỗng.");

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
            string? scope = null,
            string? priority = null,
            bool sortByPriority = false) =>
        {
            var tickets = await getAgentQueue.ExecuteAsync(new(status, category, scope, page, pageSize, priority, sortByPriority), ct);
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
    }
}
