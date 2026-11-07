namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Phòng ban / bộ phận trong tổ chức (dùng để định tuyến IT Helpdesk). Dữ liệu được nạp bằng migration/seed;
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
