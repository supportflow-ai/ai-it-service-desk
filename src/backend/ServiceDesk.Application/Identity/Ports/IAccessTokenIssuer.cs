using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Application.Identity.Ports;

public interface IAccessTokenIssuer
{
    string GenerateToken(UserInfo user);
}
