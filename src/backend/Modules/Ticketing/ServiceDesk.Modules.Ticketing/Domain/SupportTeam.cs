namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Nhóm hỗ trợ: nơi xử lý phiếu, tách hoàn toàn khỏi <c>Department</c> (phòng ban của người gửi). Xem ADR-016.
/// Giai đoạn nền dữ liệu: bảng và hàng đợi phân loại thủ công được tạo bằng migration; chưa có thao tác ghi,
/// chưa có định tuyến và chưa dùng để cấp quyền đọc.
/// </summary>
public sealed class SupportTeam
{
    /// <summary>Mã cố định của hàng đợi phân loại thủ công do migration tạo sẵn.</summary>
    public const string ManualTriageCode = "MANUAL_TRIAGE";

    /// <summary>Tên hiển thị của hàng đợi phân loại thủ công.</summary>
    public const string ManualTriageName = "Phân loại thủ công";

    /// <summary>Mã định danh cố định của hàng đợi phân loại thủ công (trùng giá trị seed trong migration).</summary>
    public static readonly Guid ManualTriageId = new("5b1f0c2e-7a64-4d3b-9e18-2c6a8f4d1b07");

    private SupportTeam()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Định danh ổn định, không phụ thuộc tên hiển thị; duy nhất trong hệ thống.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Đúng một nhóm là hàng đợi phân loại thủ công; cơ sở dữ liệu bảo đảm tối đa một.</summary>
    public bool IsTriageQueue { get; private set; }

    public bool IsActive { get; private set; }
}
