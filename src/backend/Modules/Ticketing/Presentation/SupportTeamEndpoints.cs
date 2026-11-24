using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.SupportTeams;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.SharedKernel.Web;

namespace ServiceDesk.Modules.Ticketing.Presentation;

public static class SupportTeamEndpoints
{
    public static IEndpointRouteBuilder MapSupportTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support-teams").WithTags("SupportTeams").AddEndpointFilter<ApiErrorFilter>();

        // GET /api/support-teams: Lấy danh sách nhóm hỗ trợ đang hoạt động
        group.MapGet("/", async (
            [FromServices] IGetSupportTeams getSupportTeams,
            CancellationToken ct) =>
        {
            var teams = await getSupportTeams.ExecuteAsync(ct);
            return Results.Ok(teams);
        })
        .WithName("GetSupportTeams")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<IReadOnlyList<SupportTeamDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/support-teams/{id}: Lấy chi tiết nhóm hỗ trợ kèm danh sách thành viên
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IGetSupportTeamDetail getSupportTeamDetail,
            CancellationToken ct) =>
        {
            var team = await getSupportTeamDetail.ExecuteAsync(new(id), ct);
            if (team == null)
            {
                return Results.NotFound(new { error = "Không tìm thấy nhóm hỗ trợ." });
            }
            return Results.Ok(team);
        })
        .WithName("GetSupportTeamDetail")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<SupportTeamDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/support-teams/{id}/members: Thêm thành viên vào nhóm hỗ trợ
        group.MapPost("/{id:guid}/members", async (
            Guid id,
            [FromBody] AddTeamMemberRequest request,
            [FromServices] IAddTeamMember addTeamMember,
            CancellationToken ct) =>
        {
            var member = await addTeamMember.ExecuteAsync(new(id, request), ct);
            return Results.Created($"/api/support-teams/{id}/members/{member.UserId}", member);
        })
        .WithName("AddSupportTeamMember")
        .RequireAuthorization()
        .Produces<SupportTeamMemberDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE /api/support-teams/{id}/members/{userId}: Xóa thành viên khỏi nhóm hỗ trợ
        group.MapDelete("/{id:guid}/members/{userId:guid}", async (
            Guid id,
            Guid userId,
            [FromServices] IRemoveTeamMember removeTeamMember,
            CancellationToken ct) =>
        {
            await removeTeamMember.ExecuteAsync(new(id, userId), ct);
            return Results.NoContent();
        })
        .WithName("RemoveSupportTeamMember")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/support-teams: Tạo nhóm hỗ trợ mới
        group.MapPost("/", async (
            [FromBody] CreateSupportTeamRequest request,
            [FromServices] ICreateSupportTeam createSupportTeam,
            CancellationToken ct) =>
        {
            var team = await createSupportTeam.ExecuteAsync(request, ct);
            return Results.Created($"/api/support-teams/{team.Id}", team);
        })
        .WithName("CreateSupportTeam")
        .RequireAuthorization(PolicyNames.RequireAdmin)
        .Produces<SupportTeamDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        // PUT /api/support-teams/{id}: Cập nhật thông tin nhóm hỗ trợ
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSupportTeamRequest request,
            [FromServices] IUpdateSupportTeam updateSupportTeam,
            CancellationToken ct) =>
        {
            var team = await updateSupportTeam.ExecuteAsync(id, request, ct);
            return Results.Ok(team);
        })
        .WithName("UpdateSupportTeam")
        .RequireAuthorization(PolicyNames.RequireAdmin)
        .Produces<SupportTeamDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
