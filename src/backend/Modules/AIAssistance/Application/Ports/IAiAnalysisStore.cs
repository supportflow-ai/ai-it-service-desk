using ServiceDesk.Modules.AIAssistance.Domain;

namespace ServiceDesk.Modules.AIAssistance.Application.Ports;

/// <summary>Kho lưu bền kết quả phân tích AI và quyết định HITL. Phân tích lại không ghi đè lịch sử.</summary>
public interface IAiAnalysisStore
{
    Task AddAsync(AIAnalysis analysis, CancellationToken cancellationToken = default);

    Task<AIAnalysis?> GetLatestAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ranh giới giao dịch của lệnh HITL: mở giao dịch, khóa analysis của đúng phiếu, gọi <paramref name="decide"/>
    /// (nhận null khi analysis không thuộc phiếu), lưu analysis rồi commit. Mọi lệnh ghi trong
    /// <paramref name="decide"/> trên cùng scope cùng commit hoặc cùng rollback.
    /// Không gọi dịch vụ bên ngoài trong <paramref name="decide"/>.
    /// </summary>
    Task<TResult> DecideAsync<TResult>(
        Guid ticketId,
        Guid analysisId,
        Func<AIAnalysis?, Task<TResult>> decide,
        CancellationToken cancellationToken = default);
}
