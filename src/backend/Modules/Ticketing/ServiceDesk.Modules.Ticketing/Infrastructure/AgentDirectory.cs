using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Tra cứu nhân viên hỗ trợ cho Ticketing qua danh bạ người dùng do Identity công bố;
/// Ticketing không đọc bảng của ASP.NET Core Identity.
/// </summary>
internal sealed class AgentDirectory(IUserDirectory users) : IAgentDirectory
{
    /// <summary>Tên hiển thị khi người dùng không tồn tại hoặc chưa có tên.</summary>
    internal const string UnknownUserName = "Người dùng";

    private static readonly string[] StaffRoles = [RoleNames.Agent, RoleNames.Admin];

    public Task<bool> IsAgentAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        users.IsInAnyRoleAsync(agentId, StaffRoles, cancellationToken);

    public async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var name = await users.FindDisplayNameAsync(userId, cancellationToken);
        return !string.IsNullOrWhiteSpace(name) ? name : UnknownUserName;
    }
}
