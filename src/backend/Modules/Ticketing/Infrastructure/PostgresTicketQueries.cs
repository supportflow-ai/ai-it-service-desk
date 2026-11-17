using ServiceDesk.Modules.Ticketing.Domain;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.Authorization;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Cài đặt truy vấn dữ liệu ticket trực tiếp trên cơ sở dữ liệu PostgreSQL (Read-only queries).
/// </summary>
internal sealed class PostgresTicketQueries(DbContext context) : ITicketQueries
{
    private readonly DbContext _context = context;

    /// <summary>Truy vấn danh sách vé có phân trang của một Requester.</summary>
    public async Task<PagedTicketResult> GetForRequesterAsync(
        Guid requesterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Ticket>()
            .AsNoTracking()
            .Where(ticket => ticket.RequesterId == requesterId);

        var totalCount = await query.CountAsync(cancellationToken);
        var rawTickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => new { Ticket = ticket, Xmin = EF.Property<uint>(ticket, "xmin") })
            .ToListAsync(cancellationToken);

        var tickets = rawTickets.Select(item => TicketProjection.ToDto(item.Ticket, item.Xmin)).ToList();

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }

    /// <summary>Truy vấn chi tiết vé theo mã Id và phạm vi quyền đọc (TicketReadScope).</summary>
    public async Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken)
    {
        var query = _context.Set<Ticket>().AsNoTracking().Where(ticket => ticket.Id == id);
        var result = await TicketScopeQuery.Apply(query, _context, scope)
            .Select(ticket => new { Ticket = ticket, Xmin = EF.Property<uint>(ticket, "xmin") })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? null : TicketProjection.ToDto(result.Ticket, result.Xmin);
    }

    /// <summary>Truy vấn hàng đợi vé cho nhân viên hỗ trợ (Agent Queue) có lọc và phân trang (tương thích backward).</summary>
    public Task<PagedTicketResult> GetAgentQueueAsync(
        Guid currentUserId,
        TicketReadScope readScope,
        int? status,
        string? categoryId,
        string? scope,
        string? priority,
        bool sortByPriority,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => GetAgentQueueAsync(currentUserId, readScope, status, categoryId, scope, priority, sortByPriority, page, pageSize, null, null, cancellationToken);

    /// <summary>Truy vấn hàng đợi vé cho nhân viên hỗ trợ (Agent Queue) có lọc SLA, evaluatedAt và phân trang.</summary>
    public async Task<PagedTicketResult> GetAgentQueueAsync(
        Guid currentUserId,
        TicketReadScope readScope,
        int? status,
        string? categoryId,
        string? scope,
        string? priority,
        bool sortByPriority,
        int page,
        int pageSize,
        string? slaStatus,
        DateTime? evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var query = TicketScopeQuery.Apply(_context.Set<Ticket>().AsNoTracking(), _context, readScope);

        if (status.HasValue)
        {
            query = query.Where(ticket => (int)ticket.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(ticket => ticket.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse<Domain.Priority>(priority, true, out var parsedPriority))
                throw new ArgumentException("priority must be one of P1, P2, P3, P4");
            query = query.Where(ticket => ticket.Priority == parsedPriority);
        }

        if (string.Equals(scope, "unassigned", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(ticket => ticket.AssignedToId == null && ticket.Status != Domain.TicketStatus.Closed && ticket.Status != Domain.TicketStatus.Cancelled);
        }
        else if (string.Equals(scope, "my", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(ticket => ticket.AssignedToId == currentUserId && ticket.Status != Domain.TicketStatus.Closed && ticket.Status != Domain.TicketStatus.Cancelled);
        }

        var evalTime = evaluatedAt ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(slaStatus))
        {
            if (string.Equals(slaStatus, "Breached", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(ticket => ticket.IsSlaApplicable && (
                    ticket.FirstResponseBreached ||
                    ticket.ResolutionBreached ||
                    (ticket.FirstRespondedAt != null && ticket.FirstResponseTargetMinutes.HasValue && ticket.FirstRespondedAt.Value > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) ||
                    (ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed && evalTime > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) ||
                    ((ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) ||
                    ((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) ||
                    (ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && (evalTime + ticket.ResolutionAccumulatedDuration) > ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value))
                ));
            }
            else if (string.Equals(slaStatus, "AtRisk", StringComparison.OrdinalIgnoreCase))
            {
                // Chưa breach VÀ ít nhất 1 đồng hồ AtRisk (0.75 * Target <= E <= Target)
                query = query.Where(ticket => ticket.IsSlaApplicable &&
                    !ticket.FirstResponseBreached &&
                    !ticket.ResolutionBreached &&
                    !(ticket.FirstRespondedAt != null && ticket.FirstResponseTargetMinutes.HasValue && ticket.FirstRespondedAt.Value > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) &&
                    !(ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed && evalTime > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) &&
                    !((ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    !((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    !(ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && (evalTime + ticket.ResolutionAccumulatedDuration) > ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    (
                        // FR AtRisk
                        (ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed &&
                         evalTime >= ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value * 0.75) &&
                         evalTime <= ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) ||
                        // Res AtRisk khi Paused
                        ((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue &&
                         (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) >= ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75) &&
                         (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) <= ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) ||
                        // Res AtRisk khi Running
                        (ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled &&
                         (evalTime + ticket.ResolutionAccumulatedDuration) >= ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75) &&
                         (evalTime + ticket.ResolutionAccumulatedDuration) <= ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value))
                    )
                );
            }
            else if (string.Equals(slaStatus, "OnTrack", StringComparison.OrdinalIgnoreCase))
            {
                // Chưa breach VÀ không AtRisk VÀ có ít nhất 1 đồng hồ running/paused on-track (E < 0.75 * Target)
                query = query.Where(ticket => ticket.IsSlaApplicable &&
                    !ticket.FirstResponseBreached &&
                    !ticket.ResolutionBreached &&
                    !(ticket.FirstRespondedAt != null && ticket.FirstResponseTargetMinutes.HasValue && ticket.FirstRespondedAt.Value > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) &&
                    !(ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed && evalTime > ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) &&
                    !((ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    !((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue && (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) > ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    !(ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && (evalTime + ticket.ResolutionAccumulatedDuration) > ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value)) &&
                    // Không AtRisk
                    !(
                        (ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed &&
                         evalTime >= ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value * 0.75) &&
                         evalTime <= ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value)) ||
                        ((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue &&
                         (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) >= ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75) &&
                         (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) <= ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value)) ||
                        (ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled &&
                         (evalTime + ticket.ResolutionAccumulatedDuration) >= ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75) &&
                         (evalTime + ticket.ResolutionAccumulatedDuration) <= ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value))
                    ) &&
                    // Có ít nhất 1 đồng hồ Running hoặc Paused OnTrack
                    (
                        (ticket.FirstRespondedAt == null && ticket.FirstResponseTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled && ticket.Status != TicketStatus.Closed &&
                         evalTime < ticket.CreatedAt.AddMinutes(ticket.FirstResponseTargetMinutes.Value * 0.75)) ||
                        ((ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal) && ticket.ResolutionTargetMinutes.HasValue &&
                         (ticket.CreatedAt + ticket.ResolutionAccumulatedDuration) < ticket.CreatedAt.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75)) ||
                        (ticket.ResolutionSegmentStartedAt != null && ticket.ResolutionTargetMinutes.HasValue && ticket.Status != TicketStatus.Cancelled &&
                         (evalTime + ticket.ResolutionAccumulatedDuration) < ticket.ResolutionSegmentStartedAt.Value.AddMinutes(ticket.ResolutionTargetMinutes.Value * 0.75))
                    )
                );
            }
            else if (!string.Equals(slaStatus, "All", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("slaStatus must be one of All, OnTrack, AtRisk, Breached");
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = sortByPriority
            ? query.OrderBy(ticket => ticket.Priority).ThenByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id)
            : query.OrderByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id);

        var rawTickets = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => new { Ticket = ticket, Xmin = EF.Property<uint>(ticket, "xmin") })
            .ToListAsync(cancellationToken);

        var tickets = rawTickets
            .Select(item => TicketProjection.ToDto(item.Ticket, item.Xmin, evalTime))
            .ToList();

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }
}
