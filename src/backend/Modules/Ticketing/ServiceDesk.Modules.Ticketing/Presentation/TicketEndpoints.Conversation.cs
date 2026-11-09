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
    /// <summary>Endpoint bình luận, ghi chú nội bộ và dòng thời gian.</summary>
    private static void MapTicketConversation(RouteGroupBuilder group)
    {
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
    }
}
