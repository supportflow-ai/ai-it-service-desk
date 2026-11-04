using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.CreateDraftArticle;
using ServiceDesk.Application.Knowledge.UpdateDraftArticle;
using ServiceDesk.Application.Knowledge.PublishArticle;
using ServiceDesk.Application.Knowledge.ArchiveArticle;
using ServiceDesk.Application.Knowledge.GetArticleDetail;
using ServiceDesk.Application.Knowledge.SearchKnowledge;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Knowledge;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Request body tạo bản nháp bài viết tri thức.</summary>
public record CreateDraftArticleRequest(string Title, string Content, IReadOnlyList<string>? Tags = null);

/// <summary>Request body cập nhật bản nháp bài viết tri thức.</summary>
public record UpdateDraftArticleRequest(string Title, string Content, IReadOnlyList<string>? Tags = null, uint? ExpectedRevision = null);

/// <summary>Request body xuất bản bài viết tri thức.</summary>
public record PublishArticleRequest(uint? ExpectedRevision = null);

/// <summary>Request body lưu trữ bài viết tri thức.</summary>
public record ArchiveArticleRequest(uint? ExpectedRevision = null);

/// <summary>
/// Định nghĩa các API endpoints cho phân hệ tri thức (Knowledge Base).
/// Áp dụng bộ lọc lỗi LegacyErrorFilter để chuẩn hóa các lỗi nghiệp vụ về mã HTTP tương ứng.
/// </summary>
public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge/articles")
            .WithTags("Knowledge")
            .AddEndpointFilter<LegacyErrorFilter>()
            .AddEndpointFilter(async (ctx, next) =>
            {
                try
                {
                    return await next(ctx);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.UnprocessableEntity(new { error = ex.Message });
                }
            });

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

        // POST /api/knowledge/articles/{id}/publish: Xuất bản bài viết từ bản nháp (Agent/Admin)
        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            [FromBody] PublishArticleRequest? request,
            IPublishArticle publishArticle,
            CancellationToken ct) =>
        {
            var effectiveRevision = request?.ExpectedRevision ?? expectedRevision;
            var res = await publishArticle.ExecuteAsync(new PublishArticleCommand(id, effectiveRevision), ct);
            return Results.Ok(res);
        })
        .WithName("PublishArticle")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<ArticleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/knowledge/articles/{id}/archive: Lưu trữ bài viết (Agent/Admin)
        group.MapPost("/{id:guid}/archive", async (
            Guid id,
            [FromQuery] uint? expectedRevision,
            [FromBody] ArchiveArticleRequest? request,
            IArchiveArticle archiveArticle,
            CancellationToken ct) =>
        {
            var effectiveRevision = request?.ExpectedRevision ?? expectedRevision;
            var res = await archiveArticle.ExecuteAsync(new ArchiveArticleCommand(id, effectiveRevision), ct);
            return Results.Ok(res);
        })
        .WithName("ArchiveArticle")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<ArticleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // GET /api/knowledge/articles/{id}: Chi tiết bài viết tri thức (Requester/Agent/Admin)
        group.MapGet("/{id:guid}", async (
            Guid id,
            IGetArticleDetail getArticleDetail,
            CancellationToken ct) =>
        {
            var res = await getArticleDetail.ExecuteAsync(new GetArticleDetailQuery(id), ct);
            if (res == null)
            {
                return Results.NotFound(new { error = "Không tìm thấy bài viết." });
            }
            return Results.Ok(res);
        })
        .WithName("GetArticleDetail")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<ArticleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/knowledge/articles/search: Tìm kiếm bài viết tri thức (Requester/Agent/Admin)
        group.MapGet("/search", async (
            [FromQuery] string? query,
            [FromQuery] string? tag,
            [FromQuery] ArticleStatus? status,
            ISearchKnowledge searchKnowledge,
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) =>
        {
            var res = await searchKnowledge.ExecuteAsync(new SearchKnowledgeQuery(query, tag, status, page, pageSize), ct);
            return Results.Ok(res);
        })
        .WithName("SearchKnowledgeArticles")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<PagedArticleResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/knowledge/articles: Danh sách / tìm kiếm bài viết tri thức (alias)
        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] string? tag,
            [FromQuery] ArticleStatus? status,
            ISearchKnowledge searchKnowledge,
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) =>
        {
            var res = await searchKnowledge.ExecuteAsync(new SearchKnowledgeQuery(query, tag, status, page, pageSize), ct);
            return Results.Ok(res);
        })
        .WithName("ListKnowledgeArticles")
        .RequireAuthorization(PolicyNames.RequireRequester)
        .Produces<PagedArticleResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
