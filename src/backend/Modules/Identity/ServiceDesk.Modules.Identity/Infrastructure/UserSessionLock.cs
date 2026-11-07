using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Khóa dòng người dùng trong <c>AspNetUsers</c> làm khóa phiên: mọi thao tác tạo hoặc thu hồi refresh token
/// (đăng nhập, xoay vòng, đổi/đặt lại mật khẩu, đổi vai trò, thu hồi toàn bộ) lấy khóa này trước trong giao dịch của mình.
/// Vì vậy lệnh thu hồi luôn bắt đầu sau khi token mới đã commit, hoặc token cũ đã bị thu hồi trước khi xoay vòng chạy;
/// không có token nào lọt qua khe giữa hai giao dịch (Read Committed chỉ thấy dòng đã commit khi câu lệnh bắt đầu).
/// Thứ tự khóa cố định: (khóa vai trò Admin) → người dùng → refresh token.
/// </summary>
internal static class UserSessionLock
{
    public static Task LockAsync(DbContext context, string userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM public.\"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE", cancellationToken);
}
