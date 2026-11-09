namespace ServiceDesk.Modules.Identity.Application.Contracts;

public sealed record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);
