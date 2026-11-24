using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Reports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.SharedKernel.Web;

namespace ServiceDesk.Modules.Ticketing.Presentation;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").AddEndpointFilter<ApiErrorFilter>();

        // GET /api/reports/sla-performance: Báo cáo chỉ số tuân thủ SLA và thời gian xử lý trung bình (MTTA, MTTR)
        group.MapGet("/sla-performance", async (
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] Guid? teamId,
            [FromServices] IGetSlaPerformanceReport getSlaReport,
            CancellationToken ct) =>
        {
            var report = await getSlaReport.ExecuteAsync(new(from, to, teamId), ct);
            return Results.Ok(report);
        })
        .WithName("GetSlaPerformanceReport")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<SlaPerformanceReportDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/reports/ticket-distribution: Báo cáo phân bố vé theo trạng thái, mức ưu tiên, danh mục và kỹ thuật viên
        group.MapGet("/ticket-distribution", async (
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] Guid? teamId,
            [FromServices] IGetTicketDistributionReport getDistributionReport,
            CancellationToken ct) =>
        {
            var report = await getDistributionReport.ExecuteAsync(new(from, to, teamId), ct);
            return Results.Ok(report);
        })
        .WithName("GetTicketDistributionReport")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketDistributionReportDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
