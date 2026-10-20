using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.CreateTicket;

/// <summary>
/// Hợp đồng use case tạo mới ticket.
/// </summary>
public interface ICreateTicket
{
    Task<TicketDto> ExecuteAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý nghiệp vụ tạo mới ticket:
/// - Xác thực danh tính người gửi (Requester).
/// - Khởi tạo thực thể Ticket với các ràng buộc nghiệp vụ Domain.
/// - Áp dụng cơ chế Idempotency chống gửi trùng lặp nếu có Idempotency-Key.
/// </summary>
internal sealed class CreateTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : ICreateTicket
{
    public Task<TicketDto> ExecuteAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var requester = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = Ticket.Create(requester, command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime);
        var fingerprint = idempotencyKey is null ? null : CreateTicketFingerprint.Create(requester, idempotencyKey, command);

        return writer.CreateAsync(ticket, fingerprint, cancellationToken);
    }
}
