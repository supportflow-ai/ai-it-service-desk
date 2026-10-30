using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>
/// Ánh xạ dữ liệu giữa thực thể Domain Ticket và đối tượng truyền tải TicketDto.
/// </summary>
internal static class TicketProjection
{
    internal static TicketDto ToDto(Ticket ticket, uint revision = 0) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.Title,
        ticket.Description,
        ticket.CategoryId,
        (int)ticket.Status,
        ticket.CreatedAt,
        ticket.RequesterDepartmentId,
        ticket.RequesterId,
        ticket.AssignedToId,
        ticket.AssignedAt,
        ticket.UpdatedAt,
        revision,
        ticket.Priority?.ToString(),
        ticket.Impact?.ToString(),
        ticket.Urgency?.ToString());
}
