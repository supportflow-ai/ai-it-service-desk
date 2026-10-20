using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

/// <summary>
/// Các endpoints kiểm tra và chẩn đoán phân quyền hệ thống (Diagnostic).
/// </summary>
public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diag").WithTags("Diagnostic");

        // GET /api/diag/protected: Yêu cầu bất kỳ người dùng nào đã đăng nhập
        group.MapGet("/protected", () => Results.Ok(new { message = "Authenticated" }))
            .WithName("DiagProtected")
            .RequireAuthorization();

        // GET /api/diag/agent-only: Yêu cầu quyền Agent hoặc Admin
        group.MapGet("/agent-only", () => Results.Ok(new { message = "Agent+ only" }))
            .WithName("DiagAgentOnly")
            .RequireAuthorization(PolicyNames.RequireAgent);

        // GET /api/diag/admin-only: Yêu cầu quyền Admin
        group.MapGet("/admin-only", () => Results.Ok(new { message = "Admin only" }))
            .WithName("DiagAdminOnly")
            .RequireAuthorization(PolicyNames.RequireAdmin);

        return app;
    }
}
