namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>
/// Hàng đợi yêu cầu quên mật khẩu. Endpoint chỉ đưa email vào hàng đợi rồi trả về ngay, nên thời gian phản hồi
/// không phụ thuộc email có tồn tại hay việc gửi mã chậm hay nhanh.
/// </summary>
public interface IPasswordResetQueue
{
    /// <summary>Lưu yêu cầu bền vững trước khi trả true; false khi hàng đợi đầy.</summary>
    Task<bool> EnqueueAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
