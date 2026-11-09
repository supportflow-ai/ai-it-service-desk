namespace ServiceDesk.Modules.Identity.Application.Contracts;

/// <summary>Kết quả đăng nhập bằng mật khẩu: người dùng và refresh token gốc (chỉ trả cho client một lần).</summary>
public sealed record PasswordSignIn(UserInfo User, string RawRefreshToken);
