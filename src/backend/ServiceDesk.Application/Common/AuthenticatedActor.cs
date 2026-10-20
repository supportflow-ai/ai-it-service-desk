using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Application.Common;

/// <summary>
/// Tiện ích xác thực danh tính người dùng bắt buộc trong tầng Application.
/// </summary>
internal static class AuthenticatedActor
{
    /// <summary>
    /// Kiểm tra người dùng đã xác thực và trả về Guid UserId hợp lệ.
    /// Ném UnauthorizedAccessException nếu chưa đăng nhập hoặc ID không hợp lệ.
    /// </summary>
    public static Guid RequireId(ICurrentUser user)
    {
        if (!user.IsAuthenticated || !Guid.TryParse(user.UserId, out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Yêu cầu danh tính người dùng đã được xác thực.");
        return id;
    }
}
