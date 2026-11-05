using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.GetDepartments;

namespace ServiceDesk.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static IEndpointRouteBuilder MapDepartmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/departments").WithTags("Departments");

        // GET /api/departments: Lấy danh sách các phòng ban trong tổ chức
        group.MapGet("/", async (
            [FromServices] IGetDepartments getDepartments,
            CancellationToken ct) =>
        {
            var departments = await getDepartments.ExecuteAsync(new GetDepartmentsQuery(), ct);
            return Results.Ok(departments);
        })
        .AddEndpointFilter<LegacyErrorFilter>()
        .WithName("GetDepartments")
        .RequireAuthorization()
        .Produces<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
