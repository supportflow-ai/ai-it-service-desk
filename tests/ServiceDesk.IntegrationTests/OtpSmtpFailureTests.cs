using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class OtpSmtpFailureTests
{
    [Fact]
    public async Task SmtpFailureIsPropagatedSoTheDurableJobCanRecordIt()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            await peer.GetStream().WriteAsync(Encoding.ASCII.GetBytes("554 Service unavailable\r\n"), timeout.Token);
        });
        var sender = new SmtpOtpSender(Options.Create(new SmtpEmailOptions
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, EnableSsl = false, FromAddress = "sender@test.invalid"
        }), NullLogger<SmtpOtpSender>.Instance);
        await Assert.ThrowsAsync<SmtpException>(() => sender.SendPasswordResetCodeAsync("user@test.invalid", "739251", DateTime.UtcNow.AddMinutes(15), timeout.Token));
        await server;
    }

    [Fact]
    public async Task MissingSmtpIsAnExplicitDeliveryFailure()
    {
        var sender = new NoOpOtpSender(NullLogger<NoOpOtpSender>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendPasswordResetCodeAsync("user@test.invalid", "739251", DateTime.UtcNow.AddMinutes(15)));
    }

    [Fact]
    public async Task CancellationDuringSmtpDoesNotReportSuccess()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var sending = new CancellationTokenSource();
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
            accepted.SetResult();
            await release.Task.WaitAsync(deadline.Token);
        });
        var sender = new SmtpOtpSender(Options.Create(new SmtpEmailOptions
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, EnableSsl = false, FromAddress = "sender@test.invalid"
        }), NullLogger<SmtpOtpSender>.Instance);
        var delivery = sender.SendPasswordResetCodeAsync("user@test.invalid", "739251", DateTime.UtcNow.AddMinutes(15), sending.Token);
        try
        {
            await accepted.Task.WaitAsync(deadline.Token);
            sending.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
        }
        finally
        {
            release.TrySetResult();
            await server;
        }
    }
}
