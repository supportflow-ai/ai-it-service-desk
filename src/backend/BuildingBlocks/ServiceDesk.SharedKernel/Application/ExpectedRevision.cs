namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Kiểm soát đồng thời lạc quan dùng chung cho mọi lệnh ghi có <c>expectedRevision</c> (Ticketing, Knowledge):
/// bắt buộc có revision khác 0 (lỗi đầu vào, 400) và phải khớp revision hiện tại (xung đột, 409).
/// </summary>
public static class ExpectedRevision
{
    /// <summary>Mã lỗi khi thiếu <c>expectedRevision</c> hoặc bằng 0.</summary>
    public const string RequiredCode = "expected_revision_required";

    /// <summary>Thông điệp khi thiếu <c>expectedRevision</c> hoặc bằng 0.</summary>
    public const string RequiredMessage = "Cần cung cấp expectedRevision khác 0.";

    /// <summary>
    /// Yêu cầu có revision mong đợi khác 0 và khớp revision hiện tại.
    /// <paramref name="currentRevision"/> là hàm để chỉ đọc revision khi đã có revision mong đợi.
    /// </summary>
    /// <param name="staleMessage">Thông điệp 409 riêng của module; để trống dùng thông điệp mặc định.</param>
    /// <exception cref="InvalidInputException">Thiếu revision mong đợi hoặc bằng 0.</exception>
    /// <exception cref="ConcurrencyConflictException">Revision mong đợi khác revision hiện tại.</exception>
    public static void Require(uint? expectedRevision, Func<uint> currentRevision, string? staleMessage = null)
    {
        ArgumentNullException.ThrowIfNull(currentRevision);
        if (!expectedRevision.HasValue || expectedRevision.Value == 0)
            throw new InvalidInputException(RequiredCode, RequiredMessage);
        if (currentRevision() != expectedRevision.Value)
            throw staleMessage is null ? new ConcurrencyConflictException() : new ConcurrencyConflictException(staleMessage);
    }
}
