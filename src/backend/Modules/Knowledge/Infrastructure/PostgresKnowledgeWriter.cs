using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Domain;

namespace ServiceDesk.Modules.Knowledge.Infrastructure;

/// <summary>
/// Cài đặt cổng ghi dữ liệu Knowledge Base Article vào PostgreSQL:
/// - Tạo mới dự thảo bài viết (CreateDraftAsync).
/// - Lấy bài viết kèm Tags và Versions để cập nhật (GetForUpdateAsync).
/// - Đọc phiên bản concurrency token xmin (GetRevision).
/// - Cập nhật bài viết với kiểm tra xung đột xmin lạc quan (UpdateArticleAsync).
/// </summary>
internal sealed class PostgresKnowledgeWriter(DbContext context) : IKnowledgeWriter
{
    private readonly DbContext _context = context;

    public async Task<Article> CreateDraftAsync(Article article, CancellationToken cancellationToken = default)
    {
        _context.Set<Article>().Add(article);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(article).ReloadAsync(cancellationToken);
        return article;
    }

    public Task<Article?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Set<Article>()
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
        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
            var current = await _context.Database.SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM public.articles WHERE \"Id\" = {article.Id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var loaded = GetRevision(article);
            if (current == 0 || current != loaded || expectedRevision.HasValue && current != expectedRevision.Value)
                throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.");
            foreach (var version in article.Versions)
            {
                var entry = _context.Entry(version);
                if (entry.State is EntityState.Modified or EntityState.Detached) entry.State = EntityState.Added;
            }
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(article).ReloadAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return article;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Xung đột phiên bản bài viết. Dữ liệu đã bị sửa đổi bởi người khác.", ex);
        }
        catch (Exception ex) when (ex.GetBaseException() is DbException { SqlState: "55P03" })
        {
            throw new KnowledgeWriteBusyException(ex);
        }
    }
}
