namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>Bảo vệ mã entropy thấp theo đích và mục đích; không dùng digest của refresh token.</summary>
public interface IOtpCodeProtector
{
    string Hash(string normalizedEmail, string purpose, string code);
    string Protect(string normalizedEmail, Guid otpId, string code);
    string Unprotect(string normalizedEmail, Guid otpId, string protectedCode);
}
