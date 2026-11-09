using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.GetCurrentUser;

/// <summary>Hợp đồng use case lấy thông tin tài khoản hiện tại.</summary>
public interface IGetCurrentUser
{
    Task<UserInfo?> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy thông tin người dùng đang thực hiện request từ claims JWT.
/// </summary>
internal sealed class GetCurrentUserUseCase(IUserAccountGateway accounts, ICurrentUser currentUser) : IGetCurrentUser
{
    public Task<UserInfo?> ExecuteAsync(CancellationToken cancellationToken = default) =>
        accounts.FindByIdAsync(AuthenticatedActor.RequireId(currentUser).ToString("D"), cancellationToken);
}
