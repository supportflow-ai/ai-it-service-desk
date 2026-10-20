using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Application.Identity.Ports;

/// <summary>
/// Cổng tạo chuỗi mã truy cập xác thực (JWT Token).
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Sinh mã JWT Bearer token chứa các claims tương ứng với thông tin tài khoản và roles.
    /// </summary>
    string GenerateToken(UserInfo user);
}
