using ServiceDesk.Modules.Ticketing.Domain;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.SharedKernel.Application;

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
        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => TicketProjection.ToDto(ticket, EF.Property<uint>(ticket, "xmin")))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }

    /// <summary>Truy vấn chi tiết vé theo mã Id và phạm vi quyền đọc (TicketReadScope).</summary>
    public async Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken)
    {
        var query = _context.Set<Ticket>().AsNoTracking().Where(ticket => ticket.Id == id);
        return await TicketScopeQuery.Apply(query, _context, scope)
            .Select(ticket => TicketProjection.ToDto(ticket, EF.Property<uint>(ticket, "xmin")))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Truy vấn hàng đợi vé cho nhân viên hỗ trợ (Agent Queue) có lọc và phân trang.</summary>
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
                throw new InvalidInputException("priority_invalid", "priority phải là một trong P1, P2, P3, P4.");
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

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = sortByPriority
            ? query.OrderBy(ticket => ticket.Priority).ThenByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id)
            : query.OrderByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id);
        var tickets = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => TicketProjection.ToDto(ticket, EF.Property<uint>(ticket, "xmin")))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }
}
