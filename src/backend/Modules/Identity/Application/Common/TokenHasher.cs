using System.Security.Cryptography;
using System.Text;

namespace ServiceDesk.Modules.Identity.Application.Common;

internal static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
