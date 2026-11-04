using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Persistence;

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
        if (expectedRevision.HasValue && expectedRevision.Value > 0)
        {
            _context.Entry(article).Property<uint>("xmin").OriginalValue = expectedRevision.Value;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return article;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Xung đột phiên bản bài viết. Dữ liệu đã bị sửa đổi bởi người khác.", ex);
        }
    }
}
