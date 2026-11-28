using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresSlaEvaluationAdapter(
    DbContext context,
    ILogger<PostgresSlaEvaluationAdapter> logger) : ISlaEvaluationGateway
{
    public async Task<IReadOnlyList<Ticket>> GetActiveSlaTicketsBatchAsync(Guid? afterId, int batchSize, CancellationToken cancellationToken)
    {
        var query = context.Set<Ticket>()
            .Where(t => t.IsSlaApplicable
                     && t.Status != TicketStatus.Closed
                     && t.Status != TicketStatus.Cancelled
                     && (!t.FirstResponseBreached || !t.ResolutionBreached));

        if (afterId.HasValue)
        {
            query = query.Where(t => t.Id > afterId.Value);
        }

        return await query
            .OrderBy(t => t.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateBreachedMarkersAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Loại bỏ entity bị conflict khỏi ChangeTracker để không làm hỏng các ticket tiếp theo trong batch
            var entry = context.Entry(ticket);
            entry.State = EntityState.Detached;

            logger.LogWarning(ex, "Xung đột đồng thời khi cập nhật vi phạm SLA cho ticket {TicketId}. Sẽ đánh giá lại ở lần sweep tiếp theo.", ticket.Id);
            return false;
        }
        catch (DbUpdateException ex) when (ex.InnerException is not DbException { IsTransient: true })
        {
            context.Entry(ticket).State = EntityState.Detached;
            logger.LogWarning("Không thể ghi marker SLA của phiếu {TicketId} ({FailureType}).", ticket.Id, ex.GetType().Name);
            throw new SlaTicketWriteException(ex);
        }
    }
}
