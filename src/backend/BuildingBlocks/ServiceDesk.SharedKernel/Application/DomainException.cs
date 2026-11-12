namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Gốc của mọi lỗi nghiệp vụ có chủ đích do mã ServiceDesk ném ra. Khác với ngoại lệ của .NET hay thư viện
/// (<see cref="ArgumentException"/>, <see cref="InvalidOperationException"/>...), kiểu này luôn mang
/// <see cref="Code"/> ổn định để client nhận diện và <see cref="StatusCode"/> tương thích với mã HTTP hiện hành.
/// Tầng trình bày chỉ trả <see cref="Exception.Message"/> cho client khi lỗi thuộc nhóm này; ngoại lệ cùng loại
/// đến từ .NET hoặc thư viện chỉ nhận thông điệp chung và được ghi vào log.
/// </summary>
public abstract class DomainException : Exception
{
    private const int MaxCodeLength = 64;

    protected DomainException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        if (!IsValidCode(code))
            throw new ArgumentException("Mã lỗi phải ở dạng snake_case, 2–64 ký tự, bắt đầu bằng chữ cái thường.", nameof(code));
        Code = code;
    }

    /// <summary>Mã lỗi ổn định (snake_case); không đổi khi sửa câu chữ của thông điệp.</summary>
    public string Code { get; }

    /// <summary>Mã HTTP tương thích mà tầng trình bày trả về cho lỗi này.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Mã hợp lệ: <c>[a-z][a-z0-9_]{1,63}</c>.</summary>
    public static bool IsValidCode(string? code)
    {
        if (code is null || code.Length < 2 || code.Length > MaxCodeLength) return false;
        if (code[0] is < 'a' or > 'z') return false;
        foreach (var c in code)
            if (c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_') return false;
        return true;
    }
}
