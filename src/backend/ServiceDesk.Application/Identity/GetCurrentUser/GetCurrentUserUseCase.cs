using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.GetCurrentUser;

public interface IGetCurrentUser
{
    Task<UserInfo?> ExecuteAsync(CancellationToken cancellationToken = default);
}

internal sealed class GetCurrentUserUseCase(IUserAccountGateway accounts, ICurrentUser currentUser) : IGetCurrentUser
{
    public Task<UserInfo?> ExecuteAsync(CancellationToken cancellationToken = default) =>
        accounts.FindByIdAsync(AuthenticatedActor.RequireId(currentUser).ToString("D"), cancellationToken);
}
