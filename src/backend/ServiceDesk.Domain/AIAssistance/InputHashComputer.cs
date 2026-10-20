using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text;

namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Tính toán mã băm (Hash) tất định cho dữ liệu đầu vào của ticket gửi tới AI.
/// Sử dụng để phát hiện dữ liệu cũ (stale suggestion): nếu mã hash thay đổi do người dùng cập nhật ticket,
/// toàn bộ các gợi ý cũ đang chờ duyệt sẽ bị đánh dấu hết hạn (Expired).
/// Sử dụng thuật toán SHA-256 ổn định và bất biến.
/// </summary>
public static class InputHashComputer
{
    /// <summary>
    /// Tính toán chuỗi băm SHA-256 tất định từ tiêu đề và mô tả ticket.
    /// </summary>
    /// <param name="title">Tiêu đề ticket (bắt buộc).</param>
    /// <param name="description">Mô tả ticket.</param>
    /// <returns>Chuỗi hex mã băm SHA-256 chữ thường.</returns>
    public static string Compute(string title, string? description)
    {
        ArgumentNullException.ThrowIfNull(title);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("ai-input-v2\0"));
        Span<byte> length = stackalloc byte[4];
        foreach (var field in new[] { title.Trim(), (description ?? string.Empty).Trim() })
        {
            var bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Kiểm tra định dạng chuỗi hash SHA-256 (phải đúng 64 ký tự hex chữ thường).
    /// </summary>
    public static bool IsValid(string? hash) => hash is { Length: 64 } &&
        hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
