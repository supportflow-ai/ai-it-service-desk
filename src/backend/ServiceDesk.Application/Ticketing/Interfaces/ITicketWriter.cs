using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Interfaces;

/// <summary>The adapter owns the complete atomic persistence operation, including replay receipts.</summary>
public interface ITicketWriter
{
    Task<TicketDto> CreateAsync(Ticket ticket, CreateTicketFingerprint? fingerprint, CancellationToken cancellationToken);
}
