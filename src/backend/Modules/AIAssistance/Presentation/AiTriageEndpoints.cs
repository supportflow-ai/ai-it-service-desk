using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Web;

namespace ServiceDesk.Modules.AIAssistance.Presentation;

public static class AiTriageEndpoints
{
    public static IEndpointRouteBuilder MapAiTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("AI Triage").AddEndpointFilter<ApiErrorFilter>();

        // POST /api/tickets/{id}/ai-analyze: Kích hoạt phân loại AI theo yêu cầu
        group.MapPost("/{id:guid}/ai-analyze", async (
            Guid id,
            [FromServices] IRequestAiTriage requestAiTriage,
            CancellationToken ct) =>
        {
            var result = await requestAiTriage.ExecuteAsync(id, ct);
            return Results.Ok(result);
        })
        .WithName("TriggerTicketAiAnalysis")
        .RequireAuthorization("RequireAgent")
        .Produces<AgentTriageView>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/tickets/{id}/triage: Lấy kết quả gợi ý AI Triage (phân quyền xem theo Requester / Agent)
        group.MapGet("/{id:guid}/triage", async (
            Guid id,
            [FromServices] IGetTicketTriage getTicketTriage,
            CancellationToken ct) =>
        {
            var result = await getTicketTriage.ExecuteAsync(id, ct);
            return result.StaffView is not null ? Results.Ok(result.StaffView) : Results.Ok(result.RequesterView);
        })
        .WithName("GetTicketAiTriage")
        .RequireAuthorization()
        .Produces<AgentTriageView>(StatusCodes.Status200OK)
        .Produces<RequesterTriageView>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tickets/{id}/triage/decision: Phê duyệt gợi ý AI (Accept, Override, Reject - HITL)
        group.MapPost("/{id:guid}/triage/decision", async (
            Guid id,
            [FromBody] TriageDecisionRequest request,
            [FromServices] IDecideAiTriage decideAiTriage,
            CancellationToken ct) =>
        {
            var command = new TriageDecisionCommand(
                id,
                request.Decision,
                request.Impact,
                request.Urgency,
                request.CategoryId,
                request.Reason,
                request.ExpectedRevision);

            var decisionResult = await decideAiTriage.ExecuteAsync(command, ct);
            return Results.Ok(decisionResult);
        })
        .WithName("DecideTicketAiTriage")
        .RequireAuthorization("RequireAgent")
        .Produces<TriageDecisionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}

public record TriageDecisionRequest(
    TriageDecisionType Decision,
    Impact? Impact = null,
    Urgency? Urgency = null,
    string? CategoryId = null,
    string? Reason = null,
    uint? ExpectedRevision = null
);
