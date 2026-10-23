using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Errors;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>
/// Cài đặt cổng ghi dữ liệu ticket vào PostgreSQL:
/// - Xử lý tạo vé nguyên tử (atomic transaction) kết hợp hóa đơn Idempotency.
/// - Dùng PostgreSQL advisory lock (pg_advisory_xact_lock) để đồng bộ giữa nhiều API instance.
/// - Cập nhật trạng thái vé và lưu thay đổi qua SaveChangesAsync.
/// </summary>
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
            // Lock theo transaction, chia sẻ giữa tất cả API replica, tự giải phóng khi rollback.
            await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({fingerprint.LockId})", cancellationToken);

            // Đọc dữ liệu sau khi chiếm lock dưới mức cô lập READ COMMITTED; dùng thời gian database.
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

                // Phòng thủ chiều sâu: replay không bao giờ được lộ phạm vi chi tiết của Agent/Admin.
                var ownsTicket = await _context.Tickets.AsNoTracking().AnyAsync(
                    ticket => ticket.Id == receipt.TicketId && ticket.RequesterId == requesterId, cancellationToken);
                var response = JsonSerializer.Deserialize<TicketDto>(receipt.ResponseJson);
                if (!ownsTicket || response is null || response.Id != receipt.TicketId)
                    throw new InvalidOperationException("Hóa đơn replay idempotent không hợp lệ.");

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

            // Khóa đã hết hạn sẽ bắt đầu một vòng đời mới trong khi vẫn giữ lock.
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
            // Transaction được hủy bỏ trước khi API trả về lỗi có thể thử lại này.
            throw new IdempotencyInProgressException(exception);
        }
        // Các lỗi khác hoặc hủy yêu cầu cũng tự động hủy transaction: không có vé hay hóa đơn nào được lưu.
    }

    private Task<DateTime> DatabaseUtcNowAsync(CancellationToken cancellationToken) =>
        _context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);

    public Task<Ticket?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => _context.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TicketDto> UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Không thử lại tự động: caller phải tải dữ liệu và kiểm tra lại quyền/trạng thái.
            throw new TicketConcurrencyException(exception);
        }
        return TicketProjection.ToDto(ticket);
    }
}
