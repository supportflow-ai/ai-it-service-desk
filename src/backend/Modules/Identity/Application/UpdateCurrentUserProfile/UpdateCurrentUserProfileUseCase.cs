namespace ServiceDesk.Modules.Identity.Application.UpdateCurrentUserProfile;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

internal sealed class UpdateCurrentUserProfileUseCase(
    IUserAccountGateway accounts,
    ICurrentUser currentUser) : IUpdateCurrentUserProfile
{
    public async Task<UserInfo?> ExecuteAsync(UpdateCurrentUserProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = AuthenticatedActor.RequireId(currentUser).ToString("D");

        if (command.PhoneNumber is not null)
        {
            var phone = command.PhoneNumber.Trim();
            if (phone.Length > 20)
            {
                throw new ArgumentException("Số điện thoại không được vượt quá 20 ký tự.");
            }
        }

        if (command.FullName is not null)
        {
            var name = command.FullName.Trim();
            if (name.Length > 100)
            {
                throw new ArgumentException("Họ tên không được vượt quá 100 ký tự.");
            }
        }

        return await accounts.UpdateProfileAsync(userId, command.FullName, command.PhoneNumber, cancellationToken);
    }
}
