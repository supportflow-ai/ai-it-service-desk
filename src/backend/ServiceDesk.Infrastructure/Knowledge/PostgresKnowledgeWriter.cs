using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Knowledge.Indexing;

namespace ServiceDesk.Infrastructure.Knowledge;

/// <summary>
/// Cài đặt cổng ghi dữ liệu Knowledge Base Article vào PostgreSQL:
/// - Tạo mới dự thảo bài viết (CreateDraftAsync).
/// - Lấy bài viết kèm Tags và Versions để cập nhật (GetForUpdateAsync).
/// - Đọc phiên bản concurrency token xmin (GetRevision).
/// - Cập nhật bài viết với kiểm tra xung đột xmin lạc quan (UpdateArticleAsync).
/// </summary>
internal sealed class PostgresKnowledgeWriter(ApplicationDbContext context) : IKnowledgeWriter
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Article> CreateDraftAsync(Article article, CancellationToken cancellationToken = default)
    {
        _context.Articles.Add(article);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(article).ReloadAsync(cancellationToken);
        return article;
    }

    public Task<Article?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public uint GetRevision(Article article)
    {
        var prop = _context.Entry(article).Property<uint>("xmin");
        return prop.CurrentValue;
    }

    public async Task<Article> UpdateArticleAsync(Article article, uint? expectedRevision = null, CancellationToken cancellationToken = default)
    {
        var newVersions = new List<ArticleVersion>();
        foreach (var version in article.Versions)
        {
            var entry = _context.Entry(version);
            if (entry.State is EntityState.Modified or EntityState.Detached)
            {
                entry.State = EntityState.Added;
            }
            if (entry.State == EntityState.Added) newVersions.Add(version);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            // Serialize publication before EF can insert a new snapshot ahead of the article UPDATE.
            var revision = (await _context.Articles.FromSqlInterpolated($"""
                SELECT *, xmin FROM public.articles WHERE "Id" = {article.Id} FOR UPDATE
                """).AsNoTracking().Select(a => new { Revision = EF.Property<uint>(a, "xmin") })
                .ToListAsync(cancellationToken)).SingleOrDefault();
            var expected = expectedRevision ?? _context.Entry(article).Property<uint>("xmin").OriginalValue;
            if (revision is null || revision.Revision != expected)
                throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi.");
            if (expectedRevision.HasValue)
                _context.Entry(article).Property<uint>("xmin").OriginalValue = expectedRevision.Value;
            await _context.SaveChangesAsync(cancellationToken);
            foreach (var version in newVersions)
                await KnowledgeIndexScheduling.ScheduleAsync(_context, article, version, version.Id,
                    article.UpdatedAt, invalidateActive: true, cancellationToken);
            if (article.Status == ArticleStatus.Archived)
                await KnowledgeIndexScheduling.ArchiveAsync(_context, article.Id, article.UpdatedAt, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _context.Entry(article).ReloadAsync(cancellationToken);
            return article;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Xung đột phiên bản bài viết. Dữ liệu đã bị sửa đổi bởi người khác.", ex);
        }
    }
}
