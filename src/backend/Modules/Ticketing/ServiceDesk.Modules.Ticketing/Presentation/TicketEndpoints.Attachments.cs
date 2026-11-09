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
    /// <summary>Endpoint tệp đính kèm.</summary>
    private static void MapTicketAttachments(RouteGroupBuilder group)
    {
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
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status503ServiceUnavailable);

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
    }
}
