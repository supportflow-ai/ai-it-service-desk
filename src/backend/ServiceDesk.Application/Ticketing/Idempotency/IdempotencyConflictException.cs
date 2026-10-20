namespace ServiceDesk.Application.Ticketing.Idempotency;

/// <summary>
/// Ngoại lệ ném ra khi Idempotency-Key bị tái sử dụng với nội dung payload khác biệt (Xung đột Idempotency).
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("Idempotency-Key đã được sử dụng trước đó với nội dung payload khác.") { }
}

/// <summary>
/// Ngoại lệ ném ra khi một request có cùng Idempotency-Key đang trong quá trình xử lý đồng thời (Chờ hoàn tất).
/// </summary>
public sealed class IdempotencyInProgressException : Exception
{
    public IdempotencyInProgressException(Exception innerException)
        : base("Một request khác cùng Idempotency-Key đang được xử lý. Vui lòng thử lại sau.", innerException) { }
}
