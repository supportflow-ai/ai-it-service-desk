using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class PostgresTicketWriter(ApplicationDbContext context) : ITicketWriter
{
    private readonly ApplicationDbContext _context = context;
    public static readonly TimeSpan IdempotencyLifetime = TimeSpan.FromMinutes(10);

    public async Task<TicketDto> CreateAsync(Ticket ticket, CreateTicketFingerprint? fingerprint, CancellationToken cancellationToken)
    {
        if (fingerprint is null)
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(ticket).ReloadAsync(cancellationToken);
            return TicketProjection.ToDto(ticket);
        }
        var requesterId = ticket.RequesterId;
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Transaction-scoped, shared by all API instances, automatically released on rollback.
            // A 64-bit hash collision only serializes unrelated keys; lookup still uses the full scope.
            await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({fingerprint.LockId})", cancellationToken);

            // Read after acquiring the lock, under READ COMMITTED; use database time on every instance.
            var now = await DatabaseUtcNowAsync(cancellationToken);
            var receipt = await _context.TicketIdempotencyRecords.SingleOrDefaultAsync(record =>
                record.RequesterId == requesterId &&
                record.Method == CreateTicketFingerprint.Method &&
                record.Resource == CreateTicketFingerprint.Resource &&
                record.KeyHash == fingerprint.KeyHash, cancellationToken);

            if (receipt is not null && receipt.ExpiresAt > now)
            {
                if (receipt.PayloadHash != fingerprint.PayloadHash)
                    throw new IdempotencyConflictException();

                // Defense in depth: replay must never use the broader Agent/Admin detail visibility.
                var ownsTicket = await _context.Tickets.AsNoTracking().AnyAsync(
                    ticket => ticket.Id == receipt.TicketId && ticket.RequesterId == requesterId, cancellationToken);
                var response = JsonSerializer.Deserialize<TicketDto>(receipt.ResponseJson);
                if (!ownsTicket || response is null || response.Id != receipt.TicketId)
                    throw new InvalidOperationException("Invalid ticket idempotency receipt.");

                await transaction.CommitAsync(cancellationToken);
                return response;
            }

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(ticket).ReloadAsync(cancellationToken);
            var createdResponse = TicketProjection.ToDto(ticket);

            var completedAt = await DatabaseUtcNowAsync(cancellationToken);
            if (receipt is null)
            {
                receipt = new TicketIdempotencyRecord
                {
                    Id = Guid.NewGuid(),
                    RequesterId = requesterId,
                    Method = CreateTicketFingerprint.Method,
                    Resource = CreateTicketFingerprint.Resource,
                    KeyHash = fingerprint.KeyHash
                };
                _context.TicketIdempotencyRecords.Add(receipt);
            }

            // An expired key starts a new generation while holding the same scope lock.
            receipt.PayloadHash = fingerprint.PayloadHash;
            receipt.TicketId = ticket.Id;
            receipt.ResponseJson = JsonSerializer.Serialize(createdResponse);
            receipt.CreatedAt = completedAt;
            receipt.ExpiresAt = completedAt.Add(IdempotencyLifetime);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return createdResponse;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            // The transaction is disposed/rolled back before the API returns this retryable error.
            throw new IdempotencyInProgressException(exception);
        }
        // Other failures/cancellation also dispose the transaction: no ticket or receipt is committed.
        // DbContext is scoped per HTTP request and must be discarded after a failed operation.
    }

    private Task<DateTime> DatabaseUtcNowAsync(CancellationToken cancellationToken) =>
        _context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);

    public Task<Ticket?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => _context.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TicketDto> UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        return TicketProjection.ToDto(ticket);
    }
}
