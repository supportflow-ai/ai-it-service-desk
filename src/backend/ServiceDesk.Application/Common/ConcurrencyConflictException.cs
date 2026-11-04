namespace ServiceDesk.Application.Common;

/// <summary>
/// Ngoại lệ ném ra khi phát hiện xung đột ghi đồng thời (Optimistic Concurrency Conflict).
/// Tầng Endpoint chuyển đổi ngoại lệ này thành HTTP 409 Conflict.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.")
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
