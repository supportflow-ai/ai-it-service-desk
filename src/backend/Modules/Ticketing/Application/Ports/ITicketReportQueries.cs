using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

public interface ITicketReportQueries
{
    Task<SlaPerformanceReportDto> GetSlaPerformanceAsync(
        TicketReadScope readScope,
        DateTime? from,
        DateTime? to,
        Guid? teamId,
        CancellationToken cancellationToken = default);

    Task<TicketDistributionReportDto> GetDistributionAsync(
        TicketReadScope readScope,
        DateTime? from,
        DateTime? to,
        Guid? teamId,
        CancellationToken cancellationToken = default);
}
