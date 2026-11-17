using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.GetTicketAnalytics;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Presentation;

public static partial class TicketEndpoints
{
    /// <summary>Endpoint thống kê phân tích số liệu và SLA.</summary>
    private static void MapTicketAnalytics(RouteGroupBuilder group)
    {
        // GET /api/tickets/analytics: Báo cáo phân tích hiệu suất và chất lượng dịch vụ
        group.MapGet("/analytics", async (
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromServices] IGetTicketAnalytics getTicketAnalytics,
            CancellationToken ct) =>
        {
            var result = await getTicketAnalytics.ExecuteAsync(new(from, to), ct);
            return Results.Ok(result);
        })
        .WithName("GetTicketAnalytics")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<TicketAnalyticsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
