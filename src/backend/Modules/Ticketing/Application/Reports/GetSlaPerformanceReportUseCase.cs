using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.Reports;

public record GetReportQuery(DateTime? From = null, DateTime? To = null, Guid? TeamId = null);

public interface IGetSlaPerformanceReport
{
    Task<SlaPerformanceReportDto> ExecuteAsync(GetReportQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetSlaPerformanceReportUseCase(
    ITicketReportQueries reportQueries,
    ISupportTeamMembershipReader membershipReader,
    ICurrentUser currentUser) : IGetSlaPerformanceReport
{
    public async Task<SlaPerformanceReportDto> ExecuteAsync(GetReportQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên hỗ trợ hoặc quản trị viên mới có quyền xem báo cáo hiệu suất.");
        }

        TicketReadScope scope;
        if (TicketPermissions.IsAdmin(currentUser))
        {
            scope = new TicketReadScope.AllTickets();
        }
        else
        {
            if (query.TeamId.HasValue)
            {
                var activeTeams = await membershipReader.GetTeamIdsAsync(actorId, cancellationToken);
                if (!activeTeams.Contains(query.TeamId.Value))
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền xem báo cáo của nhóm hỗ trợ này.");
                }
            }
            scope = new TicketReadScope.AgentTickets(actorId);
        }

        return await reportQueries.GetSlaPerformanceAsync(scope, query.From, query.To, query.TeamId, cancellationToken);
    }
}
