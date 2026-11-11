namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Lớp cơ sở cho tình huống tạm thời chưa xử lý được nhưng người gọi có thể gửi lại cùng yêu cầu
/// (ví dụ một request cùng Idempotency-Key đang được xử lý).
/// Tầng trình bày chuyển nhóm này thành HTTP 503 kèm header Retry-After.
/// </summary>
public abstract class RetryLaterException : DomainException
{
    protected RetryLaterException(string code, string message, Exception innerException)
        : base(code, message, innerException) { }

    public sealed override int StatusCode => 503;

    /// <summary>Số giây gợi ý cho header Retry-After.</summary>
    public virtual int RetryAfterSeconds => 1;
}
