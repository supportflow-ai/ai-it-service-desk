using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Đặt lại mật khẩu bằng OTP trong một giao dịch PostgreSQL:
/// 1. Khóa dòng mã còn hiệu lực mới nhất của email (FOR UPDATE) để hai request không cùng dùng một mã.
/// 2. Mã đã vượt số lần nhập sai hoặc sai mã: tăng bộ đếm, commit, trả lỗi chung.
/// 3. Đúng mã: đổi mật khẩu, đánh dấu mã đã dùng, thu hồi mọi refresh token, commit.
/// Lỗi khi đổi mật khẩu (ví dụ mật khẩu yếu) làm rollback, mã vẫn còn dùng được.
/// </summary>
internal sealed class PasswordResetGateway(UserManager<IdentityUser> users, DbContext context) : IPasswordResetGateway
{
    private const string InvalidCode = "Mã OTP không hợp lệ hoặc đã hết hạn.";

    public async Task<IdentityOperationResult> ResetWithOtpAsync(string normalizedEmail, string codeHash, string newPassword, DateTime now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var purpose = OtpCode.ResetPasswordPurpose;
        // ToListAsync không ghép thêm toán tử nên EF gửi nguyên câu lệnh, không bọc FOR UPDATE trong truy vấn con.
        var otp = (await context.Set<OtpCode>().FromSqlInterpolated(
                $"SELECT * FROM public.otp_codes WHERE target = {normalizedEmail} AND purpose = {purpose} AND is_used = false AND expires_at >= {now} ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();
        if (otp is null || !otp.IsValid(now))
        {
            return IdentityOperationResult.Failed(InvalidCode);
        }

        if (!otp.Matches(codeHash))
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.otp_codes SET failed_attempts = failed_attempts + 1 WHERE id = {otp.Id}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return IdentityOperationResult.Failed(InvalidCode);
        }

        var user = await users.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return IdentityOperationResult.Failed(InvalidCode);
        }

        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, resetToken, newPassword);
        if (!reset.Succeeded)
        {
            return IdentityOperationResult.Failed(reset.Errors.Select(e => e.Description));
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE public.otp_codes SET is_used = true WHERE id = {otp.Id}", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE public.refresh_tokens SET revoked_at = {now} WHERE user_id = {user.Id} AND revoked_at IS NULL", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IdentityOperationResult.Success();
    }
}
