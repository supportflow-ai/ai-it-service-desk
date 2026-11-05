using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.ChangePassword;

public interface IChangePassword
{
    Task<IdentityOperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ChangePasswordUseCase(
    IUserAccountGateway accounts,
    ICurrentUser currentUser) : IChangePassword
{
    public Task<IdentityOperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = AuthenticatedActor.RequireId(currentUser);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Task.FromResult(IdentityOperationResult.Failed("Current password and new password are required."));
        }

        return accounts.ChangePasswordAsync(actorId.ToString(), request.CurrentPassword, request.NewPassword, cancellationToken);
    }
}
