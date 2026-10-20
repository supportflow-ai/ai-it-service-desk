namespace ServiceDesk.Application.Ticketing.Contracts;

/// <summary>
/// Kết quả phân trang danh sách ticket.
/// </summary>
/// <param name="Items">Danh sách các vé trong trang hiện tại.</param>
/// <param name="Page">Chỉ số trang hiện tại (bắt đầu từ 1).</param>
/// <param name="PageSize">Số lượng vé tối đa trên mỗi trang.</param>
/// <param name="TotalCount">Tổng số lượng vé thỏa mãn điều kiện lọc.</param>
public record PagedTicketResult(
    IReadOnlyList<TicketDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
