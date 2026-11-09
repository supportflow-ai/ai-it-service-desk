using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        return app;
    }
}
