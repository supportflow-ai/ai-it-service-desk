namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Thành viên của một nhóm hỗ trợ; khóa chính (TeamId, UserId) nên một người có thể thuộc nhiều nhóm.
/// <see cref="UserId"/> cùng kiểu với mã người dùng trong Ticketing và không có khóa ngoại tới bảng Identity
/// (ranh giới module); việc kiểm người dùng tồn tại sẽ đi qua cổng danh bạ khi có thao tác ghi.
/// <see cref="IsLead"/> chỉ là năng lực trong nhóm này, không phải vai trò JWT (ADR-016).
/// </summary>
public sealed class SupportTeamMember
{
    private SupportTeamMember()
    {
    }

    public Guid TeamId { get; private set; }

    public Guid UserId { get; private set; }

    public bool IsLead { get; private set; }
}
