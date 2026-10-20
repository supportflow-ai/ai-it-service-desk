namespace ServiceDesk.Application.Ticketing.CreateTicket;

/// <summary>
/// Lệnh yêu cầu tạo mới một ticket hỗ trợ IT.
/// </summary>
/// <param name="Title">Tiêu đề ngắn gọn mô tả sự cố (tối đa 100 ký tự).</param>
/// <param name="Description">Nội dung chi tiết mô tả sự cố (tối thiểu 10 ký tự).</param>
/// <param name="CategoryId">Mã danh mục sự cố (thuộc TicketCategories.All).</param>
public record CreateTicketCommand(
    string Title,
    string Description,
    string CategoryId
);
