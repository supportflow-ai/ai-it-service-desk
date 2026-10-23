namespace ServiceDesk.ArchitectureTests.Rules;

/// <summary>Quyền gọi từ HTTP độc lập với quyền sử dụng contract giữa các mô-đun.</summary>
internal static class PublishedContracts
{
    internal static readonly IReadOnlySet<string> Presentation = new HashSet<string>(StringComparer.Ordinal)
    {
        "ServiceDesk.Domain.Identity.RoleNames", "ServiceDesk.Domain.Identity.PolicyNames",
        "ServiceDesk.Domain.Ticketing.TicketCategory", "ServiceDesk.Domain.Ticketing.Impact", "ServiceDesk.Domain.Ticketing.Urgency",
        "ServiceDesk.Application.Identity.RegisterUser.IRegisterUser", "ServiceDesk.Application.Identity.LoginUser.ILoginUser",
        "ServiceDesk.Application.Identity.GetCurrentUser.IGetCurrentUser",
        "ServiceDesk.Application.Identity.Contracts.RegisterRequest", "ServiceDesk.Application.Identity.Contracts.LoginRequest",
        "ServiceDesk.Application.Identity.Contracts.AuthResult", "ServiceDesk.Application.Identity.Contracts.UserInfo",
        "ServiceDesk.Application.Ticketing.CreateTicket.ICreateTicket", "ServiceDesk.Application.Ticketing.CreateTicket.CreateTicketCommand",
        "ServiceDesk.Application.Ticketing.GetMyTickets.IGetMyTickets", "ServiceDesk.Application.Ticketing.GetMyTickets.GetMyTicketsQuery",
        "ServiceDesk.Application.Ticketing.GetTicketDetail.IGetTicketDetail", "ServiceDesk.Application.Ticketing.GetTicketDetail.GetTicketDetailQuery",
        "ServiceDesk.Application.Ticketing.Contracts.TicketDto", "ServiceDesk.Application.Ticketing.Contracts.PagedTicketResult",
        "ServiceDesk.Application.Ticketing.UpdateTicket.IUpdateTicket", "ServiceDesk.Application.Ticketing.UpdateTicket.UpdateTicketCommand",
        "ServiceDesk.Application.Ticketing.ClaimTicket.IClaimTicket", "ServiceDesk.Application.Ticketing.ClaimTicket.ClaimTicketCommand",
        "ServiceDesk.Application.Ticketing.ResolveTicket.IResolveTicket", "ServiceDesk.Application.Ticketing.ResolveTicket.ResolveTicketCommand",
        "ServiceDesk.Application.Ticketing.ReopenTicket.IReopenTicket", "ServiceDesk.Application.Ticketing.ReopenTicket.ReopenTicketCommand",
        "ServiceDesk.Application.Ticketing.CloseTicket.ICloseTicket", "ServiceDesk.Application.Ticketing.CloseTicket.CloseTicketCommand",
        "ServiceDesk.Application.Ticketing.CancelTicket.ICancelTicket", "ServiceDesk.Application.Ticketing.CancelTicket.CancelTicketCommand",
        "ServiceDesk.Application.Ticketing.Errors.TicketConcurrencyException",
        "ServiceDesk.Application.Ticketing.Idempotency.IdempotencyConflictException",
        "ServiceDesk.Application.Ticketing.Idempotency.IdempotencyInProgressException"
    };

    private static readonly IReadOnlyDictionary<(string Source, string Target), IReadOnlySet<string>> BetweenModules =
        new Dictionary<(string, string), IReadOnlySet<string>>
        {
            [("Ticketing", "Identity")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "ServiceDesk.Domain.Identity.RoleNames"
            },
            [("AIAssistance", "Ticketing")] = new HashSet<string>(StringComparer.Ordinal)
            {
                "ServiceDesk.Domain.Ticketing.TicketCategory",
                "ServiceDesk.Domain.Ticketing.Impact",
                "ServiceDesk.Domain.Ticketing.Urgency"
            }
        };

    internal static bool Allows(string source, string target, string contract) =>
        BetweenModules.TryGetValue((source, target), out var contracts) && contracts.Contains(contract);
}
