namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng đọc tư cách thành viên nhóm hỗ trợ (ADR-016). Thành viên hợp lệ = có dòng thành viên
/// <b>và</b> nhóm đang hoạt động; thu hồi tư cách là xóa dòng thành viên. Dữ liệu lấy từ cơ sở dữ liệu ở mỗi lần gọi,
/// không dựa vào JWT hay dữ liệu client, và không tra Identity: người dùng không có dòng thành viên thì không có
/// quyền nào. Một người thuộc nhiều nhóm có quyền là hợp của mọi nhóm; <c>IsLead</c> chỉ xét riêng từng nhóm.
/// </summary>
public interface ISupportTeamMembershipReader
{
    /// <summary>Mã mọi nhóm đang hoạt động mà người dùng là thành viên; rỗng nếu không có.</summary>
    Task<IReadOnlyList<Guid>> GetTeamIdsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Người dùng có là thành viên hợp lệ (nhóm đang hoạt động) của nhóm này không.</summary>
    Task<bool> IsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Người dùng là thành viên hợp lệ của nhóm này và được đánh dấu Lead trong chính nhóm đó.</summary>
    Task<bool> IsLeadAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);
}
