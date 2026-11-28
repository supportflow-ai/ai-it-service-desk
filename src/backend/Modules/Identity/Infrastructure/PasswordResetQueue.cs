using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>Worker nhúng trong API; nhiều replica phối hợp claim/lease trên PostgreSQL.</summary>
internal sealed class PasswordResetQueueProcessor(IServiceScopeFactory scopes, IClock clock, ILogger<PasswordResetQueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IPasswordResetJobStore>();
                var claim = await store.ClaimAsync(clock.UtcNow.UtcDateTime, stoppingToken);
                if (claim is not null)
                {
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<PasswordResetCodeIssuer>().ProcessAsync(claim, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        var permanent = ex is FormatException or InvalidOperationException or CryptographicException || ex is SmtpException smtp && (int)smtp.StatusCode >= 500;
                        await store.FailAsync(claim, clock.UtcNow.UtcDateTime, !permanent, permanent ? "PermanentDeliveryFailure" : "TransientDeliveryFailure", stoppingToken);
                        logger.LogWarning("Xử lý gửi OTP thất bại ({ErrorType}, {JobId}).", ex.GetType().Name, claim.Id);
                    }
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError("Hàng đợi OTP không khả dụng ({ErrorType}).", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
