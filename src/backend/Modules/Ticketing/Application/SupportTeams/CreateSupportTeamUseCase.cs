using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public interface ICreateSupportTeam
{
    Task<SupportTeamDto> ExecuteAsync(CreateSupportTeamRequest request, CancellationToken cancellationToken = default);
}

public sealed class SupportTeamConflictException(string message) : ConflictException(message);

internal sealed class CreateSupportTeamUseCase(
    ISupportTeamStore supportTeamStore,
    ICurrentUser currentUser) : ICreateSupportTeam
{
    public async Task<SupportTeamDto> ExecuteAsync(CreateSupportTeamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ quản trị viên mới có quyền tạo nhóm hỗ trợ.");
        }

        if (await supportTeamStore.ExistsCodeAsync(request.Code, cancellationToken))
        {
            throw new SupportTeamConflictException($"Mã nhóm hỗ trợ '{request.Code}' đã tồn tại.");
        }

        var team = SupportTeam.Create(request.Code, request.Name);
        await supportTeamStore.AddTeamAsync(team, cancellationToken);

        return new SupportTeamDto(
            team.Id,
            team.Code,
            team.Name,
            team.IsTriageQueue,
            team.IsActive,
            Array.Empty<SupportTeamMemberDto>());
    }
}
