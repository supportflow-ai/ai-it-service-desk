using ServiceDesk.Modules.Identity.Application.Contracts;

namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>Kết quả tạo tài khoản người dùng.</summary>
public sealed record AccountCreation(UserInfo? User, IReadOnlyList<string> Errors);

/// <summary>
/// Cổng giao tiếp với tầng hạ tầng quản lý tài khoản người dùng (ASP.NET Core Identity).
/// </summary>
public interface IUserAccountGateway
{
    /// <summary>Tạo tài khoản người dùng mới với vai trò mặc định là Requester.</summary>
    Task<AccountCreation> CreateRequesterAsync(string email, string password, string? fullName, CancellationToken cancellationToken);

    /// <summary>Kiểm tra thông tin đăng nhập và mật khẩu của người dùng.</summary>
    Task<UserInfo?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Tìm kiếm thông tin người dùng theo Id.</summary>
    Task<UserInfo?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Kiểm tra xem người dùng có tồn tại và thuộc vai trò Agent hay không.</summary>
    Task<bool> IsAgentAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Lấy danh sách người dùng theo vai trò cụ thể.</summary>
    Task<IReadOnlyList<UserInfo>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Đổi mật khẩu và, trong cùng giao dịch, thu hồi mọi refresh token của người dùng tại <paramref name="revokeSessionsAt"/>.
    /// Lỗi ở bước nào thì không bước nào được ghi.
    /// </summary>
    Task<IdentityOperationResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, DateTime revokeSessionsAt,
        CancellationToken cancellationToken);

    /// <summary>Email đã có tài khoản hay chưa.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Thay toàn bộ vai trò và, trong cùng giao dịch, thu hồi mọi refresh token của người dùng tại <paramref name="revokeSessionsAt"/>.
    /// Từ chối khi thao tác gỡ vai trò Admin của quản trị viên cuối cùng; các thao tác đổi Admin chạy tuần tự nên
    /// hai yêu cầu đồng thời không thể cùng gỡ hai Admin cuối.
    /// </summary>
    Task<IdentityOperationResult> AssignRolesAsync(string userId, IReadOnlyList<string> roles, DateTime revokeSessionsAt,
        CancellationToken cancellationToken);
}
