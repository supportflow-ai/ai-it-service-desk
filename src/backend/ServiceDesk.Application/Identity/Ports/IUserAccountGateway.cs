using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Application.Identity.Ports;

/// <summary>Kết quả tạo tài khoản người dùng.</summary>
public sealed record AccountCreation(UserInfo? User, IReadOnlyList<string> Errors);

/// <summary>
/// Cổng giao tiếp với tầng hạ tầng quản lý tài khoản người dùng (ASP.NET Core Identity).
/// </summary>
public interface IUserAccountGateway
{
    /// <summary>Tạo tài khoản người dùng mới với vai trò mặc định là Requester.</summary>
    Task<AccountCreation> CreateRequesterAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Kiểm tra thông tin đăng nhập và mật khẩu của người dùng.</summary>
    Task<UserInfo?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Tìm kiếm thông tin người dùng theo Id.</summary>
    Task<UserInfo?> FindByIdAsync(string userId, CancellationToken cancellationToken);
}
