namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>
/// Ngoại lệ ném ra khi thao tác bảo trì cơ sở dữ liệu thất bại; chỉ chứa mã lỗi an toàn, không bao giờ lộ thông tin mật.
/// </summary>
public sealed class DatabaseMaintenanceException(string code) : InvalidOperationException(code);
