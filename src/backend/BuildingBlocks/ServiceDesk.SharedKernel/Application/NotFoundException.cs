namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Tài nguyên không tồn tại hoặc nằm ngoài phạm vi người gọi được phép biết (HTTP 404, không kèm body để
/// không lộ việc tài nguyên có tồn tại hay không). Thay cho <see cref="KeyNotFoundException"/>.
/// </summary>
public sealed class NotFoundException(string code, string message) : DomainException(code, message)
{
    public override int StatusCode => 404;
}
