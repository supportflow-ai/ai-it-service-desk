using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diag").WithTags("Diagnostic");

        group.MapGet("/protected", () => Results.Ok(new { message = "Authenticated" }))
            .WithName("DiagProtected")
            .RequireAuthorization();

        group.MapGet("/agent-only", () => Results.Ok(new { message = "Agent+ only" }))
            .WithName("DiagAgentOnly")
            .RequireAuthorization(PolicyNames.RequireAgent);

        group.MapGet("/admin-only", () => Results.Ok(new { message = "Admin only" }))
            .WithName("DiagAdminOnly")
            .RequireAuthorization(PolicyNames.RequireAdmin);

        return app;
    }
}
