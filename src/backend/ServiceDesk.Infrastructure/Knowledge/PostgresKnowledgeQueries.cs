using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Knowledge.Authorization;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Knowledge;

/// <summary>
/// Cài đặt cổng truy vấn dữ liệu Knowledge Base trực tiếp từ PostgreSQL (Read-only queries):
/// - Chi tiết bài viết theo Id có kiểm tra phạm vi đọc (Public, Author, Staff).
/// - Tìm kiếm bài viết đã xuất bản (SearchPublishedAsync).
/// - Tìm kiếm danh sách bài viết dành cho Agent/Admin kèm bộ lọc trạng thái (SearchStaffAsync).
/// </summary>
internal sealed class PostgresKnowledgeQueries(ApplicationDbContext context) : IKnowledgeQueries
{
    private readonly ApplicationDbContext _context = context;

    public async Task<ArticleDto?> GetByIdAsync(Guid id, KnowledgeReadScope scope, CancellationToken cancellationToken = default)
    {
        var query = _context.Articles.AsNoTracking().Where(a => a.Id == id);

        query = scope switch
        {
            KnowledgeReadScope.Public => query.Where(a => a.Status == ArticleStatus.Published),
            KnowledgeReadScope.Author author => query.Where(a => a.Status == ArticleStatus.Published || a.AuthorId == author.AuthorId),
            KnowledgeReadScope.Staff => query,
            _ => throw new ArgumentException("Yêu cầu phạm vi quyền đọc hợp lệ.", nameof(scope))
        };

        return await ProjectToDto(query).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedArticleResult> SearchPublishedAsync(
        string? query,
        string? tag,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryable = _context.Articles.AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            queryable = queryable.Where(a => a.Tags.Any(t => t.Name == normalizedTag));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            queryable = queryable.Where(a => EF.Functions.ILike(a.Title, pattern) || EF.Functions.ILike(a.Content, pattern));
        }

        var totalCount = await queryable.CountAsync(cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = (int)Math.Ceiling((double)totalCount / safePageSize);

        var items = await ProjectToDto(
            queryable
                .OrderByDescending(a => a.PublishedAt)
                .ThenByDescending(a => a.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize))
            .ToListAsync(cancellationToken);

        return new PagedArticleResult(items, safePage, safePageSize, totalCount, totalPages);
    }

    public async Task<PagedArticleResult> SearchStaffAsync(
        string? query,
        string? tag,
        ArticleStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryable = _context.Articles.AsNoTracking();

        if (status.HasValue)
        {
            queryable = queryable.Where(a => a.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            queryable = queryable.Where(a => a.Tags.Any(t => t.Name == normalizedTag));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            queryable = queryable.Where(a => EF.Functions.ILike(a.Title, pattern) || EF.Functions.ILike(a.Content, pattern));
        }

        var totalCount = await queryable.CountAsync(cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = (int)Math.Ceiling((double)totalCount / safePageSize);

        var items = await ProjectToDto(
            queryable
                .OrderByDescending(a => a.UpdatedAt)
                .ThenByDescending(a => a.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize))
            .ToListAsync(cancellationToken);

        return new PagedArticleResult(items, safePage, safePageSize, totalCount, totalPages);
    }

    private static IQueryable<ArticleDto> ProjectToDto(IQueryable<Article> query) =>
        query.Select(a => new ArticleDto(
            a.Id,
            a.Title,
            a.Content,
            a.Status,
            a.AuthorId,
            a.Version,
            EF.Property<uint>(a, "xmin"),
            a.Tags.Select(t => t.Name).ToList(),
            a.CreatedAt,
            a.UpdatedAt,
            a.PublishedAt,
            a.ArchivedAt
        ));

    internal static ArticleDto ToDto(Article a, uint revision) => new(
        a.Id,
        a.Title,
        a.Content,
        a.Status,
        a.AuthorId,
        a.Version,
        revision,
        a.Tags.Select(t => t.Name).ToList(),
        a.CreatedAt,
        a.UpdatedAt,
        a.PublishedAt,
        a.ArchivedAt
    );
}
