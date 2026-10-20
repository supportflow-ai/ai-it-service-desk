namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Danh sách các mã danh mục sự cố IT được hỗ trợ trong hệ thống.
/// </summary>
public static class TicketCategories
{
    /// <summary>Tài khoản & Phân quyền truy cập (Account & Access)</summary>
    public const string ACC = "ACC";

    /// <summary>Mạng nội bộ & Internet (Network)</summary>
    public const string NET = "NET";

    /// <summary>Mạng riêng ảo (VPN)</summary>
    public const string VPN = "VPN";

    /// <summary>Hộp thư điện tử (Email)</summary>
    public const string MAIL = "MAIL";

    /// <summary>Phần mềm & Ứng dụng (Software)</summary>
    public const string SW = "SW";

    /// <summary>Phần cứng máy tính & Thiết bị (Hardware)</summary>
    public const string HW = "HW";

    /// <summary>Máy in & Máy quét (Printer)</summary>
    public const string PRINT = "PRINT";

    /// <summary>Thẻ từ & Truy cập vật lý (Physical Access)</summary>
    public const string ACCESS = "ACCESS";

    /// <summary>Trang thiết bị văn phòng (Equipment)</summary>
    public const string EQUIP = "EQUIP";

    /// <summary>Bảo mật & An toàn thông tin (Security)</summary>
    public const string SEC = "SEC";

    /// <summary>Yêu cầu khác (Other)</summary>
    public const string OTHER = "OTHER";

    /// <summary>Tập hợp tất cả các danh mục hợp lệ.</summary>
    public static readonly string[] All = { ACC, NET, VPN, MAIL, SW, HW, PRINT, ACCESS, EQUIP, SEC, OTHER };
}
