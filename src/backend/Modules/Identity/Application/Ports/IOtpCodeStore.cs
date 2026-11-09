using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>Cổng lưu mã OTP (chỉ lưu giá trị băm).</summary>
public interface IOtpCodeStore
{
    /// <summary>
    /// Phát mã mới nếu đích và mục đích của <paramref name="otp"/> chưa có <paramref name="maxIssued"/> mã kể từ
    /// <paramref name="since"/>; khi phát thì vô hiệu mọi mã còn hiệu lực trước đó. Đếm, vô hiệu và thêm mã chạy tuần tự
    /// cho cùng một đích, nên yêu cầu đồng thời không vượt giới hạn và chỉ còn một mã hiệu lực.
    /// Trả false (không ghi gì) khi đã đạt giới hạn.
    /// </summary>
    Task<bool> TryIssueAsync(OtpCode otp, int maxIssued, DateTime since, CancellationToken cancellationToken = default);
}
