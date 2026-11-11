using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Danh mục lỗi nghiệp vụ lặp lại của Ticketing: giữ một mã ổn định và một thông điệp tiếng Việt cho mỗi tình huống
/// để Domain, Application và Infrastructure không chép lại chuỗi. Tất cả đều là <see cref="DomainException"/>;
/// mã HTTP do loại lỗi quyết định (404, 403, 400).
/// </summary>
internal static class TicketErrors
{
    /// <summary>Phiếu không tồn tại hoặc ngoài phạm vi người gọi được biết (404 không body, không lộ sự tồn tại).</summary>
    public static NotFoundException NotFound() => new("ticket_not_found", "Không tìm thấy phiếu.");

    public static NotFoundException UploadNotFound() => new("attachment_upload_not_found", "Không tìm thấy yêu cầu tải tệp.");

    public static NotFoundException AttachmentNotFound() => new("attachment_not_found", "Không tìm thấy tệp đính kèm.");

    /// <summary>Thao tác chỉ dành cho Agent/Admin. <paramref name="action"/> là cụm động từ, ví dụ "nhận xử lý phiếu".</summary>
    public static ForbiddenException StaffRequired(string action) =>
        new("ticket_staff_required", $"Chỉ kỹ thuật viên hoặc quản trị viên mới có thể {action}.");

    /// <summary>Phiếu đã Closed/Cancelled nên không nhận thêm thao tác (400, giữ như hợp đồng hiện hành).</summary>
    public static RuleViolationException TicketClosed(string message) => new("ticket_closed", message);

    public static InvalidInputException PagingInvalid() =>
        new("paging_invalid", "page phải từ 1 trở lên và pageSize phải từ 1 đến 100.");

    public static InvalidInputException FileSizeInvalid() =>
        new("attachment_size_invalid", "Kích thước tệp phải từ 1 byte đến 10MB.");
}
