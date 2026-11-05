using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.Ports;

public interface IOtpCodeStore
{
    Task SaveOtpAsync(OtpCode otp, CancellationToken cancellationToken = default);
    Task<OtpCode?> FindValidOtpAsync(string target, string purpose, string codeHash, DateTime now, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default);
}
