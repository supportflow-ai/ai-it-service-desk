namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>Bảo vệ mã entropy thấp theo đích và mục đích; không dùng digest của refresh token.</summary>
public interface IOtpCodeProtector
{
    string Hash(string normalizedEmail, string purpose, string code);
}
