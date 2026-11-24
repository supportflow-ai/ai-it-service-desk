using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.GetTicketCategories;

public interface IGetTicketCategories
{
    Task<IReadOnlyList<TicketCategoryMetadataDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

internal sealed class GetTicketCategoriesUseCase : IGetTicketCategories
{
    private static readonly IReadOnlyList<TicketCategoryMetadataDto> Categories =
    [
        new(TicketCategory.ACC.ToString(), "Tài khoản & Đăng nhập", "Sự cố tài khoản, mật khẩu, xác thực người dùng và phân quyền cá nhân.", "P3"),
        new(TicketCategory.NET.ToString(), "Mạng & Kết nối", "Kết nối mạng nội bộ LAN, Wi-Fi và đường truyền Internet văn phòng.", "P2"),
        new(TicketCategory.VPN.ToString(), "Mạng riêng ảo VPN", "Truy cập mạng từ xa, chứng chỉ kết nối VPN và xác thực 2 lớp qua VPN.", "P2"),
        new(TicketCategory.MAIL.ToString(), "Email & Tin nhắn", "Thư điện tử Outlook/Exchange, hòm thư nhóm và hệ thống liên lạc nội bộ.", "P3"),
        new(TicketCategory.SW.ToString(), "Phần mềm & Ứng dụng", "Cài đặt phần mềm làm việc, kích hoạt bản quyền, lỗi ứng dụng chuyên ngành.", "P3"),
        new(TicketCategory.HW.ToString(), "Phần cứng máy tính", "Hỏng hóc máy tính, thay thế bàn phím, chuột, màn hình và linh kiện.", "P3"),
        new(TicketCategory.PRINT.ToString(), "Máy in & Thiết bị VP", "Kết nối máy in mạng, kẹt giấy, thay mực, máy quét văn phòng.", "P4"),
        new(TicketCategory.ACCESS.ToString(), "Quyền truy cập thư mục", "Cấp quyền truy cập thư mục chia sẻ, cơ sở dữ liệu nội bộ và Google Drive.", "P3"),
        new(TicketCategory.EQUIP.ToString(), "Cấp phát thiết bị", "Yêu cầu cấp phát mới laptop, màn hình phụ, tai nghe hoặc phụ kiện làm việc.", "P4"),
        new(TicketCategory.SEC.ToString(), "An ninh & Bảo mật", "Sự cố nghi ngờ nhiễm mã độc, tấn công giả mạo (phishing), rò rỉ dữ liệu.", "P1"),
        new(TicketCategory.OTHER.ToString(), "Yêu cầu khác", "Các yêu cầu hỗ trợ công nghệ thông tin khác chưa được phân loại.", "P3")
    ];

    public Task<IReadOnlyList<TicketCategoryMetadataDto>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Categories);
}
