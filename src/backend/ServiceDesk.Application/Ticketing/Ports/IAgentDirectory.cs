namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng tra cứu thông tin nhân viên hỗ trợ (Agent) phục vụ phân công và hiển thị timeline.
/// </summary>
public interface IAgentDirectory
{
    Task<bool> IsAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
