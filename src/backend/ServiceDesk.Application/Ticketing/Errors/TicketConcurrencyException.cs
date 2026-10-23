namespace ServiceDesk.Application.Ticketing.Errors;

/// <summary>Xung đột khi một yêu cầu khác đã thay đổi hoặc xóa phiếu vừa được tải.</summary>
public sealed class TicketConcurrencyException(Exception innerException)
    : Exception("Phiếu đã được thay đổi bởi yêu cầu khác. Hãy tải lại dữ liệu trước khi thao tác.", innerException);
