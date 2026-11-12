namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Dữ liệu đầu vào thiếu, sai định dạng hoặc ngoài giới hạn cho phép. Tầng trình bày trả HTTP 400.
/// Thay cho <see cref="ArgumentException"/> ở những chỗ mã ServiceDesk chủ động từ chối đầu vào của người dùng.
/// </summary>
public sealed class InvalidInputException(string code, string message) : DomainException(code, message)
{
    public override int StatusCode => 400;
}
