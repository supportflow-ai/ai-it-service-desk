namespace ServiceDesk.Application.Identity.Contracts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
