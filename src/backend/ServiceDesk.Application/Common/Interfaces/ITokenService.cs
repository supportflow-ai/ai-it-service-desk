using ServiceDesk.Application.Auth;

namespace ServiceDesk.Application.Common.Interfaces;

public interface ITokenService
{
    string Create(IdentityUserRecord user);
}
