namespace ServiceDesk.Application.Ticketing.Dtos;

public record PagedTicketResult(
    IReadOnlyList<TicketDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
