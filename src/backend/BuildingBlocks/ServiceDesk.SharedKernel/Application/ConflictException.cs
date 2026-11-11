namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Lớp cơ sở cho các xung đột nghiệp vụ mà người gọi phải tải lại dữ liệu hoặc đổi yêu cầu
/// (ví dụ xung đột ghi đồng thời, Idempotency-Key bị tái sử dụng với payload khác).
/// Tầng trình bày chuyển mọi ngoại lệ thuộc nhóm này thành HTTP 409 Conflict.
/// </summary>
public abstract class ConflictException : DomainException
{
    protected ConflictException(string code, string message) : base(code, message) { }

    protected ConflictException(string code, string message, Exception innerException) : base(code, message, innerException) { }

    public sealed override int StatusCode => 409;
}
