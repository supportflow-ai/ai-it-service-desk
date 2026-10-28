using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>
/// Cài đặt truy vấn dữ liệu ticket trực tiếp trên cơ sở dữ liệu PostgreSQL (Read-only queries).
/// </summary>
internal sealed class PostgresTicketQueries(ApplicationDbContext context) : ITicketQueries
{
    private readonly ApplicationDbContext _context = context;

    /// <summary>Truy vấn danh sách vé có phân trang của một Requester.</summary>
    public async Task<PagedTicketResult> GetForRequesterAsync(
        Guid requesterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.RequesterId == requesterId);

        var totalCount = await query.CountAsync(cancellationToken);
        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => TicketProjection.ToDto(ticket))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }

    /// <summary>Truy vấn chi tiết vé theo mã Id và phạm vi quyền đọc (TicketReadScope).</summary>
    public async Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsNoTracking().Where(ticket => ticket.Id == id);
        query = scope switch
        {
            TicketReadScope.OwnTickets own => query.Where(ticket => ticket.RequesterId == own.ActorId),
            TicketReadScope.AllTickets => query,
            _ => throw new ArgumentException("Yêu cầu phạm vi quyền đọc hợp lệ.", nameof(scope))
        };
        var ticket = await query.FirstOrDefaultAsync(cancellationToken);
        return ticket is null ? null : TicketProjection.ToDto(ticket);
    }

    /// <summary>Truy vấn hàng đợi vé cho nhân viên hỗ trợ (Agent Queue) có lọc và phân trang.</summary>
    public async Task<PagedTicketResult> GetAgentQueueAsync(
        int? status,
        string? categoryId,
        string? scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(ticket => (int)ticket.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(ticket => ticket.CategoryId == categoryId);
        }

        if (string.Equals(scope, "unassigned", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(ticket => ticket.Status == Domain.Ticketing.TicketStatus.Submitted);
        }
        else if (string.Equals(scope, "my", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(ticket => ticket.Status == Domain.Ticketing.TicketStatus.InProgress);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => TicketProjection.ToDto(ticket))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }
}
