using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.AIAssistance.Infrastructure;

/// <summary>
/// Lưu analysis và quyết định HITL bằng DbContext dùng chung theo scope, nên lệnh ghi phiếu của Ticketing gọi
/// trong <see cref="DecideAsync{TResult}"/> tham gia cùng giao dịch PostgreSQL.
/// Thứ tự khóa: analysis trước, phiếu sau. Lệnh phiếu khác không khóa analysis nên không tạo vòng chờ.
/// </summary>
internal sealed class PostgresAiAnalysisStore(DbContext context) : IAiAnalysisStore
{
    public async Task AddAsync(AIAnalysis analysis, CancellationToken cancellationToken = default)
    {
        context.Set<AIAnalysis>().Add(analysis);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<AIAnalysis?> GetLatestAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        context.Set<AIAnalysis>().AsNoTracking()
            .Include(analysis => analysis.Suggestions)
            .Where(analysis => analysis.TicketId == ticketId)
            .OrderByDescending(analysis => analysis.CreatedAt).ThenByDescending(analysis => analysis.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TResult> DecideAsync<TResult>(
        Guid ticketId,
        Guid analysisId,
        Func<AIAnalysis?, Task<TResult>> decide,
        CancellationToken cancellationToken = default)
    {
        // Mọi lỗi hoặc hủy yêu cầu hủy giao dịch khi dispose: phiếu, audit và quyết định cùng rollback.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
            // ToListAsync giữ nguyên câu lệnh lấy khóa, không bọc vào truy vấn con.
            var locked = await context.Database.SqlQuery<Guid>(
                    $"SELECT id AS \"Value\" FROM public.ai_analyses WHERE id = {analysisId} AND ticket_id = {ticketId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            // Đọc sau khi có khóa (READ COMMITTED) để thấy quyết định đã commit của giao dịch trước.
            var analysis = locked.Count == 0
                ? null
                : await context.Set<AIAnalysis>().Include(value => value.Suggestions)
                    .SingleAsync(value => value.Id == analysisId, cancellationToken);

            var result = await decide(analysis);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception.GetBaseException() is PostgresException
               { SqlState: PostgresErrorCodes.LockNotAvailable })
        {
            throw new TriageDecisionInProgressException(exception);
        }
    }
}

/// <summary>Quyết định khác trên cùng analysis đang giữ khóa → 503 kèm Retry-After.</summary>
internal sealed class TriageDecisionInProgressException(Exception inner)
    : RetryLaterException("Kết quả phân tích này đang được xử lý, hãy thử lại.", inner);
