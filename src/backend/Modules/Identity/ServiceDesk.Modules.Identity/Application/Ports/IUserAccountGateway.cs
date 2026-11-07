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

    /// <summary>Đổi mật khẩu người dùng.</summary>
    Task<IdentityOperationResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    /// <summary>Đặt lại mật khẩu người dùng (sau khi đã xác thực OTP thành công).</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>Gán danh sách vai trò cho người dùng (Admin).</summary>
    Task<IdentityOperationResult> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken);
}
