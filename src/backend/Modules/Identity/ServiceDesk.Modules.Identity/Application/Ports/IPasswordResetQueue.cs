namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>
/// Hàng đợi yêu cầu quên mật khẩu. Endpoint chỉ đưa email vào hàng đợi rồi trả về ngay, nên thời gian phản hồi
/// không phụ thuộc email có tồn tại hay việc gửi mã chậm hay nhanh.
/// </summary>
public interface IPasswordResetQueue
{
    /// <summary>Đưa email đã chuẩn hóa vào hàng đợi; false khi hàng đợi đầy (yêu cầu bị bỏ, không báo cho client).</summary>
    bool TryEnqueue(string normalizedEmail);
}
