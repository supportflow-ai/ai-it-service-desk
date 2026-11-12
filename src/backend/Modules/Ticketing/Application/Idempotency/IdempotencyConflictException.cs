using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Application.Idempotency;

/// <summary>
/// Ngoại lệ ném ra khi Idempotency-Key bị tái sử dụng với nội dung payload khác biệt (Xung đột Idempotency).
/// </summary>
public sealed class IdempotencyConflictException : ConflictException
{
    public IdempotencyConflictException()
        : base("idempotency_key_reused", "Idempotency-Key đã được sử dụng trước đó với nội dung payload khác.") { }
}

/// <summary>
/// Ngoại lệ ném ra khi một request có cùng Idempotency-Key đang trong quá trình xử lý đồng thời (Chờ hoàn tất).
/// </summary>
public sealed class IdempotencyInProgressException : RetryLaterException
{
    public IdempotencyInProgressException(Exception innerException)
        : base("idempotency_in_progress", "Một request khác cùng Idempotency-Key đang được xử lý. Vui lòng thử lại sau.", innerException) { }
}
