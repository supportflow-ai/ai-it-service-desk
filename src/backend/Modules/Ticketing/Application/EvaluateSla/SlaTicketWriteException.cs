namespace ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

/// <summary>Lỗi ghi riêng một phiếu; adapter đã bỏ entity lỗi để batch tiếp tục an toàn.</summary>
internal sealed class SlaTicketWriteException(Exception inner) : Exception("Không thể ghi marker SLA của phiếu.", inner);
