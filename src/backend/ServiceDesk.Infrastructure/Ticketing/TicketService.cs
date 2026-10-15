using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Ticketing;

namespace ServiceDesk.Application.Ticketing;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    public static readonly TimeSpan IdempotencyLifetime = TimeSpan.FromMinutes(10);

    public TicketService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, Guid requesterId, CancellationToken cancellationToken = default)
    {
        var ticket = NewTicket(command, requesterId);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);
        return ToDto(ticket);
    }

    public async Task<PagedTicketResult> GetTicketsForRequesterAsync(
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
            .Select(ticket => new TicketDto(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.Description,
                ticket.CategoryId,
                (int)ticket.Status,
                ticket.CreatedAt,
                ticket.RequesterDepartmentId))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }

    public async Task<TicketDto> CreateTicketIdempotentlyAsync(
        CreateTicketCommand command, Guid requesterId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // Do not trust an arbitrary requester ID supplied by a caller of this use case.
        if (!_currentUser.IsAuthenticated ||
            !Guid.TryParse(_currentUser.UserId, out var authenticatedId) || authenticatedId != requesterId)
        {
            throw new UnauthorizedAccessException("Authenticated requester identity is required.");
        }

        var createdTicket = NewTicket(command, requesterId);
        var fingerprint = CreateTicketFingerprint.Create(requesterId, idempotencyKey, command);
        if (!_context.Database.IsNpgsql())
        {
            // No process-local/cache fallback: every production writer must share the database lock.
            throw new NotSupportedException("Persistent ticket idempotency requires PostgreSQL.");
        }

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

            _context.Tickets.Add(createdTicket);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(createdTicket).ReloadAsync(cancellationToken);
            var createdResponse = ToDto(createdTicket);

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
            receipt.TicketId = createdTicket.Id;
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

    private static Ticket NewTicket(CreateTicketCommand command, Guid requesterId)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Ticket.Create(requesterId, command.Title, command.Description, command.CategoryId, DateTime.UtcNow);
    }

    private static TicketDto ToDto(Ticket ticket) => new(
        ticket.Id, ticket.TicketNumber, ticket.Title, ticket.Description, ticket.CategoryId,
        (int)ticket.Status, ticket.CreatedAt, ticket.RequesterDepartmentId);

    public async Task<TicketDto?> GetTicketByIdAsync(Guid id, Guid requesterId, CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking().Where(ticket => ticket.Id == id);
        if (_currentUser?.IsInRole(RoleNames.Admin) != true && _currentUser?.IsInRole(RoleNames.Agent) != true)
            query = query.Where(ticket => ticket.RequesterId == requesterId);
        var ticket = await query.FirstOrDefaultAsync(cancellationToken);
        return ticket is null ? null : ToDto(ticket);
    }
}
