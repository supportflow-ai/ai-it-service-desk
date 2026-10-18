using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Application.Identity.Ports;

public sealed record AccountCreation(UserInfo? User, IReadOnlyList<string> Errors);

public interface IUserAccountGateway
{
    Task<AccountCreation> CreateRequesterAsync(string email, string password, CancellationToken cancellationToken);
    Task<UserInfo?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);
    Task<UserInfo?> FindByIdAsync(string userId, CancellationToken cancellationToken);
}
