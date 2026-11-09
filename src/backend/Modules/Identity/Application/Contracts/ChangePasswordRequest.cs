namespace ServiceDesk.Modules.Identity.Application.Contracts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
