namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Danh mục sự cố dạng Enum chuẩn hoá.
/// Được AI Triage sử dụng để kiểm tra tính hợp lệ của phân loại gợi ý.
/// </summary>
public enum TicketCategory
{
    /// <summary>Sự cố tài khoản (mật khẩu, đăng nhập, thông tin người dùng).</summary>
    ACC,

    /// <summary>Kết nối mạng nội bộ và đường truyền Internet.</summary>
    NET,

    /// <summary>Mạng riêng ảo VPN và truy cập từ xa.</summary>
    VPN,

    /// <summary>Thư điện tử Email và tin nhắn nội bộ.</summary>
    MAIL,

    /// <summary>Cài đặt phần mềm, bản quyền ứng dụng, cập nhật hệ thống.</summary>
    SW,

    /// <summary>Hỏng hóc phần cứng, thay thế linh kiện máy tính.</summary>
    HW,

    /// <summary>Máy in và máy quét văn phòng.</summary>
    PRINT,

    /// <summary>Phân quyền truy cập, quyền hạn nhóm và thư mục.</summary>
    ACCESS,

    /// <summary>Yêu cầu cấp phát thiết bị làm việc (laptop, màn hình, phụ kiện).</summary>
    EQUIP,

    /// <summary>Sự cố an ninh & an toàn thông tin — luôn yêu cầu phân loại thủ công.</summary>
    SEC,

    /// <summary>Yêu cầu khác hoặc chưa phân loại.</summary>
    OTHER
}
