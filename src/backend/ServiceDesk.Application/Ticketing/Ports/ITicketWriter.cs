using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>The adapter owns the complete atomic persistence operation, including replay receipts.</summary>
public interface ITicketWriter
{
    Task<TicketDto> CreateAsync(Ticket ticket, CreateTicketFingerprint? fingerprint, CancellationToken cancellationToken);
    Task<Ticket?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<TicketDto> UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken);
}
