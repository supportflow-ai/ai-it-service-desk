using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.GetTicketCategories;
using ServiceDesk.SharedKernel.Web;

namespace ServiceDesk.Modules.Ticketing.Presentation;

/// <summary>
/// Định nghĩa các API endpoints cho phân hệ quản lý vé (Ticket Management).
/// Áp dụng bộ lọc lỗi ApiErrorFilter để chuẩn hóa các lỗi nghiệp vụ về mã HTTP tương ứng.
/// </summary>
public static partial class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets").AddEndpointFilter<ApiErrorFilter>();

        // Thứ tự đăng ký giữ nguyên để tài liệu OpenAPI không đổi.
        MapTicketCrud(group);
        MapTicketAssignment(group);
        MapTicketLifecycle(group);
        MapTicketConversation(group);
        MapTicketAttachments(group);

        // GET /api/categories: Lấy danh mục sự cố ITIL kèm mô tả và độ ưu tiên khuyến nghị
        app.MapGet("/api/categories", async (
            [FromServices] IGetTicketCategories getTicketCategories,
            CancellationToken ct) =>
        {
            var categories = await getTicketCategories.ExecuteAsync(ct);
            return Results.Ok(categories);
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithTags("Categories")
        .WithName("GetTicketCategories")
        .RequireAuthorization()
        .Produces<IReadOnlyList<TicketCategoryMetadataDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
