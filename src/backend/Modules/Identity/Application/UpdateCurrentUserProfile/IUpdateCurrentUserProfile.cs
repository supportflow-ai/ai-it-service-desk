namespace ServiceDesk.Modules.Identity.Application.UpdateCurrentUserProfile;

using ServiceDesk.Modules.Identity.Application.Contracts;

public interface IUpdateCurrentUserProfile
{
    Task<UserInfo?> ExecuteAsync(UpdateCurrentUserProfileCommand command, CancellationToken cancellationToken = default);
}
