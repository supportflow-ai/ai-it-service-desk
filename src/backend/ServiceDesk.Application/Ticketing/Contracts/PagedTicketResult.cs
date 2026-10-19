namespace ServiceDesk.Application.Ticketing.Contracts;

public record PagedTicketResult(
    IReadOnlyList<TicketDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
