namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Phòng ban tổ chức của người dùng/requester. Không đại diện cho nhóm hỗ trợ và không dùng để cấp quyền xử lý
/// phiếu (xem ADR-016). Chỉ Admin được tạo và cập nhật qua Application.
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

    public static Department Create(string code, string name, string? description, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (code.Trim().Length > 50) throw new ArgumentException("Mã phòng ban không được vượt quá 50 ký tự.", nameof(code));
        ValidateDetails(name, description);
        return new Department
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = now
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateDetails(name, description);
        Name = name.Trim();
        Description = description?.Trim();
    }

    private static void ValidateDetails(string name, string? description)
    {
        if (name.Trim().Length > 100) throw new ArgumentException("Tên phòng ban không được vượt quá 100 ký tự.", nameof(name));
        if (description?.Trim().Length > 500) throw new ArgumentException("Mô tả phòng ban không được vượt quá 500 ký tự.", nameof(description));
    }
}
