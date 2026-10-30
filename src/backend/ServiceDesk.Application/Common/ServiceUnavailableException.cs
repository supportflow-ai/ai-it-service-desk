namespace ServiceDesk.Application.Common;

/// <summary>
/// Ngoại lệ ném ra khi một dịch vụ phụ thuộc bên ngoài (MinIO, AI Provider...) không khả dụng.
/// Tầng Endpoint map ngoại lệ này sang HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException(string message) : Exception(message);
