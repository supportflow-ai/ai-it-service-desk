namespace ServiceDesk.Modules.Identity.Application.UpdateCurrentUserProfile;

public record UpdateCurrentUserProfileCommand(string? FullName, string? PhoneNumber);
