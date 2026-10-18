using ServiceDesk.Application.Identity.Models;

namespace ServiceDesk.Application.Identity.Interfaces;

public interface IAccessTokenIssuer
{
    string GenerateToken(UserInfo user);
}
