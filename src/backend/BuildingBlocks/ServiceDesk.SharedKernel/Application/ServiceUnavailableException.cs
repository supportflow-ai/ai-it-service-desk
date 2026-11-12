namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Ngoại lệ ném ra khi một dịch vụ phụ thuộc bên ngoài (MinIO, AI Provider...) không khả dụng.
/// Tầng Endpoint map ngoại lệ này sang HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException : DomainException
{
    /// <summary>Mã lỗi mặc định khi không cần phân biệt dịch vụ.</summary>
    public const string DefaultCode = "service_unavailable";

    public ServiceUnavailableException(string message) : base(DefaultCode, message) { }

    public ServiceUnavailableException(string code, string message) : base(code, message) { }

    public sealed override int StatusCode => 503;
}
