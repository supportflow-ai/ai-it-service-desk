using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Domain.Ticketing;
namespace ServiceDesk.Infrastructure.Ticketing;
internal static class TicketProjection
{
    internal static TicketDto ToDto(Ticket ticket) => new(ticket.Id, ticket.TicketNumber, ticket.Title,
        ticket.Description, ticket.CategoryId, (int)ticket.Status, ticket.CreatedAt, ticket.RequesterDepartmentId);
}
