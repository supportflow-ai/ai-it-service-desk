namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>
/// Bản ghi hóa đơn Idempotency lưu tại tầng Infrastructure, được commit nguyên tử cùng ticket.
/// </summary>
public sealed class TicketIdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid TicketId { get; set; }

    // Chỉ lưu phản hồi DTO đã chấp nhận để replay chính xác kết quả ban đầu.
    // Tuyệt đối không lưu header Authorization, token, khóa raw hoặc body request thô.
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
