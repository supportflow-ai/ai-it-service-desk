using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.CreateTicket;

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
