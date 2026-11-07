using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>Cổng lưu mã OTP (chỉ lưu giá trị băm).</summary>
public interface IOtpCodeStore
{
    /// <summary>Lưu mã mới và vô hiệu các mã còn hiệu lực trước đó của cùng đích và mục đích.</summary>
    Task IssueAsync(OtpCode otp, CancellationToken cancellationToken = default);

    /// <summary>Đếm số mã đã phát cho một đích và mục đích kể từ thời điểm cho trước.</summary>
    Task<int> CountIssuedSinceAsync(string target, string purpose, DateTime since, CancellationToken cancellationToken = default);
}
