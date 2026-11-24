using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Departments;
using ServiceDesk.Modules.Identity.Application.GetDepartments;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.SharedKernel.Web;

namespace ServiceDesk.Modules.Identity.Presentation;

public record CreateDepartmentRequest(string Code, string Name, string? Description = null);
public record UpdateDepartmentRequest(string Name, string? Description = null);

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
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("GetDepartments")
        .RequireAuthorization()
        .Produces<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/departments: Quản trị viên tạo phòng ban mới
        group.MapPost("/", async (
            [FromBody] CreateDepartmentRequest request,
            [FromServices] ICreateDepartment createDepartment,
            CancellationToken ct) =>
        {
            var dept = await createDepartment.ExecuteAsync(new(request.Code, request.Name, request.Description), ct);
            return Results.Created($"/api/departments/{dept.Id}", dept);
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("CreateDepartment")
        .RequireAuthorization(PolicyNames.RequireAdmin)
        .Produces<DepartmentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        // PUT /api/departments/{id}: Quản trị viên cập nhật thông tin phòng ban
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateDepartmentRequest request,
            [FromServices] IUpdateDepartment updateDepartment,
            CancellationToken ct) =>
        {
            var dept = await updateDepartment.ExecuteAsync(new(id, request.Name, request.Description), ct);
            return Results.Ok(dept);
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("UpdateDepartment")
        .RequireAuthorization(PolicyNames.RequireAdmin)
        .Produces<DepartmentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
