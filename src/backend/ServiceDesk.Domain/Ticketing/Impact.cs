namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Mức độ ảnh hưởng của sự cố đối với hoạt động của tổ chức (Impact).
/// Dùng để tính toán độ ưu tiên: Priority = Impact × Urgency.
/// </summary>
public enum Impact
{
    /// <summary>Ảnh hưởng thấp (cá nhân nhỏ lẻ, không gián đoạn công việc quan trọng).</summary>
    Low,

    /// <summary>Ảnh hưởng trung bình (một nhóm người dùng hoặc một phần quy trình bị chậm).</summary>
    Medium,

    /// <summary>Ảnh hưởng nghiêm trọng (toàn bộ phòng ban hoặc hệ thống trọng yếu bị gián đoạn).</summary>
    High
}
