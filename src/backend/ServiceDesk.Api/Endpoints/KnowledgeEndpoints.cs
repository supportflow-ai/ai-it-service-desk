using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.CreateDraftArticle;
using ServiceDesk.Application.Knowledge.UpdateDraftArticle;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Request body tạo bản nháp bài viết tri thức.</summary>
public record CreateDraftArticleRequest(string Title, string Content, IReadOnlyList<string>? Tags = null);

/// <summary>Request body cập nhật bản nháp bài viết tri thức.</summary>
public record UpdateDraftArticleRequest(string Title, string Content, IReadOnlyList<string>? Tags = null, uint? ExpectedRevision = null);

/// <summary>
/// Định nghĩa các API endpoints cho phân hệ tri thức (Knowledge Base).
/// Áp dụng bộ lọc lỗi LegacyErrorFilter để chuẩn hóa các lỗi nghiệp vụ về mã HTTP tương ứng.
/// </summary>
public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge/articles").WithTags("Knowledge").AddEndpointFilter<LegacyErrorFilter>();

        // POST /api/knowledge/articles: Tạo bản nháp bài viết mới (Agent/Admin)
        group.MapPost("/", async (
            [FromBody] CreateDraftArticleRequest request,
            ICreateDraftArticle createDraft,
            CancellationToken ct) =>
        {
            var res = await createDraft.ExecuteAsync(new CreateDraftArticleCommand(request.Title, request.Content, request.Tags), ct);
            return Results.Created($"/api/knowledge/articles/{res.Id}/draft", res);
        })
        .WithName("CreateDraftArticle")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<ArticleDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/knowledge/articles/{id}/draft: Cập nhật bản nháp bài viết (Agent/Admin)
        group.MapPut("/{id:guid}/draft", async (
            Guid id,
            [FromBody] UpdateDraftArticleRequest request,
            IUpdateDraftArticle updateDraft,
            CancellationToken ct) =>
        {
            var res = await updateDraft.ExecuteAsync(new UpdateDraftArticleCommand(id, request.Title, request.Content, request.Tags, request.ExpectedRevision), ct);
            return Results.Ok(res);
        })
        .WithName("UpdateDraftArticle")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<ArticleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
