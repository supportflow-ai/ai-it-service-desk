namespace ServiceDesk.Application.Identity.Contracts;

public sealed record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);
