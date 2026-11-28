using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

public interface IPasswordResetJobStore
{
    Task<PasswordResetJob?> ClaimAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<PasswordResetJob?> PrepareAsync(PasswordResetJob claim, OtpCode code, string protectedCode, DateTime now, CancellationToken cancellationToken = default);
    Task<bool> RenewForSendAsync(PasswordResetJob claim, DateTime now, CancellationToken cancellationToken = default);
    Task CompleteAsync(PasswordResetJob claim, DateTime now, string? failureCode = null, CancellationToken cancellationToken = default);
    Task FailAsync(PasswordResetJob claim, DateTime now, bool retry, string failureCode, CancellationToken cancellationToken = default);
}
