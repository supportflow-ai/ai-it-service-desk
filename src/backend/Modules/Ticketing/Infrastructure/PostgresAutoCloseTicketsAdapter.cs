using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresAutoCloseTicketsAdapter(
    DbContext context,
    ILogger<PostgresAutoCloseTicketsAdapter> logger) : IAutoCloseTicketsGateway
{
    public async Task<IReadOnlyList<Ticket>> GetResolvedTicketsEligibleForAutoCloseAsync(
        DateTime olderThanUtc,
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var query = context.Set<Ticket>()
            .Where(t => t.Status == TicketStatus.Resolved
                     && t.ResolvedAt != null
                     && t.ResolvedAt <= olderThanUtc);

        if (afterId.HasValue)
        {
            query = query.Where(t => t.Id > afterId.Value);
        }

        return await query
            .OrderBy(t => t.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CloseWithAuditAsync(
        Ticket ticket,
        DateTime now,
        string auditMessage,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            ticket.Close(now);

            var audit = TicketComment.CreatePublic(
                ticket.Id,
                Guid.Empty,
                "Hệ thống",
                "System",
                auditMessage,
                now);

            context.Set<TicketComment>().Add(audit);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            var entry = context.Entry(ticket);
            entry.State = EntityState.Detached;

            logger.LogWarning(ex, "Xung đột đồng thời khi tự động đóng ticket {TicketId}. Sẽ thử lại ở lần quét tiếp theo.", ticket.Id);
            return false;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Lỗi khi tự động đóng ticket {TicketId}.", ticket.Id);
            return false;
        }
    }
}
