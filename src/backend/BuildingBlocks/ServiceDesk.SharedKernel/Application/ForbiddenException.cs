namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Người gọi đã xác thực nhưng không có quyền thực hiện thao tác (HTTP 403).
/// Thay cho <see cref="UnauthorizedAccessException"/> ở những chỗ mã ServiceDesk chủ động từ chối quyền.
/// </summary>
public sealed class ForbiddenException(string code, string message) : DomainException(code, message)
{
    public override int StatusCode => 403;
}
