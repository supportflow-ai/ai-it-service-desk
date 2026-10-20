using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.GetCurrentUser;

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
