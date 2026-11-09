namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Phòng ban tổ chức của người dùng/requester. Không đại diện cho nhóm hỗ trợ và không dùng để cấp quyền xử lý
/// phiếu (xem ADR-016). Dữ liệu được nạp bằng migration/seed;
/// ứng dụng hiện chỉ đọc nên không có thao tác thay đổi.
/// </summary>
public sealed class Department
{
    private Department()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
